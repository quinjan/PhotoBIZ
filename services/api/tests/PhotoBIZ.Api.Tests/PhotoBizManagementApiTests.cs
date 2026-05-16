using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhotoBIZ.Api;
using PhotoBIZ.Api.Data;

namespace PhotoBIZ.Api.Tests;

public sealed class PhotoBizManagementApiTests
{
    [Fact]
    public async Task CreateBoothRejectsWhenSubscriptionAllowanceIsReached()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1);
        await LoginAsync(client, seed.ClientOwnerEmail);

        var response = await client.PostAsJsonAsync("/api/admin/booths", new
        {
            clientAccountId = seed.ClientAccountId,
            locationId = seed.LocationId,
            name = "Booth B",
            code = "SMA-002"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Client active booth allowance has been reached.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActivateOfferRejectsSuspendedSubscription()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Suspended,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true);
        await LoginAsync(client, seed.ClientOwnerEmail);

        var response = await client.PostAsJsonAsync($"/api/admin/booths/{seed.BoothId}/activate-offer", new
        {
            boothOfferId = seed.OfferId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Client subscription must be TRIAL or ACTIVE for new booth activation.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClientOwnerCannotCreateLocationForAnotherTenant()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(StatusValues.Subscription.Active, activeBoothAllowance: 1);
        var otherClientId = await factory.SeedClientAccountAsync("Other Tenant");
        await LoginAsync(client, seed.ClientOwnerEmail);

        var response = await client.PostAsJsonAsync("/api/admin/locations", new
        {
            clientAccountId = otherClientId,
            name = "Other Mall",
            address = "Not this tenant"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var location = await response.Content.ReadFromJsonAsync<LocationSummary>();
        Assert.NotNull(location);
        Assert.Equal(seed.ClientAccountId, location.ClientAccountId);
    }

    [Fact]
    public async Task ClientOwnerOverviewExcludesOtherTenantBoothBoundRows()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true);
        var otherSeed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            ownerEmail: "other-owner@photobiz.test");
        await LoginAsync(client, seed.ClientOwnerEmail);

        var overview = await client.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");

        Assert.NotNull(overview);
        Assert.Contains(overview.Activations, item => item.BoothId == seed.BoothId);
        Assert.DoesNotContain(overview.Activations, item => item.BoothId == otherSeed.BoothId);
        Assert.Contains(overview.PaymentAssignments, item => item.BoothId == seed.BoothId);
        Assert.DoesNotContain(overview.PaymentAssignments, item => item.BoothId == otherSeed.BoothId);
    }

    [Fact]
    public async Task OverviewIncludesScopedReportsAndAuditLogs()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            ownerEmail: "reports-owner@photobiz.test");
        var otherSeed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            ownerEmail: "reports-other-owner@photobiz.test");
        _ = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Completed, StatusValues.LumaboothSessionMode.Print);
        _ = await factory.SeedSessionTransactionAsync(otherSeed, StatusValues.Transaction.Completed, StatusValues.LumaboothSessionMode.Print);
        await factory.SeedAuditLogAsync(seed.ClientAccountId, seed.ClientOwnerId, "reports.seeded");
        await factory.SeedAuditLogAsync(otherSeed.ClientAccountId, otherSeed.ClientOwnerId, "reports.other_seeded");
        await LoginAsync(client, seed.ClientOwnerEmail);

        var overview = await client.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");

        Assert.NotNull(overview);
        Assert.Equal(25000, overview.Reports.Sales.TodayGrossSalesCents);
        Assert.Equal(1, overview.Reports.Sales.TodayCompletedSessions);
        Assert.Contains(overview.Reports.BoothSales, item => item.BoothId == seed.BoothId && item.GrossSalesCents == 25000);
        Assert.DoesNotContain(overview.Reports.BoothSales, item => item.BoothId == otherSeed.BoothId);
        Assert.Contains(overview.AuditLogs, item => item.Action == "reports.seeded");
        Assert.DoesNotContain(overview.AuditLogs, item => item.Action == "reports.other_seeded");
    }

    [Fact]
    public async Task CreateCashierRequiresSameTenantAssignedBooth()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1);
        var otherSeed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            ownerEmail: "other-owner@photobiz.test");
        await LoginAsync(client, seed.ClientOwnerEmail);

        var missingBoothResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            clientAccountId = seed.ClientAccountId,
            name = "Cashier",
            email = "cashier@photobiz.test",
            password = "Test12345!",
            role = StatusValues.User.Cashier
        });
        var crossTenantResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            clientAccountId = seed.ClientAccountId,
            assignedBoothId = otherSeed.BoothId,
            name = "Cashier",
            email = "cashier2@photobiz.test",
            password = "Test12345!",
            role = StatusValues.User.Cashier
        });

        Assert.Equal(HttpStatusCode.BadRequest, missingBoothResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, crossTenantResponse.StatusCode);
    }

    [Fact]
    public async Task NonCashPaymentAssignmentIsLockedAndExcludedFromBoothRuntimeConfig()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: "kiosk-token");
        await LoginAsync(client, seed.ClientOwnerEmail);

        var assignmentResponse = await client.PostAsJsonAsync($"/api/admin/booths/{seed.BoothId}/payment-options", new
        {
            paymentMethod = StatusValues.PaymentMethod.MayaCheckoutQr,
            runtimeEnabled = true
        });

        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);
        var assignment = await assignmentResponse.Content.ReadFromJsonAsync<PaymentAssignmentSummary>();
        Assert.NotNull(assignment);
        Assert.False(assignment.RuntimeEnabled);
        Assert.Equal(StatusValues.PaymentAssignment.Locked, assignment.Status);

        client.DefaultRequestHeaders.Remove("X-Kiosk-Token");
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", "kiosk-token");

        var config = await client.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");

        Assert.NotNull(config);
        Assert.DoesNotContain(config.PaymentOptions, option => option.Method == StatusValues.PaymentMethod.MayaCheckoutQr);
        Assert.Contains(config.PaymentOptions, option => option.Method == StatusValues.PaymentMethod.Cash && option.RuntimeEnabled);
    }

    [Fact]
    public async Task ApplicationOwnerCanUpdateClientAndSubscriptionLifecycle()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var ownerEmail = await factory.SeedApplicationOwnerAsync();
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 2,
            existingActiveBooths: 1);
        await LoginAsync(client, ownerEmail);

        var clientResponse = await client.PutAsJsonAsync($"/api/admin/clients/{seed.ClientAccountId}", new
        {
            name = "Updated Client",
            status = StatusValues.ClientAccount.Suspended
        });
        var subscriptionResponse = await client.PutAsJsonAsync($"/api/admin/subscriptions/{seed.SubscriptionId}", new
        {
            status = StatusValues.Subscription.PastDue,
            activeBoothAllowance = 1,
            endsOn = (DateOnly?)null,
            notes = "Manual lifecycle update"
        });
        var tooLowAllowanceResponse = await client.PutAsJsonAsync($"/api/admin/subscriptions/{seed.SubscriptionId}", new
        {
            status = StatusValues.Subscription.Active,
            activeBoothAllowance = 0,
            endsOn = (DateOnly?)null,
            notes = "Too low"
        });

        Assert.Equal(HttpStatusCode.OK, clientResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, subscriptionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLowAllowanceResponse.StatusCode);

        var updatedClient = await clientResponse.Content.ReadFromJsonAsync<ClientSummary>();
        var updatedSubscription = await subscriptionResponse.Content.ReadFromJsonAsync<ClientSubscriptionSummary>();

        Assert.NotNull(updatedClient);
        Assert.Equal(StatusValues.ClientAccount.Suspended, updatedClient.Status);
        Assert.NotNull(updatedSubscription);
        Assert.Equal(StatusValues.Subscription.PastDue, updatedSubscription.Status);
        Assert.Equal(1, updatedSubscription.ActiveBoothAllowance);
    }

    [Fact]
    public async Task ClientOwnerCanUpdateAndDeactivateLocationBoothAndOffer()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true);
        await LoginAsync(client, seed.ClientOwnerEmail);

        var locationResponse = await client.PutAsJsonAsync($"/api/admin/locations/{seed.LocationId}", new
        {
            name = "Updated Location",
            address = "Updated Address",
            status = StatusValues.Booth.Inactive
        });
        var offerResponse = await client.PutAsJsonAsync($"/api/admin/offers/{seed.OfferId}", new
        {
            name = "Updated Offer",
            description = "Updated description",
            offerType = StatusValues.OfferType.PerSession,
            priceCents = 30000,
            currency = "PHP",
            includedPrintEntitlement = StatusValues.PrintEntitlement.TwoBySixOrOneByFour,
            durationHours = (int?)null,
            sessionAllowance = (int?)null,
            allowsExtraPrintAddOn = false,
            extraPrintPriceCents = (int?)null,
            lumaboothSessionMode = "SESSION_STANDARD",
            active = false
        });
        var boothResponse = await client.PutAsJsonAsync($"/api/admin/booths/{seed.BoothId}", new
        {
            locationId = seed.LocationId,
            name = "Updated Booth",
            code = "UPDATED-BOOTH",
            status = StatusValues.Booth.Inactive
        });
        var overview = await client.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");

        Assert.Equal(HttpStatusCode.OK, locationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, offerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, boothResponse.StatusCode);
        Assert.NotNull(overview);
        Assert.DoesNotContain(overview.Activations, item => item.BoothId == seed.BoothId && item.Status == StatusValues.OfferActivation.Active);
    }

    [Fact]
    public async Task ClientOwnerCanUpdateUserLifecycleAndDisablePaymentAssignment()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: "payment-disable-token");
        await LoginAsync(client, seed.ClientOwnerEmail);

        var createUserResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            clientAccountId = seed.ClientAccountId,
            assignedBoothId = seed.BoothId,
            name = "Cashier",
            email = "cashier3@photobiz.test",
            password = "Test12345!",
            role = StatusValues.User.Cashier
        });
        createUserResponse.EnsureSuccessStatusCode();
        var cashier = await createUserResponse.Content.ReadFromJsonAsync<UserSummary>();
        Assert.NotNull(cashier);

        var updateUserResponse = await client.PutAsJsonAsync($"/api/admin/users/{cashier.Id}", new
        {
            assignedBoothId = seed.BoothId,
            name = "Updated Cashier",
            email = "cashier3@photobiz.test",
            role = StatusValues.User.Cashier,
            status = StatusValues.User.Inactive
        });
        var invalidAdminAssignmentResponse = await client.PutAsJsonAsync($"/api/admin/users/{cashier.Id}", new
        {
            assignedBoothId = seed.BoothId,
            name = "Updated Cashier",
            email = "cashier3@photobiz.test",
            role = StatusValues.User.ClientAdmin,
            status = StatusValues.User.Active
        });
        var disablePaymentResponse = await client.DeleteAsync($"/api/admin/booths/{seed.BoothId}/payment-options/{StatusValues.PaymentMethod.Cash}");

        client.DefaultRequestHeaders.Remove("X-Kiosk-Token");
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", "payment-disable-token");
        var config = await client.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");

        Assert.Equal(HttpStatusCode.OK, updateUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidAdminAssignmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, disablePaymentResponse.StatusCode);
        Assert.NotNull(config);
        Assert.DoesNotContain(config.PaymentOptions, option => option.Method == StatusValues.PaymentMethod.Cash);
    }

    [Fact]
    public async Task AgentCommandIncludesNormalizedLumaBoothMetadata()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string agentCredential = "agent-secret";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeFreshHeartbeat: true,
            agentCredential: agentCredential);
        await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Paid, StatusValues.LumaboothSessionMode.LegacySessionStandard);
        client.DefaultRequestHeaders.Add("X-Agent-Credential", agentCredential);

        var response = await client.GetAsync($"/api/agent/commands/next?boothCode={Uri.EscapeDataString(seed.BoothCode)}");

        response.EnsureSuccessStatusCode();
        var command = await response.Content.ReadFromJsonAsync<AgentCommandResponse>();
        Assert.NotNull(command);
        Assert.Equal("START_SESSION", command.Command);
        Assert.Equal(StatusValues.LumaboothSessionMode.Print, command.LumaboothSessionMode);
        Assert.Equal(StatusValues.OfferType.PerSession, command.OfferType);
        Assert.Equal(StatusValues.TransactionType.SessionPurchase, command.TransactionType);
        Assert.Equal(StatusValues.PrintEntitlement.TwoBySixOrOneByFour, command.IncludedPrintEntitlement);
        Assert.Equal(0, command.ExtraPrintCount);
    }

    [Fact]
    public async Task AgentSessionCallbacksPersistLumaBoothReferenceAndRemainIdempotent()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string agentCredential = "agent-secret";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeFreshHeartbeat: true,
            agentCredential: agentCredential);
        var transactionId = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.StartingSession, StatusValues.LumaboothSessionMode.Print);
        client.DefaultRequestHeaders.Add("X-Agent-Credential", agentCredential);

        var started = await client.PostAsJsonAsync($"/api/agent/transactions/{transactionId}/session-started", new
        {
            boothCode = seed.BoothCode,
            lumaboothSessionRef = "PBZ-luma-1",
            lumaboothEventType = "session_start"
        });
        var duplicateStarted = await client.PostAsJsonAsync($"/api/agent/transactions/{transactionId}/session-started", new
        {
            boothCode = seed.BoothCode,
            lumaboothSessionRef = "PBZ-luma-1",
            lumaboothEventType = "session_start"
        });
        var completed = await client.PostAsJsonAsync($"/api/agent/transactions/{transactionId}/session-completed", new
        {
            boothCode = seed.BoothCode,
            lumaboothSessionRef = "PBZ-luma-1",
            lumaboothEventType = "session_end"
        });
        var duplicateCompleted = await client.PostAsJsonAsync($"/api/agent/transactions/{transactionId}/session-completed", new
        {
            boothCode = seed.BoothCode,
            lumaboothSessionRef = "PBZ-luma-1",
            lumaboothEventType = "session_end"
        });
        var result = await factory.LoadTransactionSessionAsync(transactionId);

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicateStarted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicateCompleted.StatusCode);
        Assert.Equal(StatusValues.Transaction.Completed, result.TransactionStatus);
        Assert.Equal(StatusValues.Session.Completed, result.SessionStatus);
        Assert.Equal("PBZ-luma-1", result.LumaBoothSessionRef);
    }

    [Fact]
    public async Task CashierCanSellLatestSessionExtraPrintAndAgentCompletesPrintCopies()
    {
        await using var factory = new PhotoBizApiFactory();
        var cashierClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var agentClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string agentCredential = "agent-extra-print-secret";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeFreshHeartbeat: true,
            agentCredential: agentCredential);
        var cashierEmail = await factory.SeedCashierAsync(seed, "cashier-extra-print@photobiz.test");
        var parentTransactionId = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Completed, StatusValues.LumaboothSessionMode.Print);
        await LoginAsync(cashierClient, cashierEmail);
        agentClient.DefaultRequestHeaders.Add("X-Agent-Credential", agentCredential);

        var addOnResponse = await cashierClient.PostAsJsonAsync($"/api/cashier/transactions/{parentTransactionId}/extra-prints", new
        {
            copyCount = 2
        });
        addOnResponse.EnsureSuccessStatusCode();
        var addOn = await addOnResponse.Content.ReadFromJsonAsync<TransactionSummary>();
        Assert.NotNull(addOn);
        Assert.Equal(StatusValues.TransactionType.ExtraPrintAddOn, addOn.TransactionType);
        Assert.Equal(parentTransactionId, addOn.ParentTransactionId);
        Assert.Equal(2, addOn.ExtraPrintCount);
        Assert.Equal(10000, addOn.AmountCents);

        var approvalResponse = await cashierClient.PostAsJsonAsync($"/api/cashier/transactions/{addOn.Id}/approve-cash", new { });
        approvalResponse.EnsureSuccessStatusCode();

        var commandResponse = await agentClient.GetAsync($"/api/agent/commands/next?boothCode={Uri.EscapeDataString(seed.BoothCode)}");
        commandResponse.EnsureSuccessStatusCode();
        var command = await commandResponse.Content.ReadFromJsonAsync<AgentCommandResponse>();
        Assert.NotNull(command);
        Assert.Equal("PRINT_COPIES", command.Command);
        Assert.Equal(2, command.ExtraPrintCount);
        Assert.Equal(StatusValues.TransactionType.ExtraPrintAddOn, command.TransactionType);
        Assert.Equal(0, await factory.CountSessionsForTransactionAsync(addOn.Id));

        var printCompletedResponse = await agentClient.PostAsJsonAsync($"/api/agent/transactions/{addOn.Id}/print-completed", new
        {
            boothCode = seed.BoothCode,
            lumaboothEventType = "print_completed"
        });
        printCompletedResponse.EnsureSuccessStatusCode();

        var completed = await factory.LoadTransactionRecordAsync(addOn.Id);
        Assert.Equal(StatusValues.Transaction.Completed, completed.Status);
        Assert.Equal(StatusValues.Booth.Welcome, completed.BoothState);
    }

    [Fact]
    public async Task ExtraPrintAddOnRejectsNonLatestSessionAndInvalidCopyCounts()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeFreshHeartbeat: true);
        var cashierEmail = await factory.SeedCashierAsync(seed, "cashier-extra-print-reject@photobiz.test");
        var olderTransactionId = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Completed, StatusValues.LumaboothSessionMode.Print);
        await factory.BackdateCompletedTransactionAsync(olderTransactionId, TimeSpan.FromMinutes(10));
        _ = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Completed, StatusValues.LumaboothSessionMode.Print);
        await LoginAsync(client, cashierEmail);

        var nonLatestResponse = await client.PostAsJsonAsync($"/api/cashier/transactions/{olderTransactionId}/extra-prints", new
        {
            copyCount = 1
        });
        var invalidCopyResponse = await client.PostAsJsonAsync($"/api/cashier/transactions/{olderTransactionId}/extra-prints", new
        {
            copyCount = 6
        });

        Assert.Equal(HttpStatusCode.BadRequest, nonLatestResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCopyResponse.StatusCode);
    }

    [Theory]
    [InlineData(StatusValues.OfferType.TimeUnlimited)]
    [InlineData(StatusValues.OfferType.SessionCount)]
    public async Task ExtraPrintAddOnRejectsTimedAndSessionCountOfferSnapshots(string offerType)
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeFreshHeartbeat: true);
        var cashierEmail = await factory.SeedCashierAsync(seed, $"cashier-{offerType.ToLowerInvariant()}@photobiz.test");
        var parentTransactionId = await factory.SeedSessionTransactionAsync(
            seed,
            StatusValues.Transaction.Completed,
            StatusValues.LumaboothSessionMode.Print,
            offerType);
        await LoginAsync(client, cashierEmail);

        var response = await client.PostAsJsonAsync($"/api/cashier/transactions/{parentTransactionId}/extra-prints", new
        {
            copyCount = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Extra prints are available only for PER_SESSION transactions.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CashierOverviewIsScopedToAssignedBooth()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 2,
            existingActiveBooths: 2,
            includeOffer: true,
            includeActivation: true,
            includeFreshHeartbeat: true);
        var secondBoothId = await factory.GetSecondBoothIdAsync(seed);
        var cashierEmail = await factory.SeedCashierAsync(seed, "cashier-assigned-scope@photobiz.test");
        _ = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Completed, StatusValues.LumaboothSessionMode.Print);
        _ = await factory.SeedSessionTransactionAsync(seed with { BoothId = secondBoothId }, StatusValues.Transaction.Completed, StatusValues.LumaboothSessionMode.Print);
        await LoginAsync(client, cashierEmail);

        var overview = await client.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");

        Assert.NotNull(overview);
        Assert.Single(overview.Booths);
        Assert.Equal(seed.BoothId, overview.Booths.Single().Id);
        Assert.All(overview.Transactions, transaction => Assert.Equal(seed.BoothId, transaction.BoothId));
        Assert.All(overview.Reports.BoothSales, boothReport => Assert.Equal(seed.BoothId, boothReport.BoothId));
    }

    [Fact]
    public async Task CashierReturnToWelcomeCancelsActiveTransactionAndAllowsNewKioskTransaction()
    {
        await using var factory = new PhotoBizApiFactory();
        var cashierClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var boothClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: "return-welcome-token");
        var cashierEmail = await factory.SeedCashierAsync(seed, "cashier-return@photobiz.test");
        await LoginAsync(cashierClient, cashierEmail);

        boothClient.DefaultRequestHeaders.Add("X-Kiosk-Token", "return-welcome-token");
        var firstTransaction = await boothClient.PostAsJsonAsync("/api/booth-ui/transactions", new { });
        firstTransaction.EnsureSuccessStatusCode();
        var firstTransactionBody = await firstTransaction.Content.ReadFromJsonAsync<TransactionSummary>();
        Assert.NotNull(firstTransactionBody);

        var returnResponse = await cashierClient.PostAsJsonAsync(
            $"/api/cashier/booths/{seed.BoothId}/return-to-welcome",
            new { });

        Assert.Equal(HttpStatusCode.OK, returnResponse.StatusCode);

        var recovered = await factory.LoadTransactionRecordAsync(firstTransactionBody.Id);
        Assert.Equal(StatusValues.Transaction.Cancelled, recovered.Status);
        Assert.Equal(StatusValues.Booth.Welcome, recovered.BoothState);
        Assert.Equal("Manual booth recovery returned the booth to welcome.", recovered.FailureReason);

        var secondTransaction = await boothClient.PostAsJsonAsync("/api/booth-ui/transactions", new { });
        secondTransaction.EnsureSuccessStatusCode();
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = PhotoBizApiFactory.Password
        });
        response.EnsureSuccessStatusCode();

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookies.Select(cookie => cookie.Split(';', 2)[0]));
    }

    private sealed class PhotoBizApiFactory : WebApplicationFactory<Program>
    {
        public const string Password = "Test12345!";

        private readonly string databaseName = $"photobiz-api-tests-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Database=photobiz_tests;Username=test;Password=test");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<PhotoBizDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<PhotoBizDbContext>>();
                services.AddDbContext<PhotoBizDbContext>(options =>
                {
                    options.UseInMemoryDatabase(databaseName);
                });
            });
        }

        public async Task<string> SeedApplicationOwnerAsync(string email = "platform-owner@photobiz.test")
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Name = "Platform Owner",
                Email = email,
                Role = StatusValues.User.ApplicationOwner,
                Status = StatusValues.User.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };
            user.PasswordHash = passwordHasher.HashPassword(user, Password);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            return email;
        }

        public async Task<Guid> SeedClientAccountAsync(string name)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var client = new ClientAccount
            {
                Id = Guid.NewGuid(),
                Name = name,
                Status = StatusValues.ClientAccount.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.ClientAccounts.Add(client);
            await dbContext.SaveChangesAsync();

            return client.Id;
        }

        public async Task<SeedResult> SeedClientSetupAsync(
            string subscriptionStatus,
            int activeBoothAllowance,
            int existingActiveBooths = 0,
            bool includeOffer = false,
            bool includeActivation = false,
            bool includeAppearance = false,
            bool includeFreshHeartbeat = false,
            string? kioskToken = null,
            string? agentCredential = null,
            string ownerEmail = "owner@photobiz.test")
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();
            var tokenHasher = scope.ServiceProvider.GetRequiredService<PhotoBizTokenHasher>();

            var clientId = Guid.NewGuid();
            var planId = Guid.NewGuid();
            var subscriptionId = Guid.NewGuid();
            var locationId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var firstBoothId = Guid.NewGuid();
            var firstBoothCode = $"BOOTH-1-{clientId:N}"[..20].ToUpperInvariant();
            Guid? offerId = null;

            dbContext.ClientAccounts.Add(new ClientAccount
            {
                Id = clientId,
                Name = $"Client {clientId:N}",
                Status = StatusValues.ClientAccount.Active,
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
                Status = subscriptionStatus,
                ActiveBoothAllowance = activeBoothAllowance,
                StartsOn = DateOnly.FromDateTime(DateTime.UtcNow)
            });
            dbContext.Locations.Add(new Location
            {
                Id = locationId,
                ClientAccountId = clientId,
                Name = $"Location {locationId:N}",
                Status = StatusValues.ClientAccount.Active
            });

            var user = new ApplicationUser
            {
                Id = ownerId,
                ClientAccountId = clientId,
                Name = "Client Owner",
                Email = ownerEmail,
                Role = StatusValues.User.ClientOwner,
                Status = StatusValues.User.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };
            user.PasswordHash = passwordHasher.HashPassword(user, Password);
            dbContext.Users.Add(user);

            for (var index = 0; index < existingActiveBooths; index++)
            {
                var boothId = index == 0 ? firstBoothId : Guid.NewGuid();
                dbContext.Booths.Add(new Booth
                {
                    Id = boothId,
                    ClientAccountId = clientId,
                    LocationId = locationId,
                    Name = $"Booth {index + 1}",
                    Code = index == 0 ? firstBoothCode : $"BOOTH-{index + 1}-{clientId:N}"[..20].ToUpperInvariant(),
                    Status = StatusValues.Booth.Active,
                    CurrentState = StatusValues.Booth.Welcome,
                    LastHeartbeatAt = includeFreshHeartbeat ? DateTimeOffset.UtcNow : null,
                    KioskTokenHash = index == 0 && kioskToken is not null ? tokenHasher.Hash(kioskToken) : null,
                    AgentCredentialHash = index == 0 && agentCredential is not null ? tokenHasher.Hash(agentCredential) : null
                });
                dbContext.BoothPaymentOptionAssignments.Add(new BoothPaymentOptionAssignment
                {
                    Id = Guid.NewGuid(),
                    BoothId = boothId,
                    PaymentMethod = StatusValues.PaymentMethod.Cash,
                    RuntimeEnabled = true,
                    Status = StatusValues.PaymentAssignment.Assigned,
                    AssignedAt = DateTimeOffset.UtcNow
                });

                if (includeAppearance && index == 0)
                {
                    dbContext.BoothAppearanceConfigs.Add(new BoothAppearanceConfig
                    {
                        Id = Guid.NewGuid(),
                        BoothId = boothId,
                        ThemePreset = StatusValues.Theme.VintageFilm,
                        PrimaryColor = "#2f6868",
                        AccentColor = "#f5d27e",
                        SessionLabel = "Test session",
                        DefaultWelcomeHeadline = "Welcome",
                        DefaultWelcomeSubtitle = "Test subtitle"
                    });
                }
            }

            if (includeOffer)
            {
                offerId = Guid.NewGuid();
                dbContext.BoothOffers.Add(new BoothOffer
                {
                    Id = offerId.Value,
                    ClientAccountId = clientId,
                    Name = $"Offer {offerId.Value:N}",
                    OfferType = StatusValues.OfferType.PerSession,
                    PriceCents = 25000,
                    Currency = "PHP",
                    IncludedPrintEntitlement = StatusValues.PrintEntitlement.TwoBySixOrOneByFour,
                    AllowsExtraPrintAddOn = true,
                    ExtraPrintPriceCents = 5000,
                    LumaboothSessionMode = "SESSION_STANDARD",
                    Active = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });

                if (includeActivation && existingActiveBooths > 0)
                {
                    dbContext.BoothOfferActivations.Add(new BoothOfferActivation
                    {
                        Id = Guid.NewGuid(),
                        BoothId = firstBoothId,
                        BoothOfferId = offerId.Value,
                        Status = StatusValues.OfferActivation.Active,
                        ActivatedAt = DateTimeOffset.UtcNow
                    });
                }
            }

            await dbContext.SaveChangesAsync();

            return new SeedResult(clientId, subscriptionId, locationId, ownerId, firstBoothId, firstBoothCode, offerId ?? Guid.Empty, ownerEmail);
        }

        public async Task<string> SeedCashierAsync(SeedResult seed, string email)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();

            var cashier = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                ClientAccountId = seed.ClientAccountId,
                AssignedBoothId = seed.BoothId,
                Name = "Cashier",
                Email = email,
                Role = StatusValues.User.Cashier,
                Status = StatusValues.User.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };
            cashier.PasswordHash = passwordHasher.HashPassword(cashier, Password);
            dbContext.Users.Add(cashier);
            await dbContext.SaveChangesAsync();

            return email;
        }

        public async Task<Guid> SeedSessionTransactionAsync(
            SeedResult seed,
            string status,
            string lumaboothSessionMode,
            string offerType = StatusValues.OfferType.PerSession)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var transactionId = Guid.NewGuid();
            dbContext.Transactions.Add(new Transaction
            {
                Id = transactionId,
                ClientAccountId = seed.ClientAccountId,
                LocationId = seed.LocationId,
                BoothId = seed.BoothId,
                BoothOfferId = seed.OfferId,
                BoothOfferActivationId = Guid.NewGuid(),
                TransactionNumber = $"TXN-{transactionId:N}"[..20],
                TransactionType = StatusValues.TransactionType.SessionPurchase,
                PaymentMethod = StatusValues.PaymentMethod.Cash,
                Status = status,
                AmountCents = 25000,
                Currency = "PHP",
                OfferSnapshot = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Id = seed.OfferId,
                    Name = offerType == StatusValues.OfferType.PerSession ? "Per Session" : "Plan Offer",
                    OfferType = offerType,
                    PriceCents = 25000,
                    Currency = "PHP",
                    IncludedPrintEntitlement = StatusValues.PrintEntitlement.TwoBySixOrOneByFour,
                    AllowsExtraPrintAddOn = true,
                    ExtraPrintPriceCents = 5000,
                    LumaboothSessionMode = lumaboothSessionMode
                }),
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                PaidAt = status == StatusValues.Transaction.Paid ||
                    status == StatusValues.Transaction.StartingSession ||
                    status == StatusValues.Transaction.InSession ||
                    status == StatusValues.Transaction.Completed
                    ? DateTimeOffset.UtcNow
                    : null,
                CompletedAt = status == StatusValues.Transaction.Completed
                    ? DateTimeOffset.UtcNow
                    : null
            });

            if (status == StatusValues.Transaction.StartingSession)
            {
                dbContext.BoothSessions.Add(new BoothSession
                {
                    Id = Guid.NewGuid(),
                    BoothId = seed.BoothId,
                    TransactionId = transactionId,
                    Status = StatusValues.Session.Starting
                });
            }

            await dbContext.SaveChangesAsync();
            return transactionId;
        }

        public async Task SeedAuditLogAsync(Guid clientAccountId, Guid userId, string action)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                ClientAccountId = clientAccountId,
                UserId = userId,
                Action = action,
                EntityType = "Test",
                EntityId = clientAccountId,
                Metadata = "{}",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        public async Task<Guid> GetSecondBoothIdAsync(SeedResult seed)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            return await dbContext.Booths
                .Where(item => item.ClientAccountId == seed.ClientAccountId && item.Id != seed.BoothId)
                .Select(item => item.Id)
                .SingleAsync();
        }

        public async Task BackdateCompletedTransactionAsync(Guid transactionId, TimeSpan age)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var transaction = await dbContext.Transactions.SingleAsync(item => item.Id == transactionId);
            transaction.CreatedAt = DateTimeOffset.UtcNow.Subtract(age).AddSeconds(-5);
            transaction.CompletedAt = DateTimeOffset.UtcNow.Subtract(age);
            await dbContext.SaveChangesAsync();
        }

        public async Task<int> CountSessionsForTransactionAsync(Guid transactionId)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            return await dbContext.BoothSessions.CountAsync(item => item.TransactionId == transactionId);
        }

        public async Task<(string TransactionStatus, string SessionStatus, string? LumaBoothSessionRef)> LoadTransactionSessionAsync(Guid transactionId)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var transaction = await dbContext.Transactions.SingleAsync(item => item.Id == transactionId);
            var session = await dbContext.BoothSessions.SingleAsync(item => item.TransactionId == transactionId);
            return (transaction.Status, session.Status, session.LumaboothSessionRef);
        }

        public async Task<(string Status, string BoothState, string? FailureReason)> LoadTransactionRecordAsync(Guid transactionId)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var transaction = await dbContext.Transactions.SingleAsync(item => item.Id == transactionId);
            var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId);
            return (transaction.Status, booth.CurrentState, transaction.FailureReason);
        }
    }

    private sealed record SeedResult(
        Guid ClientAccountId,
        Guid SubscriptionId,
        Guid LocationId,
        Guid ClientOwnerId,
        Guid BoothId,
        string BoothCode,
        Guid OfferId,
        string ClientOwnerEmail);
}
