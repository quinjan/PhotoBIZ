using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhotoBIZ.Api.Data;

namespace PhotoBIZ.Api;

public sealed class PhotoBizTransactionWorkflow(
    PhotoBizDbContext dbContext,
    PhotoBizAuditService auditService,
    IPayMongoClient? payMongoClient = null,
    PhotoBizSecretProtector? secretProtector = null)
{
    private static readonly TimeSpan PendingCashWindow = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan PostSessionPromptDuration = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan PrintingOrSharingTimeout = TimeSpan.FromSeconds(15);
    private const int MaximumExtraPrintCopies = 5;
    private static readonly string[] TerminalTransactionStatuses =
    [
        StatusValues.Transaction.Completed,
        StatusValues.Transaction.Expired,
        StatusValues.Transaction.Cancelled,
        StatusValues.Transaction.PaymentFailed
    ];
    private static readonly string[] ActiveTransactionStatusesDuringCompletedPrompt =
    [
        StatusValues.Transaction.Created,
        StatusValues.Transaction.PendingCash,
        StatusValues.Transaction.PendingPayMongoQrPh,
        StatusValues.Transaction.Paid,
        StatusValues.Transaction.StartingSession,
        StatusValues.Transaction.InSession
    ];

    public async Task<Transaction> CreateTransactionAsync(
        Booth booth,
        BoothOfferActivation activeOfferActivation,
        BoothOffer activeOffer,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await PhotoBizRuntimeAvailability.EnsureBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: true,
            cancellationToken);

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
            OfferSnapshot = SerializeOfferSnapshot(activeOffer),
            CreatedAt = now,
            ExpiresAt = now.Add(PendingCashWindow)
        };

        booth.CurrentState = StatusValues.Booth.OfferConfirmed;

        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        return transaction;
    }

    public async Task<Transaction> CreateCoveredPlanSessionAsync(
        Booth booth,
        BoothOfferActivation activeOfferActivation,
        BoothOffer activeOffer,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await PhotoBizRuntimeAvailability.EnsureBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: true,
            cancellationToken);

        if (activeOffer.OfferType == StatusValues.OfferType.PerSession)
        {
            throw new InvalidOperationException("PER_SESSION offers require payment per session.");
        }

        if (activeOfferActivation.Status != StatusValues.OfferActivation.Active)
        {
            throw new InvalidOperationException("This package is awaiting cashier activation.");
        }

        if (activeOffer.OfferType == StatusValues.OfferType.TimeUnlimited &&
            activeOfferActivation.EndsAt.HasValue &&
            activeOfferActivation.EndsAt <= now)
        {
            activeOfferActivation.Status = StatusValues.OfferActivation.Completed;
            activeOfferActivation.DeactivatedAt ??= now;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("This timed package has ended.");
        }

        if (activeOffer.OfferType == StatusValues.OfferType.SessionCount &&
            activeOfferActivation.SessionAllowance.HasValue &&
            activeOfferActivation.SessionsUsed >= activeOfferActivation.SessionAllowance.Value)
        {
            activeOfferActivation.Status = StatusValues.OfferActivation.Completed;
            activeOfferActivation.DeactivatedAt ??= now;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("This session-count package has no remaining sessions.");
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
            TransactionType = StatusValues.TransactionType.CoveredPlanSession,
            PaymentMethod = StatusValues.PaymentMethod.Cash,
            Status = StatusValues.Transaction.Paid,
            AmountCents = 0,
            Currency = activeOffer.Currency,
            OfferSnapshot = SerializeOfferSnapshot(activeOffer),
            CreatedAt = now,
            ExpiresAt = now.Add(PendingCashWindow),
            PaidAt = now
        };

        booth.CurrentState = StatusValues.Booth.Paid;

        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        return transaction;
    }

    public async Task<Transaction> CreatePlanActivationAsync(
        Booth booth,
        BoothOfferActivation pendingActivation,
        BoothOffer pendingOffer,
        PhotoBizCurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        await PhotoBizRuntimeAvailability.EnsureBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: false,
            cancellationToken);

        if (pendingOffer.OfferType == StatusValues.OfferType.PerSession)
        {
            throw new InvalidOperationException("PER_SESSION packages do not require cashier plan activation.");
        }

        if (pendingActivation.Status != StatusValues.OfferActivation.PendingPayment)
        {
            throw new InvalidOperationException("This package is not awaiting cashier activation.");
        }

        if (currentUser.IsCashier && currentUser.AssignedBoothId != booth.Id)
        {
            throw new InvalidOperationException("Cashiers can activate packages only for their assigned booth.");
        }

        if (await HasAnotherActiveTransactionAsync(booth.Id, null, cancellationToken))
        {
            throw new InvalidOperationException("The booth already has an active transaction.");
        }

        var now = DateTimeOffset.UtcNow;
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            ClientAccountId = booth.ClientAccountId,
            LocationId = booth.LocationId,
            BoothId = booth.Id,
            BoothOfferId = pendingOffer.Id,
            BoothOfferActivationId = pendingActivation.Id,
            TransactionNumber = $"TXN-{now:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            TransactionType = StatusValues.TransactionType.PlanActivation,
            PaymentMethod = StatusValues.PaymentMethod.Cash,
            Status = StatusValues.Transaction.PendingCash,
            AmountCents = pendingOffer.PriceCents,
            Currency = pendingOffer.Currency,
            OfferSnapshot = SerializeOfferSnapshot(pendingOffer),
            CreatedAt = now,
            ExpiresAt = now.Add(PendingCashWindow)
        };

        booth.CurrentState = StatusValues.Booth.PaymentPending;
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            currentUser,
            "transaction.plan_activation_created",
            nameof(Transaction),
            transaction.Id,
            new { transaction.TransactionNumber, pendingOffer.Name, pendingOffer.OfferType },
            cancellationToken);

        return transaction;
    }

    public async Task<Transaction> SetPaymentMethodAsync(
        Transaction transaction,
        Booth booth,
        string paymentMethod,
        CancellationToken cancellationToken)
    {
        await PhotoBizRuntimeAvailability.EnsureBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: true,
            cancellationToken);

        if (await HasAnotherActiveTransactionAsync(booth.Id, transaction.Id, cancellationToken))
        {
            throw new InvalidOperationException("The booth already has an active transaction.");
        }

        if (transaction.Status != StatusValues.Transaction.Created)
        {
            throw new InvalidOperationException("Only newly created transactions can choose a payment method.");
        }

        if (paymentMethod == StatusValues.PaymentMethod.Cash)
        {
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

        if (paymentMethod == StatusValues.PaymentMethod.PayMongoQrPh)
        {
            return await SetPayMongoQrPhPaymentMethodAsync(transaction, booth, cancellationToken);
        }

        throw new InvalidOperationException("This payment method is not runtime-enabled.");
    }

    private async Task<Transaction> SetPayMongoQrPhPaymentMethodAsync(
        Transaction transaction,
        Booth booth,
        CancellationToken cancellationToken)
    {
        var assignment = await dbContext.BoothPaymentOptionAssignments
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.BoothId == booth.Id &&
                item.PaymentMethod == StatusValues.PaymentMethod.PayMongoQrPh &&
                item.RuntimeEnabled &&
                item.Status == StatusValues.PaymentAssignment.Assigned,
                cancellationToken);

        if (assignment is null)
        {
            throw new InvalidOperationException("This booth does not have runtime PayMongo QR Ph enabled.");
        }

        var config = await dbContext.ClientPaymentProviderConfigs
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.ClientAccountId == booth.ClientAccountId &&
                item.Provider == StatusValues.PaymentProvider.PayMongo &&
                item.IntegrationType == StatusValues.PaymentMethod.PayMongoQrPh &&
                item.Status == StatusValues.PaymentResource.Verified,
                cancellationToken);

        if (config is null || string.IsNullOrWhiteSpace(config.EncryptedSecretKey))
        {
            throw new InvalidOperationException("PayMongo QR Ph is not verified for this client.");
        }

        if (secretProtector is null)
        {
            throw new InvalidOperationException("Payment secret protection is not configured.");
        }

        var client = payMongoClient ?? new DisabledPayMongoClient();
        var credentials = new PayMongoCredentials(
            secretProtector.Unprotect(config.EncryptedSecretKey),
            config.PaymentMode ?? StatusValues.PaymentMode.Test,
            config.BusinessAccountName);
        var qrPayment = await client.CreateQrPhPaymentAsync(credentials, transaction, cancellationToken);

        transaction.PaymentMethod = StatusValues.PaymentMethod.PayMongoQrPh;
        transaction.Status = StatusValues.Transaction.PendingPayMongoQrPh;
        transaction.ExpiresAt = qrPayment.ExpiresAt;
        booth.CurrentState = StatusValues.Booth.PaymentPending;

        dbContext.PaymentAttempts.Add(new PaymentAttempt
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            Provider = StatusValues.PaymentProvider.PayMongo,
            ProviderReference = qrPayment.PaymentIntentId,
            Status = StatusValues.Transaction.PendingPayMongoQrPh,
            RawPayload = qrPayment.RawPayload,
            CreatedAt = DateTimeOffset.UtcNow
        });

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
        await PhotoBizRuntimeAvailability.EnsureBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: transaction.TransactionType != StatusValues.TransactionType.PlanActivation,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        transaction.Status = StatusValues.Transaction.Paid;
        transaction.PaidAt = now;
        transaction.ApprovedByUserId = currentUser.UserId;

        if (transaction.TransactionType == StatusValues.TransactionType.PlanActivation)
        {
            var activation = await dbContext.BoothOfferActivations
                .Include(item => item.BoothOffer)
                .SingleAsync(item => item.Id == transaction.BoothOfferActivationId, cancellationToken);

            activation.Status = StatusValues.OfferActivation.Active;
            activation.ActivatedAt = now;
            activation.DeactivatedAt = null;
            activation.SessionsUsed = 0;
            activation.SessionAllowance = activation.BoothOffer?.SessionAllowance;
            activation.StartsAt = activation.BoothOffer?.OfferType == StatusValues.OfferType.TimeUnlimited
                ? now
                : null;
            activation.EndsAt = activation.BoothOffer?.OfferType == StatusValues.OfferType.TimeUnlimited && activation.BoothOffer.DurationHours.HasValue
                ? now.AddHours(activation.BoothOffer.DurationHours.Value)
                : null;
            transaction.Status = StatusValues.Transaction.Completed;
            transaction.CompletedAt = now;
            booth.CurrentState = StatusValues.Booth.Welcome;
        }
        else
        {
            booth.CurrentState = StatusValues.Booth.Paid;
        }

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

        var previousStatus = transaction.Status;
        transaction.Status = StatusValues.Transaction.Cancelled;
        transaction.CancelledAt = DateTimeOffset.UtcNow;
        ApplyCancellationContext(
            transaction,
            StatusValues.CancellationActor.Cashier,
            currentUser.UserId,
            StatusValues.CancellationSource.CashierPosCancelTransaction,
            previousStatus);
        transaction.FailureReason ??= "Payment request was cancelled by the cashier.";

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
        booth.CurrentState = StatusValues.Booth.Welcome;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            currentUser,
            "transaction.cancelled",
            nameof(Transaction),
            transaction.Id,
            new
            {
                transaction.TransactionNumber,
                PreviousStatus = previousStatus,
                transaction.CancelledByActorType,
                transaction.CancelledByUserId,
                transaction.CancellationSource
            },
            cancellationToken);

        return transaction;
    }

    public async Task<Transaction> CancelFromKioskAsync(
        Transaction transaction,
        Booth booth,
        string trigger,
        CancellationToken cancellationToken)
    {
        if (transaction.Status != StatusValues.Transaction.Created &&
            transaction.Status != StatusValues.Transaction.PendingCash &&
            transaction.Status != StatusValues.Transaction.PendingPayMongoQrPh)
        {
            throw new InvalidOperationException("Only created or pending payment transactions can be cancelled from the booth UI.");
        }

        if (transaction.BoothId != booth.Id)
        {
            throw new InvalidOperationException("Transaction was not found for this booth.");
        }

        var previousStatus = transaction.Status;
        var source = ResolveKioskCancellationSource(previousStatus, trigger);
        var wasCreated = transaction.Status == StatusValues.Transaction.Created;

        transaction.Status = StatusValues.Transaction.Cancelled;
        transaction.CancelledAt = DateTimeOffset.UtcNow;
        ApplyCancellationContext(
            transaction,
            StatusValues.CancellationActor.BoothUser,
            cancelledByUserId: null,
            source,
            previousStatus);
        transaction.FailureReason ??= "Customer cancelled at the booth.";
        if (wasCreated)
        {
            transaction.TerminalNoticeAcknowledgedAt ??= transaction.CancelledAt;
        }
        booth.CurrentState = StatusValues.Booth.Welcome;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteSystemAsync(
            transaction.ClientAccountId,
            "transaction.kiosk_cancelled",
            nameof(Transaction),
            transaction.Id,
            new
            {
                transaction.TransactionNumber,
                transaction.Status,
                PreviousStatus = previousStatus,
                Trigger = trigger,
                transaction.CancelledByActorType,
                transaction.CancellationSource
            },
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
        await PhotoBizRuntimeAvailability.EnsureBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: true,
            cancellationToken);

        if (IsBoothInLumaboothSession(booth))
        {
            throw new InvalidOperationException("Extra prints are unavailable while the booth is in a LumaBooth session.");
        }

        if (currentUser.IsCashier && currentUser.AssignedBoothId != booth.Id)
        {
            throw new InvalidOperationException("Cashiers can create extra prints only for their assigned booth.");
        }

        var referenceTransactionId = await ResolveExtraPrintReferenceTransactionIdAsync(booth.Id, cancellationToken);
        if (referenceTransactionId != parentTransaction.Id)
        {
            throw new InvalidOperationException("Extra prints are available only for the previous booth transaction.");
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
            var previousStatus = transaction.Status;
            recoveredTransactions.Add((transaction, previousStatus));
            transaction.Status = StatusValues.Transaction.Cancelled;
            transaction.CancelledAt ??= now;
            ApplyCancellationContext(
                transaction,
                StatusValues.CancellationActor.Cashier,
                currentUser.UserId,
                StatusValues.CancellationSource.CashierPosReturnToWelcome,
                previousStatus);
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
                    recovered.Transaction.CancelledByActorType,
                    recovered.Transaction.CancelledByUserId,
                    recovered.Transaction.CancellationSource,
                    Reason = "Manual booth recovery returned the booth to welcome."
                },
                cancellationToken);
        }
    }

    public async Task<int> ExpirePendingTransactionsAsync(CancellationToken cancellationToken, Guid? boothId = null)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredTransactions = await dbContext.Transactions
            .Where(transaction =>
                (transaction.Status == StatusValues.Transaction.PendingCash ||
                    transaction.Status == StatusValues.Transaction.PendingPayMongoQrPh) &&
                transaction.ExpiresAt <= now &&
                (boothId == null || transaction.BoothId == boothId))
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

    public async Task<PayMongoWebhookApplyResult> ApplyPayMongoWebhookAsync(
        PayMongoWebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhookEvent.PaymentIntentId))
        {
            return PayMongoWebhookApplyResult.Ignored("PayMongo event has no payment intent reference.");
        }

        var attempt = await dbContext.PaymentAttempts
            .Include(item => item.Transaction)
            .Where(item =>
                item.Provider == StatusValues.PaymentProvider.PayMongo &&
                item.ProviderReference == webhookEvent.PaymentIntentId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (attempt?.Transaction is null)
        {
            return PayMongoWebhookApplyResult.Ignored("No PhotoBIZ payment attempt matched the PayMongo reference.");
        }

        var transaction = attempt.Transaction;
        if (webhookEvent.AmountCents.HasValue && webhookEvent.AmountCents.Value != transaction.AmountCents)
        {
            return PayMongoWebhookApplyResult.Invalid("PayMongo amount does not match the PhotoBIZ transaction.");
        }

        if (!string.IsNullOrWhiteSpace(webhookEvent.Currency) &&
            !string.Equals(webhookEvent.Currency, transaction.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return PayMongoWebhookApplyResult.Invalid("PayMongo currency does not match the PhotoBIZ transaction.");
        }

        attempt.RawPayload = webhookEvent.RawPayload;
        attempt.Status = webhookEvent.EventType switch
        {
            "payment.paid" => StatusValues.Transaction.Paid,
            "payment.failed" => StatusValues.Transaction.PaymentFailed,
            "qrph.expired" => StatusValues.Transaction.Expired,
            _ => attempt.Status
        };

        if (webhookEvent.EventType == "payment.paid")
        {
            if (transaction.Status == StatusValues.Transaction.PendingPayMongoQrPh)
            {
                transaction.Status = StatusValues.Transaction.Paid;
                transaction.PaidAt ??= DateTimeOffset.UtcNow;

                var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
                booth.CurrentState = StatusValues.Booth.Paid;
            }
            else if (transaction.Status is StatusValues.Transaction.Cancelled or StatusValues.Transaction.Expired or StatusValues.Transaction.PaymentFailed)
            {
                transaction.FailureReason ??= "PayMongo reported payment after the PhotoBIZ transaction was already terminal; manual reconciliation is required.";
            }
        }
        else if (webhookEvent.EventType is "payment.failed" or "qrph.expired" &&
            transaction.Status == StatusValues.Transaction.PendingPayMongoQrPh)
        {
            transaction.Status = webhookEvent.EventType == "qrph.expired"
                ? StatusValues.Transaction.Expired
                : StatusValues.Transaction.PaymentFailed;
            transaction.FailureReason = webhookEvent.EventType == "qrph.expired"
                ? "PayMongo QR Ph expired before payment was completed."
                : "PayMongo reported that the QR Ph payment failed.";

            var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
            booth.CurrentState = StatusValues.Booth.Welcome;
        }
        else if (webhookEvent.EventType is not ("payment.paid" or "payment.failed" or "qrph.expired"))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return PayMongoWebhookApplyResult.Ignored("PayMongo event type is not used by PhotoBIZ.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteSystemAsync(
            transaction.ClientAccountId,
            "transaction.paymongo_webhook",
            nameof(Transaction),
            transaction.Id,
            new
            {
                webhookEvent.EventId,
                webhookEvent.EventType,
                webhookEvent.PaymentIntentId,
                transaction.TransactionNumber,
                transaction.Status
            },
            cancellationToken);

        return PayMongoWebhookApplyResult.Applied();
    }

    public async Task<PhotoBizAgentCommand?> TryAcquireNextAgentCommandAsync(
        Booth booth,
        CancellationToken cancellationToken)
    {
        var gate = await PhotoBizRuntimeAvailability.CheckBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: false,
            cancellationToken);

        if (!gate.Succeeded)
        {
            return null;
        }

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

        var now = DateTimeOffset.UtcNow;
        transaction.Status = StatusValues.Transaction.Completed;
        transaction.CompletedAt = now;

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
        booth.CurrentState = StatusValues.Booth.Completed;

        if (transaction.TransactionType == StatusValues.TransactionType.CoveredPlanSession &&
            transaction.BoothOfferActivationId.HasValue)
        {
            var activation = await dbContext.BoothOfferActivations
                .Include(item => item.BoothOffer)
                .SingleAsync(item => item.Id == transaction.BoothOfferActivationId, cancellationToken);

            if (activation.BoothOffer?.OfferType == StatusValues.OfferType.SessionCount)
            {
                activation.SessionsUsed += 1;
                if (activation.SessionAllowance.HasValue &&
                    activation.SessionsUsed >= activation.SessionAllowance.Value)
                {
                    activation.Status = StatusValues.OfferActivation.Completed;
                    activation.DeactivatedAt = now;
                }
            }
        }

        var session = await dbContext.BoothSessions
            .OrderByDescending(item => item.Id)
            .FirstAsync(item => item.TransactionId == transactionId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(lumaboothSessionRef))
        {
            session.LumaboothSessionRef = lumaboothSessionRef.Trim();
        }

        session.Status = StatusValues.Session.Completed;
        session.EndedAt = now;

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

        var resetCount = 0;
        foreach (var booth in booths)
        {
            var result = await ReturnCompletedBoothToWelcomeAsync(booth, cutoff, cancellationToken);
            if (result.Succeeded)
            {
                resetCount += 1;
            }
        }

        return resetCount;
    }

    public async Task<int> ResetTimedOutPrintingBoothsToWelcomeAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.Subtract(PrintingOrSharingTimeout);
        var timedOutTransactions = await dbContext.Transactions
            .Where(transaction =>
                transaction.TransactionType == StatusValues.TransactionType.ExtraPrintAddOn &&
                transaction.Status == StatusValues.Transaction.StartingSession &&
                (transaction.PaidAt ?? transaction.CreatedAt) <= cutoff)
            .ToListAsync(cancellationToken);

        if (timedOutTransactions.Count == 0)
        {
            return 0;
        }

        var boothIds = timedOutTransactions.Select(transaction => transaction.BoothId).Distinct().ToArray();
        var booths = await dbContext.Booths
            .Where(booth => boothIds.Contains(booth.Id))
            .ToDictionaryAsync(booth => booth.Id, cancellationToken);
        var resetCount = 0;

        foreach (var transaction in timedOutTransactions)
        {
            var previousStatus = transaction.Status;
            transaction.Status = StatusValues.Transaction.Cancelled;
            transaction.CancelledAt ??= now;
            ApplyCancellationContext(
                transaction,
                StatusValues.CancellationActor.System,
                cancelledByUserId: null,
                StatusValues.CancellationSource.SystemExtraPrintTimeout,
                previousStatus);
            transaction.FailureReason ??= "Extra print workflow timed out before completion.";

            if (booths.TryGetValue(transaction.BoothId, out var booth) &&
                booth.CurrentState == StatusValues.Booth.PrintingOrSharing)
            {
                booth.CurrentState = StatusValues.Booth.Welcome;
                resetCount += 1;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return resetCount;
    }

    public async Task<ReturnCompletedBoothToWelcomeResult> ReturnCompletedBoothToWelcomeAsync(
        Booth booth,
        DateTimeOffset? completedAtCutoff,
        CancellationToken cancellationToken)
    {
        if (booth.CurrentState == StatusValues.Booth.Welcome)
        {
            return ReturnCompletedBoothToWelcomeResult.Success(ReturnCompletedBoothToWelcomeStatus.AlreadyWelcome);
        }

        if (booth.CurrentState != StatusValues.Booth.Completed)
        {
            return ReturnCompletedBoothToWelcomeResult.Failure(ReturnCompletedBoothToWelcomeStatus.NotCompleted);
        }

        var latestCompletedTransaction = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.BoothId == booth.Id &&
                (transaction.TransactionType == StatusValues.TransactionType.SessionPurchase ||
                    transaction.TransactionType == StatusValues.TransactionType.CoveredPlanSession) &&
                transaction.Status == StatusValues.Transaction.Completed)
            .OrderByDescending(transaction => transaction.CompletedAt ?? transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestCompletedTransaction is null)
        {
            return ReturnCompletedBoothToWelcomeResult.Failure(ReturnCompletedBoothToWelcomeStatus.NoCompletedSession);
        }

        var completedAt = latestCompletedTransaction.CompletedAt ?? latestCompletedTransaction.CreatedAt;
        if (completedAtCutoff.HasValue && completedAt > completedAtCutoff.Value)
        {
            return ReturnCompletedBoothToWelcomeResult.Failure(ReturnCompletedBoothToWelcomeStatus.NotReady);
        }

        var hasNewerActiveTransaction = await dbContext.Transactions
            .AsNoTracking()
            .AnyAsync(
                transaction =>
                    transaction.BoothId == booth.Id &&
                    transaction.Id != latestCompletedTransaction.Id &&
                    transaction.CreatedAt > completedAt &&
                    ActiveTransactionStatusesDuringCompletedPrompt.Contains(transaction.Status),
                cancellationToken);

        if (hasNewerActiveTransaction)
        {
            return ReturnCompletedBoothToWelcomeResult.Failure(ReturnCompletedBoothToWelcomeStatus.ActiveTransaction);
        }

        booth.CurrentState = StatusValues.Booth.Welcome;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ReturnCompletedBoothToWelcomeResult.Success(ReturnCompletedBoothToWelcomeStatus.Returned);
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

    private static void ApplyCancellationContext(
        Transaction transaction,
        string actorType,
        Guid? cancelledByUserId,
        string source,
        string previousStatus)
    {
        transaction.CancelledByActorType = actorType;
        transaction.CancelledByUserId = cancelledByUserId;
        transaction.CancellationSource = source;
        transaction.CancellationPreviousStatus = previousStatus;
    }

    private static string ResolveKioskCancellationSource(string previousStatus, string trigger)
    {
        return (previousStatus, trigger) switch
        {
            (StatusValues.Transaction.Created, StatusValues.BoothUiCancelTrigger.BackButton) =>
                StatusValues.CancellationSource.BoothUiPaymentOptionsBack,
            (StatusValues.Transaction.Created, StatusValues.BoothUiCancelTrigger.IdleTimeout) =>
                StatusValues.CancellationSource.BoothUiPaymentOptionsIdleTimeout,
            (StatusValues.Transaction.PendingCash, StatusValues.BoothUiCancelTrigger.BackButton) =>
                StatusValues.CancellationSource.BoothUiWaitingForPaymentBack,
            (StatusValues.Transaction.PendingPayMongoQrPh, StatusValues.BoothUiCancelTrigger.BackButton) =>
                StatusValues.CancellationSource.BoothUiWaitingForPaymentBack,
            _ => throw new InvalidOperationException("Cancellation trigger is not valid for this transaction state.")
        };
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

    private async Task<Guid?> ResolveExtraPrintReferenceTransactionIdAsync(
        Guid boothId,
        CancellationToken cancellationToken)
    {
        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.BoothId == boothId)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Select(transaction => new
            {
                transaction.Id,
                transaction.TransactionType,
                transaction.Status
            })
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            if (IsTransactionInLumaboothSession(transaction.Status))
            {
                return null;
            }

            if (IsTransactionBeforeLumaboothSession(transaction.TransactionType, transaction.Status))
            {
                continue;
            }

            if (!IsSessionTransaction(transaction.TransactionType))
            {
                if (!TerminalTransactionStatuses.Contains(transaction.Status))
                {
                    return null;
                }

                continue;
            }

            return transaction.Id;
        }

        return null;
    }

    private static bool IsTransactionBeforeLumaboothSession(string transactionType, string status)
    {
        return IsSessionTransaction(transactionType) &&
            status is StatusValues.Transaction.Created
                or StatusValues.Transaction.PendingCash
                or StatusValues.Transaction.PendingPayMongoQrPh
                or StatusValues.Transaction.Paid;
    }

    private static bool IsSessionTransaction(string transactionType)
    {
        return transactionType is StatusValues.TransactionType.SessionPurchase
            or StatusValues.TransactionType.CoveredPlanSession;
    }

    private static bool IsTransactionInLumaboothSession(string status)
    {
        return status is StatusValues.Transaction.StartingSession or StatusValues.Transaction.InSession;
    }

    private static bool IsBoothInLumaboothSession(Booth booth)
    {
        return booth.CurrentState is StatusValues.Booth.StartingLumabooth
            or StatusValues.Booth.InLumaboothSession
            or StatusValues.Booth.PrintingOrSharing;
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

    private static string SerializeOfferSnapshot(BoothOffer offer)
    {
        return JsonSerializer.Serialize(new
        {
            offer.Id,
            offer.Name,
            offer.OfferType,
            offer.PriceCents,
            offer.Currency,
            offer.IncludedPrintEntitlement,
            offer.AllowsExtraPrintAddOn,
            offer.ExtraPrintPriceCents,
            offer.LumaboothSessionMode,
            offer.DurationHours,
            offer.SessionAllowance
        });
    }
}

