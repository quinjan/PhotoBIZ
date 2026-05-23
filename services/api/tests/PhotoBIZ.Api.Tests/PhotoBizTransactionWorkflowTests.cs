using Microsoft.EntityFrameworkCore;
using PhotoBIZ.Api;
using PhotoBIZ.Api.Data;

namespace PhotoBIZ.Api.Tests;

public sealed class PhotoBizTransactionWorkflowTests
{
    [Fact]
    public async Task CashWorkflowAdvancesThroughPaidAndCompletedStates()
    {
        await using var dbContext = CreateDbContext();
        var auditService = new PhotoBizAuditService(dbContext);
        var workflow = new PhotoBizTransactionWorkflow(dbContext, auditService);
        var (booth, activation, offer) = await SeedBoothGraphAsync(dbContext);

        var transaction = await workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None);
        transaction = await workflow.SetPaymentMethodAsync(transaction, booth, StatusValues.PaymentMethod.Cash, CancellationToken.None);

        var cashier = new PhotoBizCurrentUser(Guid.NewGuid(), StatusValues.User.Cashier, booth.ClientAccountId, booth.Id);
        transaction = await workflow.ApproveCashAsync(transaction, cashier, CancellationToken.None);

        Assert.Equal(StatusValues.Transaction.Paid, transaction.Status);
        Assert.Equal(StatusValues.Booth.Paid, booth.CurrentState);

        var command = await workflow.TryAcquireNextAgentCommandAsync(booth, CancellationToken.None);

        Assert.NotNull(command);
        Assert.Equal(StatusValues.Transaction.StartingSession, transaction.Status);

        await workflow.MarkSessionStartedAsync(transaction.Id, booth.Id, "PBZ-test", CancellationToken.None);
        await workflow.MarkSessionCompletedAsync(transaction.Id, booth.Id, "PBZ-test", CancellationToken.None);

        Assert.Equal(StatusValues.Transaction.Completed, transaction.Status);
        Assert.Equal(StatusValues.Booth.Completed, booth.CurrentState);

        transaction.CompletedAt = DateTimeOffset.UtcNow.Subtract(PhotoBizTransactionWorkflow.PostSessionPromptDuration).AddSeconds(-1);
        await dbContext.SaveChangesAsync();
        await workflow.ResetCompletedBoothsToWelcomeAsync(CancellationToken.None);

