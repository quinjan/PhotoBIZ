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

        await workflow.MarkSessionStartedAsync(transaction.Id, CancellationToken.None);
        await workflow.MarkSessionCompletedAsync(transaction.Id, CancellationToken.None);

        Assert.Equal(StatusValues.Transaction.Completed, transaction.Status);
        Assert.Equal(StatusValues.Booth.Welcome, booth.CurrentState);
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
