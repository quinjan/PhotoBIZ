using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhotoBIZ.Api.Data;

namespace PhotoBIZ.Api;

public sealed class PhotoBizTransactionWorkflow(
    PhotoBizDbContext dbContext,
    PhotoBizAuditService auditService)
{
    private static readonly TimeSpan PendingCashWindow = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan PostSessionPromptDuration = TimeSpan.FromSeconds(15);
    private const int MaximumExtraPrintCopies = 5;
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

    public async Task<Transaction> CreateExtraPrintAddOnAsync(
        Transaction parentTransaction,
        PhotoBizCurrentUser currentUser,
        int copyCount,
        CancellationToken cancellationToken)
    {
        if (copyCount is < 1 or > MaximumExtraPrintCopies)
        {
            throw new InvalidOperationException("Extra print copy count must be between 1 and 5.");
        }

        if (parentTransaction.TransactionType != StatusValues.TransactionType.SessionPurchase ||
            parentTransaction.Status != StatusValues.Transaction.Completed)
        {
            throw new InvalidOperationException("Extra prints require a completed session purchase.");
        }

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == parentTransaction.BoothId, cancellationToken);
        if (PhotoBizBoothAvailability.IsAgentOffline(booth, DateTimeOffset.UtcNow))
        {
            throw new InvalidOperationException("The booth agent is offline.");
        }

        if (currentUser.IsCashier && currentUser.AssignedBoothId != booth.Id)
        {
            throw new InvalidOperationException("Cashiers can create extra prints only for their assigned booth.");
        }

        if (await HasAnotherActiveTransactionAsync(booth.Id, null, cancellationToken))
        {
            throw new InvalidOperationException("The booth already has an active transaction.");
        }

        var latestCompletedSessionId = await dbContext.Transactions
            .AsNoTracking()
            .Where(item =>
                item.BoothId == booth.Id &&
                item.TransactionType == StatusValues.TransactionType.SessionPurchase &&
                item.Status == StatusValues.Transaction.Completed)
            .OrderByDescending(item => item.CompletedAt ?? item.CreatedAt)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestCompletedSessionId != parentTransaction.Id)
        {
            throw new InvalidOperationException("Extra prints are available only for the latest completed booth session.");
        }

        using var offerSnapshot = JsonDocument.Parse(parentTransaction.OfferSnapshot);
        var snapshotRoot = offerSnapshot.RootElement;
        var offerType = GetSnapshotString(snapshotRoot, "OfferType") ?? string.Empty;
        var allowsExtraPrintAddOn = GetSnapshotBoolean(snapshotRoot, "AllowsExtraPrintAddOn");
        var extraPrintPriceCents = GetSnapshotInt(snapshotRoot, "ExtraPrintPriceCents");

        if (offerType != StatusValues.OfferType.PerSession)
        {
            throw new InvalidOperationException("Extra prints are available only for PER_SESSION transactions.");
        }

        if (!allowsExtraPrintAddOn || extraPrintPriceCents is null or <= 0)
        {
            throw new InvalidOperationException("This transaction does not allow extra print add-ons.");
        }

        var now = DateTimeOffset.UtcNow;
        var addOn = new Transaction
        {
            Id = Guid.NewGuid(),
            ClientAccountId = parentTransaction.ClientAccountId,
            LocationId = parentTransaction.LocationId,
            BoothId = parentTransaction.BoothId,
            BoothOfferId = parentTransaction.BoothOfferId,
            BoothOfferActivationId = parentTransaction.BoothOfferActivationId,
            ParentTransactionId = parentTransaction.Id,
            TransactionNumber = $"TXN-{now:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            TransactionType = StatusValues.TransactionType.ExtraPrintAddOn,
            PaymentMethod = StatusValues.PaymentMethod.Cash,
            Status = StatusValues.Transaction.PendingCash,
            AmountCents = extraPrintPriceCents.Value * copyCount,
            Currency = parentTransaction.Currency,
            OfferSnapshot = parentTransaction.OfferSnapshot,
            ExtraPrintCount = copyCount,
            CreatedAt = now,
            ExpiresAt = now.Add(PendingCashWindow)
        };

        booth.CurrentState = StatusValues.Booth.PaymentPending;
        dbContext.Transactions.Add(addOn);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            currentUser,
            "transaction.extra_print_add_on_created",
            nameof(Transaction),
            addOn.Id,
            new
            {
                addOn.TransactionNumber,
                ParentTransactionNumber = parentTransaction.TransactionNumber,
                CopyCount = copyCount
            },
            cancellationToken);

        return addOn;
    }

    public async Task ReturnBoothToWelcomeAsync(
        Booth booth,
        PhotoBizCurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var activeTransactions = await dbContext.Transactions
            .Where(transaction =>
                transaction.BoothId == booth.Id &&
                !TerminalTransactionStatuses.Contains(transaction.Status))
            .OrderBy(transaction => transaction.CreatedAt)
            .ToListAsync(cancellationToken);
        var recoveredTransactions = new List<(Transaction Transaction, string PreviousStatus)>(activeTransactions.Count);

        foreach (var transaction in activeTransactions)
        {
            recoveredTransactions.Add((transaction, transaction.Status));
            transaction.Status = StatusValues.Transaction.Cancelled;
            transaction.CancelledAt ??= now;
            transaction.FailureReason = "Manual booth recovery returned the booth to welcome.";

            var session = await dbContext.BoothSessions
                .OrderByDescending(item => item.Id)
                .FirstOrDefaultAsync(item => item.TransactionId == transaction.Id, cancellationToken);

            if (session is not null)
            {
                session.Status = StatusValues.Session.Failed;
                session.EndedAt ??= now;
            }
        }

        booth.CurrentState = StatusValues.Booth.Welcome;
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var recovered in recoveredTransactions)
        {
            await auditService.WriteAsync(
                currentUser,
                "transaction.cancelled",
                nameof(Transaction),
                recovered.Transaction.Id,
                new
                {
                    recovered.Transaction.TransactionNumber,
                    recovered.PreviousStatus,
                    Reason = "Manual booth recovery returned the booth to welcome."
                },
                cancellationToken);
        }
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
        var isExtraPrintAddOn = transaction.TransactionType == StatusValues.TransactionType.ExtraPrintAddOn;
        booth.CurrentState = isExtraPrintAddOn
            ? StatusValues.Booth.PrintingOrSharing
            : StatusValues.Booth.StartingLumabooth;
        using var offerSnapshot = JsonDocument.Parse(transaction.OfferSnapshot);
        var snapshotRoot = offerSnapshot.RootElement;
        var lumaboothSessionMode = NormalizeLumaboothSessionMode(GetSnapshotString(snapshotRoot, "LumaboothSessionMode"));
        var offerType = GetSnapshotString(snapshotRoot, "OfferType") ?? string.Empty;
        var includedPrintEntitlement = GetSnapshotString(snapshotRoot, "IncludedPrintEntitlement") ?? string.Empty;

        if (!isExtraPrintAddOn)
        {
            var session = new BoothSession
            {
                Id = Guid.NewGuid(),
                BoothId = booth.Id,
                TransactionId = transaction.Id,
                Status = StatusValues.Session.Starting
            };
            dbContext.BoothSessions.Add(session);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PhotoBizAgentCommand(
            transaction.Id,
            transaction.TransactionNumber,
            transaction.TransactionType,
            transaction.BoothOfferId,
            offerType,
            includedPrintEntitlement,
            lumaboothSessionMode,
            transaction.ExtraPrintCount);
    }

    public async Task MarkSessionStartedAsync(
        Guid transactionId,
        Guid boothId,
        string? lumaboothSessionRef,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions.SingleOrDefaultAsync(
            item => item.Id == transactionId && item.BoothId == boothId,
            cancellationToken);

        if (transaction is null)
        {
            return;
        }

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
        if (!string.IsNullOrWhiteSpace(lumaboothSessionRef))
        {
            session.LumaboothSessionRef = lumaboothSessionRef.Trim();
        }

        session.Status = StatusValues.Session.InSession;
        session.StartedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSessionCompletedAsync(
        Guid transactionId,
        Guid boothId,
        string? lumaboothSessionRef,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions.SingleOrDefaultAsync(
            item => item.Id == transactionId && item.BoothId == boothId,
            cancellationToken);

        if (transaction is null)
        {
            return;
        }

        if (transaction.Status != StatusValues.Transaction.InSession)
        {
            return;
        }

        transaction.Status = StatusValues.Transaction.Completed;
        transaction.CompletedAt = DateTimeOffset.UtcNow;

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
        booth.CurrentState = StatusValues.Booth.Completed;

        var session = await dbContext.BoothSessions
            .OrderByDescending(item => item.Id)
            .FirstAsync(item => item.TransactionId == transactionId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(lumaboothSessionRef))
        {
            session.LumaboothSessionRef = lumaboothSessionRef.Trim();
        }

        session.Status = StatusValues.Session.Completed;
        session.EndedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkPrintCompletedAsync(
        Guid transactionId,
        Guid boothId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions.SingleOrDefaultAsync(
            item => item.Id == transactionId && item.BoothId == boothId,
            cancellationToken);

        if (transaction is null)
        {
            return;
        }

        if (transaction.TransactionType != StatusValues.TransactionType.ExtraPrintAddOn ||
            transaction.Status != StatusValues.Transaction.StartingSession)
        {
            return;
        }

        transaction.Status = StatusValues.Transaction.Completed;
        transaction.CompletedAt = DateTimeOffset.UtcNow;

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
        booth.CurrentState = StatusValues.Booth.Welcome;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkPrintFailedAsync(
        Guid transactionId,
        Guid boothId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions.SingleOrDefaultAsync(
            item => item.Id == transactionId && item.BoothId == boothId,
            cancellationToken);

        if (transaction is null)
        {
            return;
        }

        if (transaction.TransactionType != StatusValues.TransactionType.ExtraPrintAddOn ||
            transaction.Status != StatusValues.Transaction.StartingSession)
        {
            return;
        }

        transaction.Status = StatusValues.Transaction.SessionFailed;
        transaction.FailureReason = reason;

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
        booth.CurrentState = StatusValues.Booth.Error;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ResetCompletedBoothsToWelcomeAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(PostSessionPromptDuration);
        var booths = await dbContext.Booths
            .Where(booth => booth.CurrentState == StatusValues.Booth.Completed)
            .ToListAsync(cancellationToken);

        if (booths.Count == 0)
        {
            return 0;
        }

        var boothIds = booths.Select(booth => booth.Id).ToArray();
        var latestCompletedTransactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                boothIds.Contains(transaction.BoothId) &&
                transaction.TransactionType == StatusValues.TransactionType.SessionPurchase &&
                transaction.Status == StatusValues.Transaction.Completed)
            .ToListAsync(cancellationToken);
        var eligibleBoothIds = latestCompletedTransactions
            .GroupBy(transaction => transaction.BoothId)
            .Select(group => group
                .OrderByDescending(transaction => transaction.CompletedAt ?? transaction.CreatedAt)
                .ThenByDescending(transaction => transaction.CreatedAt)
                .First())
            .Where(transaction => transaction.CompletedAt <= cutoff)
            .Select(transaction => transaction.BoothId)
            .ToHashSet();

        foreach (var booth in booths.Where(booth => eligibleBoothIds.Contains(booth.Id)))
        {
            booth.CurrentState = StatusValues.Booth.Welcome;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return eligibleBoothIds.Count;
    }

    public async Task MarkSessionFailedAsync(
        Guid transactionId,
        Guid boothId,
        string? reason,
        string? lumaboothSessionRef,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions.SingleOrDefaultAsync(
            item => item.Id == transactionId && item.BoothId == boothId,
            cancellationToken);

        if (transaction is null)
        {
            return;
        }

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
        if (!string.IsNullOrWhiteSpace(lumaboothSessionRef))
        {
            session.LumaboothSessionRef = lumaboothSessionRef.Trim();
        }

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

    private static string? GetSnapshotString(JsonElement snapshot, string propertyName)
    {
        return snapshot.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool GetSnapshotBoolean(JsonElement snapshot, string propertyName)
    {
        return snapshot.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.True;
    }

    private static int? GetSnapshotInt(JsonElement snapshot, string propertyName)
    {
        return snapshot.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;
    }

    private static string NormalizeLumaboothSessionMode(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized switch
        {
            StatusValues.LumaboothSessionMode.Print or StatusValues.LumaboothSessionMode.LegacySessionStandard => StatusValues.LumaboothSessionMode.Print,
            StatusValues.LumaboothSessionMode.Gif => StatusValues.LumaboothSessionMode.Gif,
            StatusValues.LumaboothSessionMode.Boomerang => StatusValues.LumaboothSessionMode.Boomerang,
            StatusValues.LumaboothSessionMode.Video => StatusValues.LumaboothSessionMode.Video,
            _ => StatusValues.LumaboothSessionMode.Print
        };
    }
}

public sealed record PhotoBizAgentCommand(
    Guid TransactionId,
    string TransactionNumber,
    string TransactionType,
    Guid BoothOfferId,
    string OfferType,
    string IncludedPrintEntitlement,
    string LumaboothSessionMode,
    int ExtraPrintCount);
