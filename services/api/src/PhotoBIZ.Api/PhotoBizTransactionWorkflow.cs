using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhotoBIZ.Api.Data;

namespace PhotoBIZ.Api;

public sealed class PhotoBizTransactionWorkflow(
    PhotoBizDbContext dbContext,
    PhotoBizAuditService auditService)
{
    private static readonly TimeSpan PendingCashWindow = TimeSpan.FromMinutes(5);
    private static readonly string[] TerminalTransactionStatuses =
    [
        StatusValues.Transaction.Completed,
        StatusValues.Transaction.Expired,
        StatusValues.Transaction.Cancelled
    ];

    public async Task<Transaction> CreateTransactionAsync(
        Booth booth,
        BoothOfferActivation activeOfferActivation,
        BoothOffer activeOffer,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (PhotoBizBoothAvailability.IsAgentOffline(booth, now))
        {
            throw new InvalidOperationException("The booth agent is offline.");
        }

        if (await HasAnotherActiveTransactionAsync(booth.Id, null, cancellationToken))
        {
            throw new InvalidOperationException("The booth already has an active transaction.");
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            ClientAccountId = booth.ClientAccountId,
            LocationId = booth.LocationId,
            BoothId = booth.Id,
            BoothOfferId = activeOffer.Id,
            BoothOfferActivationId = activeOfferActivation.Id,
            TransactionNumber = $"TXN-{now:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            TransactionType = StatusValues.TransactionType.SessionPurchase,
            PaymentMethod = StatusValues.PaymentMethod.Cash,
            Status = StatusValues.Transaction.Created,
            AmountCents = activeOffer.PriceCents,
            Currency = activeOffer.Currency,
            OfferSnapshot = JsonSerializer.Serialize(new
            {
                activeOffer.Id,
                activeOffer.Name,
                activeOffer.OfferType,
                activeOffer.PriceCents,
                activeOffer.Currency,
                activeOffer.IncludedPrintEntitlement,
                activeOffer.AllowsExtraPrintAddOn,
                activeOffer.ExtraPrintPriceCents,
                activeOffer.LumaboothSessionMode
            }),
            CreatedAt = now,
            ExpiresAt = now.Add(PendingCashWindow)
        };

        booth.CurrentState = StatusValues.Booth.OfferConfirmed;

        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        return transaction;
    }

    public async Task<Transaction> SetPaymentMethodAsync(
        Transaction transaction,
        Booth booth,
        string paymentMethod,
        CancellationToken cancellationToken)
    {
        if (PhotoBizBoothAvailability.IsAgentOffline(booth, DateTimeOffset.UtcNow))
        {
            throw new InvalidOperationException("The booth agent is offline.");
        }

        if (await HasAnotherActiveTransactionAsync(booth.Id, transaction.Id, cancellationToken))
        {
            throw new InvalidOperationException("The booth already has an active transaction.");
        }

        if (transaction.Status != StatusValues.Transaction.Created)
        {
            throw new InvalidOperationException("Only newly created transactions can choose a payment method.");
        }

        if (paymentMethod != StatusValues.PaymentMethod.Cash)
        {
            throw new InvalidOperationException("Only CASH is runtime-enabled in MVP.");
        }

        var cashAssignment = await dbContext.BoothPaymentOptionAssignments
            .AsNoTracking()
            .SingleOrDefaultAsync(assignment =>
                assignment.BoothId == booth.Id &&
                assignment.PaymentMethod == StatusValues.PaymentMethod.Cash &&
                assignment.RuntimeEnabled &&
                assignment.Status == StatusValues.PaymentAssignment.Assigned,
                cancellationToken);

        if (cashAssignment is null)
        {
            throw new InvalidOperationException("This booth does not have runtime cash enabled.");
        }

        transaction.PaymentMethod = paymentMethod;
        transaction.Status = StatusValues.Transaction.PendingCash;
        booth.CurrentState = StatusValues.Booth.PaymentPending;

        await dbContext.SaveChangesAsync(cancellationToken);

        return transaction;
    }

    public async Task<Transaction> ApproveCashAsync(
        Transaction transaction,
        PhotoBizCurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (transaction.Status != StatusValues.Transaction.PendingCash)
        {
            throw new InvalidOperationException("Only pending cash transactions can be approved.");
        }

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
        if (PhotoBizBoothAvailability.IsAgentOffline(booth, DateTimeOffset.UtcNow))
        {
            throw new InvalidOperationException("The booth agent is offline.");
        }

        transaction.Status = StatusValues.Transaction.Paid;
        transaction.PaidAt = DateTimeOffset.UtcNow;
        transaction.ApprovedByUserId = currentUser.UserId;
        booth.CurrentState = StatusValues.Booth.Paid;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            currentUser,
            "transaction.cash_approved",
            nameof(Transaction),
            transaction.Id,
            new { transaction.TransactionNumber },
            cancellationToken);

        return transaction;
    }

    public async Task<Transaction> CancelAsync(
        Transaction transaction,
        PhotoBizCurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (transaction.Status != StatusValues.Transaction.PendingCash &&
            transaction.Status != StatusValues.Transaction.SessionFailed)
        {
            throw new InvalidOperationException("Only pending cash or failed sessions can be cancelled.");
        }

        transaction.Status = StatusValues.Transaction.Cancelled;
        transaction.CancelledAt = DateTimeOffset.UtcNow;

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
        booth.CurrentState = StatusValues.Booth.Welcome;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            currentUser,
            "transaction.cancelled",
            nameof(Transaction),
            transaction.Id,
            new { transaction.TransactionNumber },
            cancellationToken);

        return transaction;
    }

    public async Task<int> ExpirePendingTransactionsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredTransactions = await dbContext.Transactions
            .Where(transaction =>
                transaction.Status == StatusValues.Transaction.PendingCash &&
                transaction.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expiredTransactions.Count == 0)
        {
            return 0;
        }

        var boothIds = expiredTransactions.Select(transaction => transaction.BoothId).Distinct().ToArray();
        var booths = await dbContext.Booths
            .Where(booth => boothIds.Contains(booth.Id))
            .ToDictionaryAsync(booth => booth.Id, cancellationToken);

        foreach (var transaction in expiredTransactions)
        {
            transaction.Status = StatusValues.Transaction.Expired;

            if (booths.TryGetValue(transaction.BoothId, out var booth))
            {
                booth.CurrentState = StatusValues.Booth.Welcome;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return expiredTransactions.Count;
    }

    public async Task<PhotoBizAgentCommand?> TryAcquireNextAgentCommandAsync(
        Booth booth,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions
            .Where(item => item.BoothId == booth.Id && item.Status == StatusValues.Transaction.Paid)
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (transaction is null)
        {
            return null;
        }

        transaction.Status = StatusValues.Transaction.StartingSession;
        booth.CurrentState = StatusValues.Booth.StartingLumabooth;

        var session = new BoothSession
        {
            Id = Guid.NewGuid(),
            BoothId = booth.Id,
            TransactionId = transaction.Id,
            Status = StatusValues.Session.Starting
        };
        dbContext.BoothSessions.Add(session);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PhotoBizAgentCommand(
            transaction.Id,
            transaction.TransactionNumber,
            transaction.BoothOfferId,
            JsonDocument.Parse(transaction.OfferSnapshot));
    }

    public async Task MarkSessionStartedAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions.SingleAsync(item => item.Id == transactionId, cancellationToken);

        if (transaction.Status != StatusValues.Transaction.StartingSession)
        {
            return;
        }

        transaction.Status = StatusValues.Transaction.InSession;

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
        booth.CurrentState = StatusValues.Booth.InLumaboothSession;

        var session = await dbContext.BoothSessions
            .OrderByDescending(item => item.Id)
            .FirstAsync(item => item.TransactionId == transactionId, cancellationToken);
        session.Status = StatusValues.Session.InSession;
        session.StartedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSessionCompletedAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions.SingleAsync(item => item.Id == transactionId, cancellationToken);

        if (transaction.Status != StatusValues.Transaction.InSession)
        {
            return;
        }

        transaction.Status = StatusValues.Transaction.Completed;
        transaction.CompletedAt = DateTimeOffset.UtcNow;

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
        booth.CurrentState = StatusValues.Booth.Welcome;

        var session = await dbContext.BoothSessions
            .OrderByDescending(item => item.Id)
            .FirstAsync(item => item.TransactionId == transactionId, cancellationToken);
        session.Status = StatusValues.Session.Completed;
        session.EndedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSessionFailedAsync(Guid transactionId, string? reason, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions.SingleAsync(item => item.Id == transactionId, cancellationToken);

        if (transaction.Status != StatusValues.Transaction.StartingSession &&
            transaction.Status != StatusValues.Transaction.InSession)
        {
            return;
        }

        transaction.Status = StatusValues.Transaction.SessionFailed;
        transaction.FailureReason = reason;

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
        booth.CurrentState = StatusValues.Booth.Error;

        var session = await dbContext.BoothSessions
            .OrderByDescending(item => item.Id)
            .FirstAsync(item => item.TransactionId == transactionId, cancellationToken);
        session.Status = StatusValues.Session.Failed;
        session.EndedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<bool> HasAnotherActiveTransactionAsync(
        Guid boothId,
        Guid? excludedTransactionId,
        CancellationToken cancellationToken)
    {
        return dbContext.Transactions.AnyAsync(
            transaction =>
                transaction.BoothId == boothId &&
                transaction.Id != excludedTransactionId &&
                !TerminalTransactionStatuses.Contains(transaction.Status),
            cancellationToken);
    }
}

public sealed record PhotoBizAgentCommand(
    Guid TransactionId,
    string TransactionNumber,
    Guid BoothOfferId,
    JsonDocument OfferSnapshot);