        Assert.Equal(StatusValues.Booth.Welcome, booth.CurrentState);
    }

    [Fact]
    public async Task ExtraPrintAddOnApprovesAndCommandsPrintCopiesWithoutCreatingBoothSession()
    {
        await using var dbContext = CreateDbContext();
        var auditService = new PhotoBizAuditService(dbContext);
        var workflow = new PhotoBizTransactionWorkflow(dbContext, auditService);
        var (booth, activation, offer) = await SeedBoothGraphAsync(dbContext);
        var cashier = new PhotoBizCurrentUser(Guid.NewGuid(), StatusValues.User.Cashier, booth.ClientAccountId, booth.Id);

        var parent = await workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None);
        parent = await workflow.SetPaymentMethodAsync(parent, booth, StatusValues.PaymentMethod.Cash, CancellationToken.None);
        parent = await workflow.ApproveCashAsync(parent, cashier, CancellationToken.None);
        _ = await workflow.TryAcquireNextAgentCommandAsync(booth, CancellationToken.None);
        await workflow.MarkSessionStartedAsync(parent.Id, booth.Id, "PBZ-parent", CancellationToken.None);
        await workflow.MarkSessionCompletedAsync(parent.Id, booth.Id, "PBZ-parent", CancellationToken.None);

        var addOn = await workflow.CreateExtraPrintAddOnAsync(parent, cashier, 3, CancellationToken.None);
        addOn = await workflow.ApproveCashAsync(addOn, cashier, CancellationToken.None);
        var command = await workflow.TryAcquireNextAgentCommandAsync(booth, CancellationToken.None);

        Assert.NotNull(command);
        Assert.Equal(StatusValues.TransactionType.ExtraPrintAddOn, command.TransactionType);
        Assert.Equal(3, command.ExtraPrintCount);
        Assert.Equal(15000, addOn.AmountCents);
        Assert.Equal(StatusValues.Booth.PrintingOrSharing, booth.CurrentState);
        Assert.Single(await dbContext.BoothSessions.Where(item => item.TransactionId == parent.Id).ToListAsync());
        Assert.Empty(await dbContext.BoothSessions.Where(item => item.TransactionId == addOn.Id).ToListAsync());

        await workflow.MarkPrintCompletedAsync(addOn.Id, booth.Id, CancellationToken.None);

        Assert.Equal(StatusValues.Transaction.Completed, addOn.Status);
        Assert.Equal(StatusValues.Booth.Welcome, booth.CurrentState);
    }

    [Fact]
    public async Task TimedOutExtraPrintAddOnReturnsBoothToWelcome()
    {
        await using var dbContext = CreateDbContext();
        var auditService = new PhotoBizAuditService(dbContext);
        var workflow = new PhotoBizTransactionWorkflow(dbContext, auditService);
        var (booth, activation, offer) = await SeedBoothGraphAsync(dbContext);
        var cashier = new PhotoBizCurrentUser(Guid.NewGuid(), StatusValues.User.Cashier, booth.ClientAccountId, booth.Id);

        var parent = await workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None);
        parent = await workflow.SetPaymentMethodAsync(parent, booth, StatusValues.PaymentMethod.Cash, CancellationToken.None);
        parent = await workflow.ApproveCashAsync(parent, cashier, CancellationToken.None);
        _ = await workflow.TryAcquireNextAgentCommandAsync(booth, CancellationToken.None);
        await workflow.MarkSessionStartedAsync(parent.Id, booth.Id, "PBZ-parent", CancellationToken.None);
        await workflow.MarkSessionCompletedAsync(parent.Id, booth.Id, "PBZ-parent", CancellationToken.None);

        var addOn = await workflow.CreateExtraPrintAddOnAsync(parent, cashier, 3, CancellationToken.None);
        addOn = await workflow.ApproveCashAsync(addOn, cashier, CancellationToken.None);
        _ = await workflow.TryAcquireNextAgentCommandAsync(booth, CancellationToken.None);
        addOn.PaidAt = DateTimeOffset.UtcNow.Subtract(PhotoBizTransactionWorkflow.PrintingOrSharingTimeout).AddSeconds(-1);
        await dbContext.SaveChangesAsync();

        var resetCount = await workflow.ResetTimedOutPrintingBoothsToWelcomeAsync(CancellationToken.None);

        Assert.Equal(1, resetCount);
        Assert.Equal(StatusValues.Transaction.Cancelled, addOn.Status);
        Assert.Equal("Extra print workflow timed out before completion.", addOn.FailureReason);
        Assert.Equal(StatusValues.CancellationActor.System, addOn.CancelledByActorType);
        Assert.Null(addOn.CancelledByUserId);
        Assert.Equal(StatusValues.CancellationSource.SystemExtraPrintTimeout, addOn.CancellationSource);
        Assert.Equal(StatusValues.Transaction.StartingSession, addOn.CancellationPreviousStatus);
        Assert.Equal(StatusValues.Booth.Welcome, booth.CurrentState);
    }

    [Fact]
    public async Task ExtraPrintAddOnRejectsInvalidEligibility()
    {
        await using var dbContext = CreateDbContext();
        var auditService = new PhotoBizAuditService(dbContext);
        var workflow = new PhotoBizTransactionWorkflow(dbContext, auditService);
        var (booth, activation, offer) = await SeedBoothGraphAsync(dbContext);
        var cashier = new PhotoBizCurrentUser(Guid.NewGuid(), StatusValues.User.Cashier, booth.ClientAccountId, booth.Id);

        var parent = await workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None);
        parent = await workflow.SetPaymentMethodAsync(parent, booth, StatusValues.PaymentMethod.Cash, CancellationToken.None);
        parent = await workflow.ApproveCashAsync(parent, cashier, CancellationToken.None);
        _ = await workflow.TryAcquireNextAgentCommandAsync(booth, CancellationToken.None);
        await workflow.MarkSessionStartedAsync(parent.Id, booth.Id, "PBZ-parent", CancellationToken.None);
        await workflow.MarkSessionCompletedAsync(parent.Id, booth.Id, "PBZ-parent", CancellationToken.None);

        var lowCopyCount = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.CreateExtraPrintAddOnAsync(parent, cashier, 0, CancellationToken.None));
        var highCopyCount = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.CreateExtraPrintAddOnAsync(parent, cashier, 6, CancellationToken.None));

        Assert.Equal("Extra print copy count must be between 1 and 5.", lowCopyCount.Message);
        Assert.Equal("Extra print copy count must be between 1 and 5.", highCopyCount.Message);

        var addOn = await workflow.CreateExtraPrintAddOnAsync(parent, cashier, 1, CancellationToken.None);
        var activeTransaction = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.CreateExtraPrintAddOnAsync(parent, cashier, 1, CancellationToken.None));

        Assert.Equal(StatusValues.Transaction.PendingCash, addOn.Status);
        Assert.Equal("Extra prints are available only for the previous booth transaction.", activeTransaction.Message);
    }

    [Fact]
    public async Task CompletedBoothResetUsesLatestCompletedSession()
    {
        await using var dbContext = CreateDbContext();
        var auditService = new PhotoBizAuditService(dbContext);
        var workflow = new PhotoBizTransactionWorkflow(dbContext, auditService);
        var (booth, activation, offer) = await SeedBoothGraphAsync(dbContext);
        var cashier = new PhotoBizCurrentUser(Guid.NewGuid(), StatusValues.User.Cashier, booth.ClientAccountId, booth.Id);

        var older = await workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None);
        older = await workflow.SetPaymentMethodAsync(older, booth, StatusValues.PaymentMethod.Cash, CancellationToken.None);
        older = await workflow.ApproveCashAsync(older, cashier, CancellationToken.None);
        _ = await workflow.TryAcquireNextAgentCommandAsync(booth, CancellationToken.None);
        await workflow.MarkSessionStartedAsync(older.Id, booth.Id, "PBZ-older", CancellationToken.None);
        await workflow.MarkSessionCompletedAsync(older.Id, booth.Id, "PBZ-older", CancellationToken.None);
        older.CompletedAt = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(5));
        booth.CurrentState = StatusValues.Booth.Welcome;
        await dbContext.SaveChangesAsync();

        var latest = await workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None);
        latest = await workflow.SetPaymentMethodAsync(latest, booth, StatusValues.PaymentMethod.Cash, CancellationToken.None);
        latest = await workflow.ApproveCashAsync(latest, cashier, CancellationToken.None);
        _ = await workflow.TryAcquireNextAgentCommandAsync(booth, CancellationToken.None);
        await workflow.MarkSessionStartedAsync(latest.Id, booth.Id, "PBZ-latest", CancellationToken.None);
        await workflow.MarkSessionCompletedAsync(latest.Id, booth.Id, "PBZ-latest", CancellationToken.None);

        var resetCount = await workflow.ResetCompletedBoothsToWelcomeAsync(CancellationToken.None);

        Assert.Equal(0, resetCount);
        Assert.Equal(StatusValues.Booth.Completed, booth.CurrentState);
    }

    [Fact]
    public async Task CompletedBoothResetSkipsWhenNewerActiveTransactionExists()
    {
        await using var dbContext = CreateDbContext();
        var auditService = new PhotoBizAuditService(dbContext);
        var workflow = new PhotoBizTransactionWorkflow(dbContext, auditService);
        var (booth, activation, offer) = await SeedBoothGraphAsync(dbContext);
        var cashier = new PhotoBizCurrentUser(Guid.NewGuid(), StatusValues.User.Cashier, booth.ClientAccountId, booth.Id);

        var parent = await workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None);
        parent = await workflow.SetPaymentMethodAsync(parent, booth, StatusValues.PaymentMethod.Cash, CancellationToken.None);
        parent = await workflow.ApproveCashAsync(parent, cashier, CancellationToken.None);
        _ = await workflow.TryAcquireNextAgentCommandAsync(booth, CancellationToken.None);
        await workflow.MarkSessionStartedAsync(parent.Id, booth.Id, "PBZ-parent", CancellationToken.None);
        await workflow.MarkSessionCompletedAsync(parent.Id, booth.Id, "PBZ-parent", CancellationToken.None);
        parent.CompletedAt = DateTimeOffset.UtcNow.Subtract(PhotoBizTransactionWorkflow.PostSessionPromptDuration).AddSeconds(-1);
        await workflow.CreateExtraPrintAddOnAsync(parent, cashier, 1, CancellationToken.None);
        booth.CurrentState = StatusValues.Booth.Completed;
        await dbContext.SaveChangesAsync();

        var resetCount = await workflow.ResetCompletedBoothsToWelcomeAsync(CancellationToken.None);

        Assert.Equal(0, resetCount);
        Assert.Equal(StatusValues.Booth.Completed, booth.CurrentState);
    }

    [Fact]
    public async Task CreateTransactionRejectsBoothWithoutHeartbeat()
    {
        await using var dbContext = CreateDbContext();
        var auditService = new PhotoBizAuditService(dbContext);
        var workflow = new PhotoBizTransactionWorkflow(dbContext, auditService);
        var (booth, activation, offer) = await SeedBoothGraphAsync(dbContext);
        booth.LastHeartbeatAt = null;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None));

        Assert.Equal("The booth agent is offline.", exception.Message);
    }

    [Fact]
    public async Task CreateTransactionRejectsBoothWithStaleHeartbeat()
    {
        await using var dbContext = CreateDbContext();
        var auditService = new PhotoBizAuditService(dbContext);
        var workflow = new PhotoBizTransactionWorkflow(dbContext, auditService);
        var (booth, activation, offer) = await SeedBoothGraphAsync(dbContext);
        booth.LastHeartbeatAt = DateTimeOffset.UtcNow.Subtract(PhotoBizBoothAvailability.AgentOfflineAfter).AddSeconds(-1);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None));

        Assert.Equal("The booth agent is offline.", exception.Message);
    }

    [Fact]
    public async Task CreateTransactionRejectsInactiveParentGates()
    {
        await using var dbContext = CreateDbContext();
        var auditService = new PhotoBizAuditService(dbContext);
        var workflow = new PhotoBizTransactionWorkflow(dbContext, auditService);
        var (booth, activation, offer) = await SeedBoothGraphAsync(dbContext);

        booth.Status = StatusValues.Booth.Inactive;
        var inactiveBooth = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None));

        booth.Status = StatusValues.Booth.Active;
        var location = await dbContext.Locations.SingleAsync(item => item.Id == booth.LocationId);
        location.Status = StatusValues.Booth.Inactive;
        await dbContext.SaveChangesAsync();
        var inactiveLocation = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None));

        location.Status = StatusValues.Booth.Active;
        var client = await dbContext.ClientAccounts.SingleAsync(item => item.Id == booth.ClientAccountId);
        client.Status = StatusValues.ClientAccount.Suspended;
        await dbContext.SaveChangesAsync();
        var inactiveClient = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None));

        Assert.Equal("Booth is inactive.", inactiveBooth.Message);
        Assert.Equal("Booth location is inactive.", inactiveLocation.Message);
        Assert.Equal("Client account is not active.", inactiveClient.Message);
    }

    [Fact]
    public async Task CreateTransactionRejectsSecondActiveTransactionForSameBooth()
    {
        await using var dbContext = CreateDbContext();
        var auditService = new PhotoBizAuditService(dbContext);
        var workflow = new PhotoBizTransactionWorkflow(dbContext, auditService);
        var (booth, activation, offer) = await SeedBoothGraphAsync(dbContext);

        _ = await workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None));

        Assert.Equal("The booth already has an active transaction.", exception.Message);
    }

    [Fact]
    public async Task SetPaymentMethodRejectsWhenAnotherActiveTransactionExists()
    {
        await using var dbContext = CreateDbContext();
        var auditService = new PhotoBizAuditService(dbContext);
        var workflow = new PhotoBizTransactionWorkflow(dbContext, auditService);
        var (booth, activation, offer) = await SeedBoothGraphAsync(dbContext);

        var firstTransaction = await workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None);
        firstTransaction = await workflow.SetPaymentMethodAsync(firstTransaction, booth, StatusValues.PaymentMethod.Cash, CancellationToken.None);

        var secondTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            ClientAccountId = booth.ClientAccountId,
            LocationId = booth.LocationId,
            BoothId = booth.Id,
            BoothOfferId = offer.Id,
            BoothOfferActivationId = activation.Id,
            TransactionNumber = "TXN-test-second",
            TransactionType = StatusValues.TransactionType.SessionPurchase,
            PaymentMethod = StatusValues.PaymentMethod.Cash,
            Status = StatusValues.Transaction.Created,
            AmountCents = offer.PriceCents,
            Currency = offer.Currency,
            OfferSnapshot = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        dbContext.Transactions.Add(secondTransaction);
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.SetPaymentMethodAsync(secondTransaction, booth, StatusValues.PaymentMethod.Cash, CancellationToken.None));

        Assert.Equal("The booth already has an active transaction.", exception.Message);
        Assert.Equal(StatusValues.Transaction.PendingCash, firstTransaction.Status);
        Assert.Equal(StatusValues.Transaction.Created, secondTransaction.Status);
    }

    [Fact]
    public async Task ApproveCashRejectsOfflineBooth()
    {
        await using var dbContext = CreateDbContext();
        var auditService = new PhotoBizAuditService(dbContext);
        var workflow = new PhotoBizTransactionWorkflow(dbContext, auditService);
        var (booth, activation, offer) = await SeedBoothGraphAsync(dbContext);
        var transaction = await workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None);
        transaction = await workflow.SetPaymentMethodAsync(transaction, booth, StatusValues.PaymentMethod.Cash, CancellationToken.None);
        booth.LastHeartbeatAt = DateTimeOffset.UtcNow.Subtract(PhotoBizBoothAvailability.AgentOfflineAfter).AddSeconds(-1);
        var cashier = new PhotoBizCurrentUser(Guid.NewGuid(), StatusValues.User.Cashier, booth.ClientAccountId, booth.Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.ApproveCashAsync(transaction, cashier, CancellationToken.None));

        Assert.Equal("The booth agent is offline.", exception.Message);
        Assert.Equal(StatusValues.Transaction.PendingCash, transaction.Status);
    }

    [Fact]
    public async Task ReturnBoothToWelcomeCancelsActiveTransactionAndFailsLatestSession()
    {
        await using var dbContext = CreateDbContext();
        var auditService = new PhotoBizAuditService(dbContext);
        var workflow = new PhotoBizTransactionWorkflow(dbContext, auditService);
        var (booth, activation, offer) = await SeedBoothGraphAsync(dbContext);
        var cashier = new PhotoBizCurrentUser(Guid.NewGuid(), StatusValues.User.Cashier, booth.ClientAccountId, booth.Id);

        var transaction = await workflow.CreateTransactionAsync(booth, activation, offer, CancellationToken.None);
        transaction = await workflow.SetPaymentMethodAsync(transaction, booth, StatusValues.PaymentMethod.Cash, CancellationToken.None);
        transaction = await workflow.ApproveCashAsync(transaction, cashier, CancellationToken.None);
        _ = await workflow.TryAcquireNextAgentCommandAsync(booth, CancellationToken.None);

        await workflow.ReturnBoothToWelcomeAsync(booth, cashier, CancellationToken.None);

        var session = await dbContext.BoothSessions.SingleAsync(item => item.TransactionId == transaction.Id);

        Assert.Equal(StatusValues.Transaction.Cancelled, transaction.Status);
        Assert.NotNull(transaction.CancelledAt);
        Assert.Equal("Manual booth recovery returned the booth to welcome.", transaction.FailureReason);
        Assert.Equal(StatusValues.CancellationActor.Cashier, transaction.CancelledByActorType);
        Assert.Equal(cashier.UserId, transaction.CancelledByUserId);
        Assert.Equal(StatusValues.CancellationSource.CashierPosReturnToWelcome, transaction.CancellationSource);
        Assert.Equal(StatusValues.Transaction.StartingSession, transaction.CancellationPreviousStatus);
        Assert.Equal(StatusValues.Session.Failed, session.Status);
        Assert.NotNull(session.EndedAt);
        Assert.Equal(StatusValues.Booth.Welcome, booth.CurrentState);
    }

    [Fact]
    public void EffectiveStateReturnsOfflineForStaleHeartbeat()
    {
        var booth = new Booth
        {
            Id = Guid.NewGuid(),
            CurrentState = StatusValues.Booth.Welcome,
            LastHeartbeatAt = DateTimeOffset.UtcNow.Subtract(PhotoBizBoothAvailability.AgentOfflineAfter).AddSeconds(-1)
        };

        var state = PhotoBizBoothAvailability.GetEffectiveState(booth, DateTimeOffset.UtcNow);

        Assert.Equal(StatusValues.Booth.Offline, state);
    }

    [Fact]
    public void EffectiveStateKeepsStoredStateForFreshHeartbeat()
    {
        var booth = new Booth
        {
            Id = Guid.NewGuid(),
            CurrentState = StatusValues.Booth.PaymentPending,
            LastHeartbeatAt = DateTimeOffset.UtcNow
        };

        var state = PhotoBizBoothAvailability.GetEffectiveState(booth, DateTimeOffset.UtcNow);

        Assert.Equal(StatusValues.Booth.PaymentPending, state);
    }

    [Fact]
    public void MarkAgentHeartbeatRestoresOfflineBoothToWelcome()
    {
        var booth = new Booth
        {
            Id = Guid.NewGuid(),
            CurrentState = StatusValues.Booth.Offline
        };

        PhotoBizBoothAvailability.MarkAgentHeartbeat(booth, DateTimeOffset.UtcNow);

        Assert.Equal(StatusValues.Booth.Welcome, booth.CurrentState);
        Assert.NotNull(booth.LastHeartbeatAt);
    }

    private static async Task<(Booth Booth, BoothOfferActivation Activation, BoothOffer Offer)> SeedBoothGraphAsync(PhotoBizDbContext dbContext)
    {
        var clientId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var boothId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var activationId = Guid.NewGuid();

        dbContext.ClientAccounts.Add(new ClientAccount
        {
            Id = clientId,
            Name = "The Memory Box",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = planId,
            Name = $"Plan {planId:N}",
            PricePerBoothCents = 990000,
            Currency = "PHP",
            Active = true
        });
        dbContext.ClientSubscriptions.Add(new ClientSubscription
        {
            Id = subscriptionId,
            ClientAccountId = clientId,
            SubscriptionPlanId = planId,
            Status = StatusValues.Subscription.Active,
            ActiveBoothAllowance = 1,
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        dbContext.Locations.Add(new Location
        {
            Id = locationId,
            ClientAccountId = clientId,
            Name = "SM Manila"
        });

        var booth = new Booth
        {
            Id = boothId,
            ClientAccountId = clientId,
            LocationId = locationId,
            Name = "Booth A",
            Code = "SMA-001",
            Status = StatusValues.Booth.Active,
            CurrentState = StatusValues.Booth.Welcome,
            LastHeartbeatAt = DateTimeOffset.UtcNow
        };

        var offer = new BoothOffer
        {
            Id = offerId,
            ClientAccountId = clientId,
            Name = "Per Session",
            OfferType = StatusValues.OfferType.PerSession,
            PriceCents = 25000,
            Currency = "PHP",
            IncludedPrintEntitlement = StatusValues.PrintEntitlement.TwoBySixOrOneByFour,
            AllowsExtraPrintAddOn = true,
            ExtraPrintPriceCents = 5000,
            LumaboothSessionMode = "SESSION_STANDARD",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var activation = new BoothOfferActivation
        {
            Id = activationId,
            BoothId = boothId,
            BoothOfferId = offerId,
            Status = StatusValues.OfferActivation.Active,
            ActivatedAt = DateTimeOffset.UtcNow,
            BoothOffer = offer
        };

        dbContext.Booths.Add(booth);
        dbContext.BoothOffers.Add(offer);
        dbContext.BoothOfferActivations.Add(activation);
        dbContext.BoothPaymentOptionAssignments.Add(new BoothPaymentOptionAssignment
        {
            Id = Guid.NewGuid(),
            BoothId = boothId,
            PaymentMethod = StatusValues.PaymentMethod.Cash,
            RuntimeEnabled = true,
            Status = StatusValues.PaymentAssignment.Assigned,
            AssignedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();

        return (booth, activation, offer);
    }

    private static PhotoBizDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PhotoBizDbContext>()
            .UseInMemoryDatabase($"photobiz-tests-{Guid.NewGuid()}")
            .Options;

        return new PhotoBizDbContext(options);
    }
}