public enum ReturnCompletedBoothToWelcomeStatus
{
    Returned,
    AlreadyWelcome,
    NotCompleted,
    NoCompletedSession,
    NotReady,
    ActiveTransaction
}

public sealed record ReturnCompletedBoothToWelcomeResult(
    bool Succeeded,
    ReturnCompletedBoothToWelcomeStatus Status)
{
    public static ReturnCompletedBoothToWelcomeResult Success(ReturnCompletedBoothToWelcomeStatus status)
    {
        return new ReturnCompletedBoothToWelcomeResult(true, status);
    }

    public static ReturnCompletedBoothToWelcomeResult Failure(ReturnCompletedBoothToWelcomeStatus status)
    {
        return new ReturnCompletedBoothToWelcomeResult(false, status);
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

public sealed record PayMongoWebhookApplyResult(
    bool Succeeded,
    bool ShouldAcknowledge,
    string? Message)
{
    public static PayMongoWebhookApplyResult Applied()
    {
        return new PayMongoWebhookApplyResult(true, true, null);
    }

    public static PayMongoWebhookApplyResult Ignored(string message)
    {
        return new PayMongoWebhookApplyResult(false, true, message);
    }

    public static PayMongoWebhookApplyResult Invalid(string message)
    {
        return new PayMongoWebhookApplyResult(false, false, message);
    }
}
