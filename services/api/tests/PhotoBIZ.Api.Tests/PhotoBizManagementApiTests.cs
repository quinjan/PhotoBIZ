using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
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
    public async Task ApplicationOwnerCanOnboardClientWithOwnerCredentials()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var ownerEmail = await factory.SeedApplicationOwnerAsync();
        await LoginAsync(client, ownerEmail);

        var response = await client.PostAsJsonAsync("/api/admin/clients/onboard", new
        {
            clientName = "The Memory Box PH",
            ownerName = "Julie Santos",
            ownerEmail = "julie@memorybox.test"
        });

        response.EnsureSuccessStatusCode();
        var onboarded = await response.Content.ReadFromJsonAsync<ClientOnboardingResponse>();

        Assert.NotNull(onboarded);
        Assert.Equal("The Memory Box PH", onboarded.Client.Name);
        Assert.Equal(StatusValues.User.ClientOwner, onboarded.Owner.Role);
        Assert.Equal(onboarded.Client.Id, onboarded.Owner.ClientAccountId);

        await LoginAsync(client, "julie@memorybox.test", PhotoBizApiFactory.DefaultInitialPassword);
        var session = await client.GetFromJsonAsync<AuthSessionResponse>("/api/auth/session");

        Assert.NotNull(session);
        Assert.Equal(StatusValues.User.ClientOwner, session.Role);
        Assert.Equal(onboarded.Client.Id, session.ClientAccountId);
        Assert.True(session.MustChangePassword);

        var blockedOverviewResponse = await client.GetAsync("/api/admin/overview");
        Assert.Equal(HttpStatusCode.Forbidden, blockedOverviewResponse.StatusCode);

        var changedSession = await ChangePasswordAsync(
            client,
            PhotoBizApiFactory.DefaultInitialPassword,
            PhotoBizApiFactory.Password,
            PhotoBizApiFactory.Password);

        Assert.False(changedSession.MustChangePassword);

        var overview = await client.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");

        Assert.NotNull(overview);
        Assert.Contains(overview.PrintEntitlements, entitlement => entitlement.Name == StatusValues.PrintEntitlement.TwoBySixOrOneByFour);
        Assert.Contains(overview.PrintEntitlements, entitlement => entitlement.Name == StatusValues.PrintEntitlement.TwoBySix);
        Assert.Contains(overview.PrintEntitlements, entitlement => entitlement.Name == StatusValues.PrintEntitlement.OneByFour);
    }

    [Fact]
    public async Task LoginAndExistingSessionsHonorUserAndClientLifecycle()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var inactiveUserSeed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            ownerEmail: "inactive-owner@photobiz.test");
        var archivedClientSeed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            ownerEmail: "archived-owner@photobiz.test");
        var activeSessionSeed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            ownerEmail: "session-owner@photobiz.test");

        await factory.SetUserStatusAsync(inactiveUserSeed.ClientOwnerId, StatusValues.User.Inactive);
        await factory.SetClientStatusAsync(archivedClientSeed.ClientAccountId, StatusValues.ClientAccount.Archived);

        var inactiveLoginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = inactiveUserSeed.ClientOwnerEmail,
            password = PhotoBizApiFactory.Password
        });
        var archivedLoginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = archivedClientSeed.ClientOwnerEmail,
            password = PhotoBizApiFactory.Password
        });

        await LoginAsync(client, activeSessionSeed.ClientOwnerEmail);
        await factory.SetUserStatusAsync(activeSessionSeed.ClientOwnerId, StatusValues.User.Inactive);
        var invalidatedSessionResponse = await client.GetAsync("/api/auth/session");

        Assert.Equal(HttpStatusCode.BadRequest, inactiveLoginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, archivedLoginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidatedSessionResponse.StatusCode);
    }

    [Fact]
    public async Task ClientOwnerCanCreateAndUpdatePrintEntitlements()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(StatusValues.Subscription.Active, activeBoothAllowance: 1);
        await LoginAsync(client, seed.ClientOwnerEmail);

        var createResponse = await client.PostAsJsonAsync("/api/admin/print-entitlements", new
        {
            clientAccountId = seed.ClientAccountId,
            name = "4 pcs 2x6"
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<PrintEntitlementSummary>();

        Assert.NotNull(created);
        Assert.Equal("4 pcs 2x6", created.Name);

        var updateResponse = await client.PutAsJsonAsync($"/api/admin/print-entitlements/{created.Id}", new
        {
            name = "4 pcs 2x6 premium"
        });

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<PrintEntitlementSummary>();

        Assert.NotNull(updated);
        Assert.Equal("4 pcs 2x6 premium", updated.Name);
    }

    [Fact]
    public async Task ClientOwnerCanDeleteUnusedPrintEntitlementOnly()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            includeOffer: true);
        await LoginAsync(client, seed.ClientOwnerEmail);

        var unusedCreateResponse = await client.PostAsJsonAsync("/api/admin/print-entitlements", new
        {
            clientAccountId = seed.ClientAccountId,
            name = "4 pcs 2x6"
        });
        var usedCreateResponse = await client.PostAsJsonAsync("/api/admin/print-entitlements", new
        {
            clientAccountId = seed.ClientAccountId,
            name = StatusValues.PrintEntitlement.TwoBySixOrOneByFour
        });

        unusedCreateResponse.EnsureSuccessStatusCode();
        usedCreateResponse.EnsureSuccessStatusCode();
        var unused = await unusedCreateResponse.Content.ReadFromJsonAsync<PrintEntitlementSummary>();
        var used = await usedCreateResponse.Content.ReadFromJsonAsync<PrintEntitlementSummary>();

        Assert.NotNull(unused);
        Assert.NotNull(used);

        var unusedDeleteResponse = await client.DeleteAsync($"/api/admin/print-entitlements/{unused.Id}");
        var usedDeleteResponse = await client.DeleteAsync($"/api/admin/print-entitlements/{used.Id}");
        var overview = await client.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");

        Assert.Equal(HttpStatusCode.NoContent, unusedDeleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, usedDeleteResponse.StatusCode);
        Assert.NotNull(overview);
        Assert.DoesNotContain(overview.PrintEntitlements, entitlement => entitlement.Id == unused.Id);
        Assert.Contains(overview.PrintEntitlements, entitlement => entitlement.Id == used.Id);
    }

    [Fact]
    public async Task ApplicationOwnerCanCreateAndUpdateSubscriptionDefinition()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var ownerEmail = await factory.SeedApplicationOwnerAsync();
        await LoginAsync(client, ownerEmail);

        var createResponse = await client.PostAsJsonAsync("/api/admin/subscription-plans", new
        {
            name = "Per Booth MVP",
            pricePerBoothCents = 200000,
            currency = "PHP"
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SubscriptionPlanSummary>();

        Assert.NotNull(created);
        Assert.Equal("Per Booth MVP", created.Name);
        Assert.Equal(200000, created.PricePerBoothCents);
        Assert.True(created.Active);

        var updateResponse = await client.PutAsJsonAsync($"/api/admin/subscription-plans/{created.Id}", new
        {
            name = "Launch Subscription",
            pricePerBoothCents = 250000,
            currency = "PHP",
            active = false
        });

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<SubscriptionPlanSummary>();

        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Launch Subscription", updated.Name);
        Assert.Equal(250000, updated.PricePerBoothCents);
        Assert.False(updated.Active);

        var overview = await client.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");

        Assert.NotNull(overview);
        var overviewSubscription = Assert.Single(
            overview.SubscriptionPlans,
            item => item.Id == created.Id);
        Assert.Equal("Launch Subscription", overviewSubscription.Name);
        Assert.False(overviewSubscription.Active);
    }

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
            includeActivation: true,
            includeAppearance: true);
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
        Assert.Contains(overview.AppearanceConfigs, item => item.BoothId == seed.BoothId);
        Assert.DoesNotContain(overview.AppearanceConfigs, item => item.BoothId == otherSeed.BoothId);
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
    public async Task CreateCashierAllowsUnassignedCreationAndRejectsCrossTenantBoothAssignment()
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
            role = StatusValues.User.Cashier
        });
        var crossTenantResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            clientAccountId = seed.ClientAccountId,
            assignedBoothId = otherSeed.BoothId,
            name = "Cashier",
            email = "cashier2@photobiz.test",
            role = StatusValues.User.Cashier
        });

        Assert.Equal(HttpStatusCode.OK, missingBoothResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, crossTenantResponse.StatusCode);

        var createdCashier = await missingBoothResponse.Content.ReadFromJsonAsync<UserSummary>();
        Assert.NotNull(createdCashier);
        Assert.Null(createdCashier.AssignedBoothId);

        var cashierClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        await LoginAsync(cashierClient, "cashier@photobiz.test", PhotoBizApiFactory.DefaultInitialPassword);
        var cashierSession = await cashierClient.GetFromJsonAsync<AuthSessionResponse>("/api/auth/session");
        var blockedOverviewResponse = await cashierClient.GetAsync("/api/admin/overview");
        var blockedCashierResponse = await cashierClient.PostAsJsonAsync($"/api/cashier/booths/{seed.BoothId}/return-to-welcome", new { });

        Assert.NotNull(cashierSession);
        Assert.True(cashierSession.MustChangePassword);
        Assert.Equal(HttpStatusCode.Forbidden, blockedOverviewResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, blockedCashierResponse.StatusCode);
    }

    [Fact]
    public async Task NormalUserManagementCannotCreatePromoteOrSelfDeactivateClientOwner()
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

        var createOwnerResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            clientAccountId = seed.ClientAccountId,
            name = "Second Owner",
            email = "second-owner@photobiz.test",
            role = StatusValues.User.ClientOwner
        });
        var createAdminResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            clientAccountId = seed.ClientAccountId,
            name = "Future Owner",
            email = "future-owner@photobiz.test",
            role = StatusValues.User.ClientAdmin
        });
        createAdminResponse.EnsureSuccessStatusCode();
        var admin = await createAdminResponse.Content.ReadFromJsonAsync<UserSummary>();
        Assert.NotNull(admin);

        var promoteResponse = await client.PutAsJsonAsync($"/api/admin/users/{admin.Id}", new
        {
            assignedBoothId = (Guid?)null,
            name = admin.Name,
            email = admin.Email,
            role = StatusValues.User.ClientOwner,
            status = StatusValues.User.Active
        });
        var selfDeactivateResponse = await client.PutAsJsonAsync($"/api/admin/users/{seed.ClientOwnerId}", new
        {
            assignedBoothId = (Guid?)null,
            name = "Client Owner",
            email = seed.ClientOwnerEmail,
            role = StatusValues.User.ClientOwner,
            status = StatusValues.User.Inactive
        });

        Assert.Equal(HttpStatusCode.BadRequest, createOwnerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, promoteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, selfDeactivateResponse.StatusCode);
    }

    [Fact]
    public async Task ApplicationOwnerCanTransferClientOwner()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var applicationOwnerEmail = await factory.SeedApplicationOwnerAsync();
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1);
        await LoginAsync(client, seed.ClientOwnerEmail);

        var createAdminResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            clientAccountId = seed.ClientAccountId,
            name = "New Owner",
            email = "new-owner@photobiz.test",
            role = StatusValues.User.ClientAdmin
        });
        createAdminResponse.EnsureSuccessStatusCode();
        var admin = await createAdminResponse.Content.ReadFromJsonAsync<UserSummary>();
        Assert.NotNull(admin);

        var ownerTransferByClientResponse = await client.PostAsJsonAsync($"/api/admin/clients/{seed.ClientAccountId}/transfer-owner", new
        {
            newOwnerUserId = admin.Id
        });

        await LoginAsync(client, applicationOwnerEmail);
        var ownerTransferResponse = await client.PostAsJsonAsync($"/api/admin/clients/{seed.ClientAccountId}/transfer-owner", new
        {
            newOwnerUserId = admin.Id
        });
        ownerTransferResponse.EnsureSuccessStatusCode();
        var overview = await client.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");

        Assert.Equal(HttpStatusCode.Forbidden, ownerTransferByClientResponse.StatusCode);
        Assert.NotNull(overview);
        Assert.Single(overview.Users, item => item.ClientAccountId == seed.ClientAccountId && item.Role == StatusValues.User.ClientOwner);
        Assert.Contains(overview.Users, item => item.Id == admin.Id && item.Role == StatusValues.User.ClientOwner);
        Assert.Contains(overview.Users, item => item.Id == seed.ClientOwnerId && item.Role == StatusValues.User.ClientAdmin);
    }

    [Fact]
    public async Task ClientOwnerCashierActionsRequireAssignedBooth()
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

        var unassignedResponse = await client.PostAsJsonAsync($"/api/cashier/booths/{seed.BoothId}/return-to-welcome", new { });

        var assignOwnerResponse = await client.PutAsJsonAsync($"/api/admin/users/{seed.ClientOwnerId}", new
        {
            assignedBoothId = seed.BoothId,
            name = "Client Owner",
            email = seed.ClientOwnerEmail,
            role = StatusValues.User.ClientOwner,
            status = StatusValues.User.Active
        });
        assignOwnerResponse.EnsureSuccessStatusCode();

        var assignedResponse = await client.PostAsJsonAsync($"/api/cashier/booths/{seed.BoothId}/return-to-welcome", new { });

        Assert.Equal(HttpStatusCode.Forbidden, unassignedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, assignedResponse.StatusCode);
    }

    [Fact]
    public async Task CreateBoothCanAssignExistingUnassignedCashier()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 2,
            existingActiveBooths: 1);
        await LoginAsync(client, seed.ClientOwnerEmail);

        var createCashierResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            clientAccountId = seed.ClientAccountId,
            name = "Cashier",
            email = "booth-assigned-cashier@photobiz.test",
            role = StatusValues.User.Cashier
        });
        createCashierResponse.EnsureSuccessStatusCode();
        var cashier = await createCashierResponse.Content.ReadFromJsonAsync<UserSummary>();
        Assert.NotNull(cashier);
        Assert.Null(cashier.AssignedBoothId);

        var createBoothResponse = await client.PostAsJsonAsync("/api/admin/booths", new
        {
            clientAccountId = seed.ClientAccountId,
            locationId = seed.LocationId,
            name = "Booth B",
            code = "SMA-002",
            cashierUserId = cashier.Id
        });

        createBoothResponse.EnsureSuccessStatusCode();

        var overview = await client.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");

        Assert.NotNull(overview);
        var assignedCashier = Assert.Single(overview.Users, item => item.Id == cashier.Id);
        var createdBooth = Assert.Single(overview.Booths, item => item.Name == "Booth B");
        Assert.Equal(createdBooth.Id, assignedCashier.AssignedBoothId);
    }

    [Fact]
    public async Task UpdateBoothCanChangePosStaffAssignmentWithinTenant()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 2,
            existingActiveBooths: 1);
        var otherSeed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            ownerEmail: "other-cashier-owner@photobiz.test");
        await LoginAsync(client, seed.ClientOwnerEmail);

        var cashierResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            clientAccountId = seed.ClientAccountId,
            name = "Assignable Cashier",
            email = "assignable-cashier@photobiz.test",
            role = StatusValues.User.Cashier
        });
        var adminResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            clientAccountId = seed.ClientAccountId,
            name = "Not Cashier",
            email = "not-cashier@photobiz.test",
            role = StatusValues.User.ClientAdmin
        });
        cashierResponse.EnsureSuccessStatusCode();
        adminResponse.EnsureSuccessStatusCode();
        var cashier = await cashierResponse.Content.ReadFromJsonAsync<UserSummary>();
        var admin = await adminResponse.Content.ReadFromJsonAsync<UserSummary>();
        Assert.NotNull(cashier);
        Assert.NotNull(admin);

        var assignResponse = await client.PutAsJsonAsync($"/api/admin/booths/{seed.BoothId}", new
        {
            locationId = seed.LocationId,
            name = "Updated Booth",
            code = "UPDATED-001",
            status = StatusValues.Booth.Active,
            cashierUserId = cashier.Id
        });
        var adminAssignmentResponse = await client.PutAsJsonAsync($"/api/admin/booths/{seed.BoothId}", new
        {
            locationId = seed.LocationId,
            name = "Updated Booth",
            code = "UPDATED-001",
            status = StatusValues.Booth.Active,
            cashierUserId = admin.Id
        });
        var crossTenantResponse = await client.PutAsJsonAsync($"/api/admin/booths/{seed.BoothId}", new
        {
            locationId = seed.LocationId,
            name = "Updated Booth",
            code = "UPDATED-001",
            status = StatusValues.Booth.Active,
            cashierUserId = otherSeed.ClientOwnerId
        });
        var overview = await client.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");

        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminAssignmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, crossTenantResponse.StatusCode);
        Assert.NotNull(overview);
        var reassignedCashier = Assert.Single(overview.Users, item => item.Id == cashier.Id);
        var assignedAdmin = Assert.Single(overview.Users, item => item.Id == admin.Id);
        Assert.Null(reassignedCashier.AssignedBoothId);
        Assert.Equal(seed.BoothId, assignedAdmin.AssignedBoothId);
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
            paymentMethod = StatusValues.PaymentMethod.PayMongoQrPh,
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
        Assert.DoesNotContain(config.PaymentOptions, option => option.Method == StatusValues.PaymentMethod.PayMongoQrPh);
        Assert.Contains(config.PaymentOptions, option => option.Method == StatusValues.PaymentMethod.Cash && option.RuntimeEnabled);
    }

    [Fact]
    public async Task PayMongoSetupRequiresCredentialsBeforeGeneratingWebhookUrl()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(StatusValues.Subscription.Active, activeBoothAllowance: 1);
        await LoginAsync(client, seed.ClientOwnerEmail);

        var emptyResponse = await client.PutAsJsonAsync("/api/admin/payment-resources/PAYMONGO_QRPH", new
        {
            enabled = true,
            paymentMode = "test",
            businessAccountName = "",
            publicKey = "",
            secretKey = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);
        var problem = await emptyResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("businessAccountName", problem.Errors.Keys);
        Assert.Contains("publicKey", problem.Errors.Keys);
        Assert.Contains("secretKey", problem.Errors.Keys);

        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.Add("X-Forwarded-Host", "api.203.0.113.10.sslip.io");

        var validResponse = await client.PutAsJsonAsync("/api/admin/payment-resources/PAYMONGO_QRPH", new
        {
            enabled = true,
            paymentMode = "test",
            businessAccountName = "PayMongo Test",
            publicKey = "pk_test_1234567890",
            secretKey = "sk_test_1234567890"
        });

        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
        var resource = await validResponse.Content.ReadFromJsonAsync<PaymentResourceSummary>();
        Assert.NotNull(resource);
        Assert.Equal(StatusValues.PaymentResource.Draft, resource.Status);
        Assert.NotNull(resource.WebhookUrl);
        Assert.Contains($"/api/payments/paymongo/webhooks/{resource.ResourceId}", resource.WebhookUrl, StringComparison.Ordinal);
        Assert.StartsWith("https://api.203.0.113.10.sslip.io/", resource.WebhookUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PayMongoQrPhSelectionCreatesQrAndWebhookMarksTransactionPaid()
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
            kioskToken: "paymongo-kiosk-token");
        var configId = await factory.SeedVerifiedPayMongoAsync(seed.ClientAccountId);
        await LoginAsync(client, seed.ClientOwnerEmail);

        var assignmentResponse = await client.PostAsJsonAsync($"/api/admin/booths/{seed.BoothId}/payment-options", new
        {
            paymentMethod = StatusValues.PaymentMethod.PayMongoQrPh,
            runtimeEnabled = true
        });
        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);

        client.DefaultRequestHeaders.Remove("X-Kiosk-Token");
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", "paymongo-kiosk-token");
        var createdResponse = await client.PostAsJsonAsync("/api/booth-ui/transactions", new { });
        var created = await createdResponse.Content.ReadFromJsonAsync<TransactionSummary>();
        Assert.NotNull(created);

        var selectedResponse = await client.PostAsJsonAsync($"/api/booth-ui/transactions/{created.Id}/payment-method", new
        {
            method = StatusValues.PaymentMethod.PayMongoQrPh
        });
        Assert.Equal(HttpStatusCode.OK, selectedResponse.StatusCode);

        var config = await client.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");
        Assert.NotNull(config);
        Assert.Contains(config.PaymentOptions, option => option.Method == StatusValues.PaymentMethod.PayMongoQrPh && option.RuntimeEnabled);
        Assert.NotNull(config.ActiveTransaction?.QrPayment);
        Assert.Equal(StatusValues.Transaction.PendingPayMongoQrPh, config.ActiveTransaction.Status);
        Assert.Equal("data:image/png;base64,cXJwaA==", config.ActiveTransaction.QrPayment.ImageUrl);

        var webhookPayload = $$"""
        {
          "data": {
            "id": "evt_paid_1",
            "type": "event",
            "attributes": {
              "type": "payment.paid",
              "livemode": false,
              "data": {
                "id": "pay_123",
                "type": "payment",
                "attributes": {
                  "amount": 25000,
                  "currency": "PHP",
                  "payment_intent_id": "{{FakePayMongoClient.PaymentIntentId}}"
                }
              }
            }
          }
        }
        """;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        client.DefaultRequestHeaders.Remove("Paymongo-Signature");
        client.DefaultRequestHeaders.Add(
            "Paymongo-Signature",
            $"t={timestamp},te={SignWebhook(timestamp, webhookPayload, PhotoBizApiFactory.PayMongoWebhookSecret)},li=");
        var webhookResponse = await client.PostAsync(
            $"/api/payments/paymongo/webhooks/{configId}",
            new StringContent(webhookPayload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
        var transaction = await dbContext.Transactions.SingleAsync(item => item.Id == created.Id);
        Assert.Equal(StatusValues.Transaction.Paid, transaction.Status);
        Assert.Equal(StatusValues.PaymentMethod.PayMongoQrPh, transaction.PaymentMethod);
        Assert.NotNull(transaction.PaidAt);
    }

    [Fact]
    public async Task BoothAppearanceUsesFixedThemeSchemeAndUploadedBackgroundData()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "theme-token";
        const string imageDataUrl = "data:image/png;base64,aGVsbG8=";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken);
        await LoginAsync(client, seed.ClientOwnerEmail);

        var response = await client.PutAsJsonAsync($"/api/admin/booths/{seed.BoothId}/appearance", new
        {
            themePreset = StatusValues.Theme.Pop,
            sessionLabel = "Neon Weekend",
            defaultWelcomeHeadline = "Pop In. Pose Big.",
            defaultWelcomeSubtitle = "Tap start and jump into LumaBooth.",
            completionThankYouMessage = "Thanks for going neon.",
            backgroundImageDataUrl = imageDataUrl
        });
        var overview = await client.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");

        client.DefaultRequestHeaders.Remove("X-Kiosk-Token");
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);
        var config = await client.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(overview);
        var appearance = Assert.Single(overview.AppearanceConfigs, item => item.BoothId == seed.BoothId);
        Assert.Equal(StatusValues.Theme.Pop, appearance.ThemePreset);
        Assert.Equal("#0bbbe6", appearance.PrimaryColor);
        Assert.Equal("#ff0090", appearance.AccentColor);
        Assert.Equal(imageDataUrl, appearance.BackgroundImageDataUrl);
        Assert.NotNull(config);
        Assert.Equal(StatusValues.Theme.Pop, config.Theme.Preset);
        Assert.Equal(imageDataUrl, config.Theme.BackgroundImageDataUrl);
        Assert.Null(config.Theme.BackgroundImageUrl);
        Assert.Equal("Pop In. Pose Big.", config.Session.WelcomeHeadline);
        Assert.Equal("Thanks for going neon.", config.Session.CompletionThankYouMessage);
    }

    [Fact]
    public async Task ClientOwnerCanReissueBoothCredentials()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string oldAgentCredential = "old-agent-secret";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            agentCredential: oldAgentCredential,
            kioskToken: "old-kiosk-token");
        await LoginAsync(client, seed.ClientOwnerEmail);

        var response = await client.PostAsync($"/api/admin/booths/{seed.BoothId}/credentials", null);
        var credentials = await response.Content.ReadFromJsonAsync<BoothCredentialResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(credentials);
        Assert.Equal(seed.BoothCode, credentials.BoothCode);
        Assert.False(string.IsNullOrWhiteSpace(credentials.AgentCredential));
        Assert.False(string.IsNullOrWhiteSpace(credentials.KioskToken));
        Assert.NotEqual(oldAgentCredential, credentials.AgentCredential);

        var oldAgentClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        oldAgentClient.DefaultRequestHeaders.Add("X-Agent-Credential", oldAgentCredential);

        var oldCredentialResponse = await oldAgentClient.GetAsync($"/api/agent/commands/next?boothCode={Uri.EscapeDataString(seed.BoothCode)}");

        Assert.Equal(HttpStatusCode.Unauthorized, oldCredentialResponse.StatusCode);
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

        var planResponse = await client.PostAsJsonAsync("/api/admin/subscription-plans", new
        {
            name = "Growth Plan",
            pricePerBoothCents = 350000,
            currency = "PHP"
        });
        planResponse.EnsureSuccessStatusCode();
        var plan = await planResponse.Content.ReadFromJsonAsync<SubscriptionPlanSummary>();

        Assert.NotNull(plan);

        var clientResponse = await client.PutAsJsonAsync($"/api/admin/clients/{seed.ClientAccountId}", new
        {
            name = "Updated Client",
            status = StatusValues.ClientAccount.Suspended
        });
        var subscriptionResponse = await client.PutAsJsonAsync($"/api/admin/subscriptions/{seed.SubscriptionId}", new
        {
            subscriptionPlanId = plan.Id,
            status = StatusValues.Subscription.Suspended,
            activeBoothAllowance = 1,
            endsOn = (DateOnly?)null,
            notes = "Manual lifecycle update"
        });
        var tooLowAllowanceResponse = await client.PutAsJsonAsync($"/api/admin/subscriptions/{seed.SubscriptionId}", new
        {
            subscriptionPlanId = plan.Id,
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
        Assert.Equal(StatusValues.Subscription.Suspended, updatedSubscription.Status);
        Assert.Equal(plan.Id, updatedSubscription.SubscriptionPlanId);
        Assert.Equal(1, updatedSubscription.ActiveBoothAllowance);
    }

    [Fact]
    public async Task SuspendedClientCanReadButCannotMutateOrUseRuntimeSurfaces()
    {
        await using var factory = new PhotoBizApiFactory();
        var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var boothClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var agentClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "suspended-client-kiosk-token";
        const string agentCredential = "suspended-client-agent-secret";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 2,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken,
            agentCredential: agentCredential,
            ownerEmail: "suspended-owner@photobiz.test");
        await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Paid, StatusValues.LumaboothSessionMode.Print);
        await factory.SetClientStatusAsync(seed.ClientAccountId, StatusValues.ClientAccount.Suspended);

        await LoginAsync(adminClient, seed.ClientOwnerEmail);
        var overviewResponse = await adminClient.GetAsync("/api/admin/overview");
        var mutationResponse = await adminClient.PostAsJsonAsync("/api/admin/locations", new
        {
            clientAccountId = seed.ClientAccountId,
            name = "Blocked Location"
        });

        boothClient.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);
        var config = await boothClient.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");
        var transactionResponse = await boothClient.PostAsJsonAsync("/api/booth-ui/transactions", new { });

        agentClient.DefaultRequestHeaders.Add("X-Agent-Credential", agentCredential);
        var commandResponse = await agentClient.GetAsync($"/api/agent/commands/next?boothCode={Uri.EscapeDataString(seed.BoothCode)}");

        Assert.Equal(HttpStatusCode.OK, overviewResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, mutationResponse.StatusCode);
        Assert.NotNull(config);
        Assert.Null(config.ActiveOffer);
        Assert.Equal("Client account is not active.", config.Session.WelcomeSubtitle);
        Assert.Equal(HttpStatusCode.BadRequest, transactionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, commandResponse.StatusCode);
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
    public async Task InactiveLocationBlocksBoothSetupAndRuntimePaths()
    {
        await using var factory = new PhotoBizApiFactory();
        var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var boothClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var cashierClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var agentClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "inactive-location-kiosk-token";
        const string agentCredential = "inactive-location-agent-secret";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 2,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken,
            agentCredential: agentCredential,
            ownerEmail: "inactive-location-owner@photobiz.test");
        var cashierEmail = await factory.SeedCashierAsync(seed, "inactive-location-cashier@photobiz.test");
        var transactionId = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.PendingCash, StatusValues.LumaboothSessionMode.Print);
        await factory.SetLocationStatusAsync(seed.LocationId, StatusValues.Booth.Inactive);

        await LoginAsync(adminClient, seed.ClientOwnerEmail);
        var createBoothResponse = await adminClient.PostAsJsonAsync("/api/admin/booths", new
        {
            clientAccountId = seed.ClientAccountId,
            locationId = seed.LocationId,
            name = "Blocked Booth",
            code = "BLOCKED-001"
        });
        var updateBoothResponse = await adminClient.PutAsJsonAsync($"/api/admin/booths/{seed.BoothId}", new
        {
            locationId = seed.LocationId,
            name = "Still Blocked Booth",
            code = seed.BoothCode,
            status = StatusValues.Booth.Active
        });

        boothClient.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);
        var config = await boothClient.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");
        var transactionResponse = await boothClient.PostAsJsonAsync("/api/booth-ui/transactions", new { });

        await LoginAsync(cashierClient, cashierEmail);
        var approveResponse = await cashierClient.PostAsJsonAsync($"/api/cashier/transactions/{transactionId}/approve-cash", new { });

        await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Paid, StatusValues.LumaboothSessionMode.Print);
        agentClient.DefaultRequestHeaders.Add("X-Agent-Credential", agentCredential);
        var commandResponse = await agentClient.GetAsync($"/api/agent/commands/next?boothCode={Uri.EscapeDataString(seed.BoothCode)}");

        Assert.Equal(HttpStatusCode.BadRequest, createBoothResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, updateBoothResponse.StatusCode);
        Assert.NotNull(config);
        Assert.Null(config.ActiveOffer);
        Assert.Equal("Booth location is inactive.", config.Session.WelcomeSubtitle);
        Assert.Equal(HttpStatusCode.BadRequest, transactionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, approveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, commandResponse.StatusCode);
    }

    [Fact]
    public async Task InactiveBoothBlocksKioskAndAgentRuntimePaths()
    {
        await using var factory = new PhotoBizApiFactory();
        var boothClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var agentClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "inactive-booth-kiosk-token";
        const string agentCredential = "inactive-booth-agent-secret";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken,
            agentCredential: agentCredential,
            ownerEmail: "inactive-booth-owner@photobiz.test");
        await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Paid, StatusValues.LumaboothSessionMode.Print);
        await factory.SetBoothStatusAsync(seed.BoothId, StatusValues.Booth.Inactive);

        boothClient.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);
        var config = await boothClient.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");
        var transactionResponse = await boothClient.PostAsJsonAsync("/api/booth-ui/transactions", new { });

        agentClient.DefaultRequestHeaders.Add("X-Agent-Credential", agentCredential);
        var commandResponse = await agentClient.GetAsync($"/api/agent/commands/next?boothCode={Uri.EscapeDataString(seed.BoothCode)}");

        Assert.NotNull(config);
        Assert.Null(config.ActiveOffer);
        Assert.Equal(StatusValues.Booth.Offline, config.Booth.State);
        Assert.Equal("Booth is inactive.", config.Session.WelcomeSubtitle);
        Assert.Equal(HttpStatusCode.BadRequest, transactionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, commandResponse.StatusCode);
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
            name = "Cashier",
            email = "cashier3@photobiz.test",
            role = StatusValues.User.Cashier,
            canApproveCash = true,
            canReturnBoothToWelcome = true,
            canCancelTransaction = false
        });
        createUserResponse.EnsureSuccessStatusCode();
        var cashier = await createUserResponse.Content.ReadFromJsonAsync<UserSummary>();
        Assert.NotNull(cashier);
        Assert.False(cashier.CanCancelTransaction);

        var updateUserResponse = await client.PutAsJsonAsync($"/api/admin/users/{cashier.Id}", new
        {
            assignedBoothId = seed.BoothId,
            name = "Updated Cashier",
            email = "cashier3@photobiz.test",
            role = StatusValues.User.Cashier,
            status = StatusValues.User.Inactive,
            canApproveCash = false,
            canReturnBoothToWelcome = true,
            canCancelTransaction = true
        });
        var updatedCashier = await updateUserResponse.Content.ReadFromJsonAsync<UserSummary>();
        var adminAssignmentResponse = await client.PutAsJsonAsync($"/api/admin/users/{cashier.Id}", new
        {
            assignedBoothId = seed.BoothId,
            name = "Updated Cashier",
            email = "cashier3@photobiz.test",
            role = StatusValues.User.ClientAdmin,
            status = StatusValues.User.Active,
            canApproveCash = false,
            canReturnBoothToWelcome = false,
            canCancelTransaction = false
        });
        var disablePaymentResponse = await client.DeleteAsync($"/api/admin/booths/{seed.BoothId}/payment-options/{StatusValues.PaymentMethod.Cash}");

        client.DefaultRequestHeaders.Remove("X-Kiosk-Token");
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", "payment-disable-token");
        var config = await client.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");

        Assert.Equal(HttpStatusCode.OK, updateUserResponse.StatusCode);
        Assert.NotNull(updatedCashier);
        Assert.False(updatedCashier.CanApproveCash);
        Assert.True(updatedCashier.CanReturnBoothToWelcome);
        Assert.True(updatedCashier.CanCancelTransaction);
        Assert.Equal(HttpStatusCode.OK, adminAssignmentResponse.StatusCode);
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
    public async Task AgentPairAndBoothUiLaunchDoNotMarkBoothOnline()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string agentCredential = "agent-pair-secret";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            agentCredential: agentCredential);
        client.DefaultRequestHeaders.Add("X-Agent-Credential", agentCredential);

        var pairResponse = await client.PostAsJsonAsync("/api/agent/pair", new
        {
            boothCode = seed.BoothCode
        });
        var launchResponse = await client.PostAsJsonAsync("/api/agent/booth-ui-launch", new
        {
            boothCode = seed.BoothCode
        });

        pairResponse.EnsureSuccessStatusCode();
        launchResponse.EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
        var booth = await dbContext.Booths.SingleAsync(item => item.Id == seed.BoothId);

        Assert.Null(booth.LastHeartbeatAt);
        Assert.Equal(StatusValues.Booth.Offline, PhotoBizBoothAvailability.GetEffectiveState(booth, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task AgentHeartbeatStoresRuntimeMetadataAndExposesOverviewStatus()
    {
        await using var factory = new PhotoBizApiFactory();
        var agentClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string agentCredential = "agent-heartbeat-secret";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            agentCredential: agentCredential);
        agentClient.DefaultRequestHeaders.Add("X-Agent-Credential", agentCredential);

        var heartbeatResponse = await agentClient.PostAsJsonAsync("/api/agent/heartbeat", new
        {
            boothCode = seed.BoothCode,
            agentVersion = "1.2.3",
            runtimeKind = "ControlCenter",
            kioskRunning = true,
            lumaBoothMode = "Simulator",
            apiReachable = true,
            chromeLaunched = true,
            triggerListenerRunning = true,
            lumaBoothReachable = true
        });
        await LoginAsync(adminClient, seed.ClientOwnerEmail);
        var overview = await adminClient.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");

        heartbeatResponse.EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
        var booth = await dbContext.Booths.SingleAsync(item => item.Id == seed.BoothId);
        var boothSummary = Assert.Single(overview!.Booths, item => item.Id == seed.BoothId);

        Assert.NotNull(booth.LastHeartbeatAt);
        Assert.Equal(StatusValues.Booth.Welcome, booth.CurrentState);
        Assert.Equal("1.2.3", booth.AgentVersion);
        Assert.Equal("ControlCenter", booth.AgentRuntimeKind);
        Assert.True(booth.AgentKioskRunning);
        Assert.Equal("Simulator", booth.AgentLumaBoothMode);
        Assert.Equal(StatusValues.AgentHealth.Ok, booth.AgentHealthStatus);
        Assert.Equal(StatusValues.AgentHealth.Ok, boothSummary.AgentStatus.HealthStatus);
        Assert.Equal(StatusValues.AgentUpdate.Unknown, boothSummary.AgentStatus.UpdateStatus);
        Assert.Equal("1.2.3", boothSummary.AgentStatus.Version);
        Assert.True(boothSummary.AgentStatus.KioskRunning);
    }

    [Fact]
    public async Task AgentOfflineEndpointClearsFreshHeartbeatWithoutCancellingActiveTransaction()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string agentCredential = "agent-offline-secret";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeFreshHeartbeat: true,
            agentCredential: agentCredential);
        var transactionId = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.StartingSession, StatusValues.LumaboothSessionMode.Print);
        await factory.SetBoothStateAsync(seed.BoothId, StatusValues.Booth.StartingLumabooth);
        client.DefaultRequestHeaders.Add("X-Agent-Credential", agentCredential);

        var response = await client.PostAsJsonAsync("/api/agent/offline", new
        {
            boothCode = seed.BoothCode
        });

        response.EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
        var booth = await dbContext.Booths.SingleAsync(item => item.Id == seed.BoothId);
        var transaction = await dbContext.Transactions.SingleAsync(item => item.Id == transactionId);

        Assert.Null(booth.LastHeartbeatAt);
        Assert.False(booth.AgentKioskRunning);
        Assert.Equal(StatusValues.AgentHealth.Offline, booth.AgentHealthStatus);
        Assert.Equal(StatusValues.Booth.StartingLumabooth, booth.CurrentState);
        Assert.Equal(StatusValues.Booth.Offline, PhotoBizBoothAvailability.GetEffectiveState(booth, DateTimeOffset.UtcNow));
        Assert.Equal(StatusValues.Transaction.StartingSession, transaction.Status);
        Assert.Null(transaction.CancelledAt);
    }

    [Fact]
    public async Task AgentCanRequestFreshBoothUiLaunchToken()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string agentCredential = "agent-launch-secret";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            agentCredential: agentCredential);
        client.DefaultRequestHeaders.Add("X-Agent-Credential", agentCredential);

        var response = await client.PostAsJsonAsync("/api/agent/booth-ui-launch", new
        {
            boothCode = seed.BoothCode
        });
        var launch = await response.Content.ReadFromJsonAsync<AgentBoothUiLaunchResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(launch);
        Assert.Equal(seed.BoothCode, launch.BoothCode);
        Assert.False(string.IsNullOrWhiteSpace(launch.KioskToken));

        var boothClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        boothClient.DefaultRequestHeaders.Add("X-Kiosk-Token", launch.KioskToken);
        var config = await boothClient.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");

        Assert.NotNull(config);
        Assert.Equal(seed.BoothId, config.Booth.Id);
    }

    [Fact]
    public async Task BoothUiUnavailableConfigUsesBoothAppearanceAndClientBranding()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "unavailable-branded-kiosk-token";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken);
        await factory.SetBoothAppearanceAsync(
            seed.BoothId,
            StatusValues.Theme.Pop,
            "SM Southmall - Neon Weekend",
            "Pop In. Pose Big.",
            "Ask the cashier to activate today's package.");
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);

        var config = await client.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");

        Assert.NotNull(config);
        Assert.Null(config.ActiveOffer);
        Assert.Equal($"Client {seed.ClientAccountId:N}", config.Client.DisplayName);
        Assert.Equal(StatusValues.Theme.Pop, config.Theme.Preset);
        Assert.Equal("SM Southmall - Neon Weekend", config.Session.Label);
        Assert.Equal("Booth unavailable", config.Session.WelcomeHeadline);
        Assert.Equal("No active booth offer configured", config.Session.WelcomeSubtitle);
    }

    [Fact]
    public async Task BoothUiConfigIncludesActiveTransactionAndCompletionMessage()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "active-transaction-kiosk-token";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken);
        await factory.SetBoothAppearanceAsync(
            seed.BoothId,
            StatusValues.Theme.Vintage,
            "SM Manila",
            "Ready?",
            "Tap start.",
            "Thanks for sharing your smile.");
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);

        var transactionResponse = await client.PostAsJsonAsync("/api/booth-ui/transactions", new { });
        transactionResponse.EnsureSuccessStatusCode();
        var transaction = await transactionResponse.Content.ReadFromJsonAsync<TransactionSummary>();
        var config = await client.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");

        Assert.NotNull(transaction);
        Assert.NotNull(config);
        Assert.NotNull(config.ActiveTransaction);
        Assert.Equal(transaction.Id, config.ActiveTransaction.Id);
        Assert.Equal(transaction.TransactionNumber, config.ActiveTransaction.TransactionNumber);
        Assert.Equal(StatusValues.Transaction.Created, config.ActiveTransaction.Status);
        Assert.Equal(StatusValues.PaymentMethod.Cash, config.ActiveTransaction.PaymentMethod);
        Assert.Equal(25000, config.ActiveTransaction.AmountCents);
        Assert.Equal("PHP", config.ActiveTransaction.Currency);
        Assert.Equal(transaction.CreatedAt, config.ActiveTransaction.CreatedAt);
        Assert.Equal(transaction.CreatedAt.AddMinutes(1), config.ActiveTransaction.ExpiresAt);
        Assert.Equal("Booth 1", config.Booth.Name);
        Assert.Equal(seed.BoothCode, config.Booth.Code);
        Assert.Equal($"Location {seed.LocationId:N}", config.Booth.LocationName);
        Assert.Equal("Thanks for sharing your smile.", config.Session.CompletionThankYouMessage);
    }

    [Fact]
    public async Task BoothUiCanCancelCreatedAndPendingCashTransactions()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "kiosk-cancel-token";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken);
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);

        var createdResponse = await client.PostAsJsonAsync("/api/booth-ui/transactions", new { });
        createdResponse.EnsureSuccessStatusCode();
        var created = await createdResponse.Content.ReadFromJsonAsync<TransactionSummary>();
        Assert.NotNull(created);

        var createdCancelResponse = await client.PostAsJsonAsync($"/api/booth-ui/transactions/{created.Id}/cancel", new
        {
            trigger = StatusValues.BoothUiCancelTrigger.BackButton
        });
        createdCancelResponse.EnsureSuccessStatusCode();
        var createdRecord = await factory.LoadTransactionRecordAsync(created.Id);
        var createdAuditCount = await factory.CountAuditLogsAsync(seed.ClientAccountId, "transaction.kiosk_cancelled");
        var createdConfig = await client.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");
        Assert.NotNull(createdConfig);

        var pendingResponse = await client.PostAsJsonAsync("/api/booth-ui/transactions", new { });
        pendingResponse.EnsureSuccessStatusCode();
        var pending = await pendingResponse.Content.ReadFromJsonAsync<TransactionSummary>();
        Assert.NotNull(pending);
        var paymentResponse = await client.PostAsJsonAsync($"/api/booth-ui/transactions/{pending.Id}/payment-method", new
        {
            method = StatusValues.PaymentMethod.Cash
        });
        paymentResponse.EnsureSuccessStatusCode();

        var pendingCancelResponse = await client.PostAsJsonAsync($"/api/booth-ui/transactions/{pending.Id}/cancel", new
        {
            trigger = StatusValues.BoothUiCancelTrigger.BackButton
        });
        pendingCancelResponse.EnsureSuccessStatusCode();
        var pendingRecord = await factory.LoadTransactionRecordAsync(pending.Id);
        var pendingAuditCount = await factory.CountAuditLogsAsync(seed.ClientAccountId, "transaction.kiosk_cancelled");

        Assert.Equal(StatusValues.Transaction.Cancelled, createdRecord.Status);
        Assert.Equal(StatusValues.Booth.Welcome, createdRecord.BoothState);
        Assert.Equal("Customer cancelled at the booth.", createdRecord.FailureReason);
        Assert.Equal(StatusValues.CancellationActor.BoothUser, createdRecord.CancelledByActorType);
        Assert.Null(createdRecord.CancelledByUserId);
        Assert.Equal(StatusValues.CancellationSource.BoothUiPaymentOptionsBack, createdRecord.CancellationSource);
        Assert.Equal(StatusValues.Transaction.Created, createdRecord.CancellationPreviousStatus);
        Assert.Null(createdConfig.RecentTransaction);
        Assert.Equal(1, createdAuditCount);
        Assert.Equal(StatusValues.Transaction.Cancelled, pendingRecord.Status);
        Assert.Equal(StatusValues.Booth.Welcome, pendingRecord.BoothState);
        Assert.Equal("Customer cancelled at the booth.", pendingRecord.FailureReason);
        Assert.Equal(StatusValues.CancellationActor.BoothUser, pendingRecord.CancelledByActorType);
        Assert.Null(pendingRecord.CancelledByUserId);
        Assert.Equal(StatusValues.CancellationSource.BoothUiWaitingForPaymentBack, pendingRecord.CancellationSource);
        Assert.Equal(StatusValues.Transaction.PendingCash, pendingRecord.CancellationPreviousStatus);
        Assert.Equal(2, pendingAuditCount);
    }

    [Fact]
    public async Task BoothUiIdleCancelTracksPaymentOptionsTimeout()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "kiosk-idle-cancel-token";
        await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken);
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);

        var createdResponse = await client.PostAsJsonAsync("/api/booth-ui/transactions", new { });
        createdResponse.EnsureSuccessStatusCode();
        var transaction = await createdResponse.Content.ReadFromJsonAsync<TransactionSummary>();
        Assert.NotNull(transaction);

        var cancelResponse = await client.PostAsJsonAsync($"/api/booth-ui/transactions/{transaction.Id}/cancel", new
        {
            trigger = StatusValues.BoothUiCancelTrigger.IdleTimeout
        });
        cancelResponse.EnsureSuccessStatusCode();

        var record = await factory.LoadTransactionRecordAsync(transaction.Id);

        Assert.Equal(StatusValues.Transaction.Cancelled, record.Status);
        Assert.Equal(StatusValues.CancellationActor.BoothUser, record.CancelledByActorType);
        Assert.Null(record.CancelledByUserId);
        Assert.Equal(StatusValues.CancellationSource.BoothUiPaymentOptionsIdleTimeout, record.CancellationSource);
        Assert.Equal(StatusValues.Transaction.Created, record.CancellationPreviousStatus);
    }

    [Fact]
    public async Task BoothUiConfigExpiresOverduePendingCashTransaction()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "kiosk-config-expire-token";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken);
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);

        var transactionResponse = await client.PostAsJsonAsync("/api/booth-ui/transactions", new { });
        transactionResponse.EnsureSuccessStatusCode();
        var transaction = await transactionResponse.Content.ReadFromJsonAsync<TransactionSummary>();
        Assert.NotNull(transaction);

        var paymentResponse = await client.PostAsJsonAsync($"/api/booth-ui/transactions/{transaction.Id}/payment-method", new
        {
            method = StatusValues.PaymentMethod.Cash
        });
        paymentResponse.EnsureSuccessStatusCode();
        await factory.MarkTransactionExpiredDueAsync(transaction.Id);

        var config = await client.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");
        var record = await factory.LoadTransactionRecordAsync(transaction.Id);

        Assert.NotNull(config);
        Assert.Null(config.ActiveTransaction);
        Assert.NotNull(config.RecentTransaction);
        Assert.Equal(transaction.Id, config.RecentTransaction.Id);
        Assert.Equal(StatusValues.Transaction.Expired, config.RecentTransaction.Status);
        Assert.Equal(StatusValues.Transaction.Expired, record.Status);
        Assert.Equal(StatusValues.Booth.Welcome, record.BoothState);
    }

    [Fact]
    public async Task BoothUiCancelRejectsWrongBoothAndPaidTransactions()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "kiosk-cancel-scope-token";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken);
        var otherSeed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: "other-kiosk-cancel-token",
            ownerEmail: "cancel-other-owner@photobiz.test");
        var paidTransactionId = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Paid, StatusValues.LumaboothSessionMode.Print);
        var otherTransactionId = await factory.SeedSessionTransactionAsync(otherSeed, StatusValues.Transaction.PendingCash, StatusValues.LumaboothSessionMode.Print);
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);

        var paidCancelResponse = await client.PostAsJsonAsync($"/api/booth-ui/transactions/{paidTransactionId}/cancel", new
        {
            trigger = StatusValues.BoothUiCancelTrigger.BackButton
        });
        var wrongBoothCancelResponse = await client.PostAsJsonAsync($"/api/booth-ui/transactions/{otherTransactionId}/cancel", new
        {
            trigger = StatusValues.BoothUiCancelTrigger.BackButton
        });

        Assert.Equal(HttpStatusCode.BadRequest, paidCancelResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongBoothCancelResponse.StatusCode);
    }

    [Fact]
    public async Task BoothUiAcknowledgesTerminalPaymentOutcomesFromBackend()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "terminal-ack-kiosk-token";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken);
        var transactionId = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.PaymentFailed, StatusValues.LumaboothSessionMode.Print);
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);

        var configBefore = await client.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");
        var ackResponse = await client.PostAsJsonAsync($"/api/booth-ui/recent-transactions/{transactionId}/acknowledge", new { });
        var configAfter = await client.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");

        Assert.NotNull(configBefore);
        Assert.Equal(transactionId, configBefore.RecentTransaction?.Id);
        Assert.Equal(HttpStatusCode.OK, ackResponse.StatusCode);
        Assert.NotNull(configAfter);
        Assert.Null(configAfter.RecentTransaction);
    }

    [Fact]
    public async Task BoothUiCanReturnCompletedPromptToWelcome()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "completed-return-kiosk-token";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken);
        _ = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Completed, StatusValues.LumaboothSessionMode.Print);
        await factory.SetBoothStateAsync(seed.BoothId, StatusValues.Booth.Completed);
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);

        var response = await client.PostAsJsonAsync("/api/booth-ui/return-to-welcome", new { });
        var config = await client.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(config);
        Assert.Equal(StatusValues.Booth.Welcome, config.Booth.State);
    }

    [Fact]
    public async Task BoothUiReturnToWelcomeIsIdempotentAfterPromptAutoReset()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "completed-idempotent-kiosk-token";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken);
        _ = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Completed, StatusValues.LumaboothSessionMode.Print);
        await factory.SetBoothStateAsync(seed.BoothId, StatusValues.Booth.Welcome);
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);

        var response = await client.PostAsJsonAsync("/api/booth-ui/return-to-welcome", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BoothUiCanReturnCompletedCoveredPlanSessionToWelcome()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "covered-return-kiosk-token";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken);
        _ = await factory.SeedSessionTransactionAsync(
            seed,
            StatusValues.Transaction.Completed,
            StatusValues.LumaboothSessionMode.Print,
            StatusValues.OfferType.TimeUnlimited,
            StatusValues.TransactionType.CoveredPlanSession);
        await factory.SetBoothStateAsync(seed.BoothId, StatusValues.Booth.Completed);
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);

        var response = await client.PostAsJsonAsync("/api/booth-ui/return-to-welcome", new { });
        var config = await client.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(config);
        Assert.Equal(StatusValues.Booth.Welcome, config.Booth.State);
    }

    [Fact]
    public async Task BoothUiReturnToWelcomeIgnoresHistoricalFailedTransactions()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "failed-history-return-kiosk-token";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken);
        _ = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Completed, StatusValues.LumaboothSessionMode.Print);
        _ = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.SessionFailed, StatusValues.LumaboothSessionMode.Print);
        _ = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.PaymentFailed, StatusValues.LumaboothSessionMode.Print);
        await factory.SetBoothStateAsync(seed.BoothId, StatusValues.Booth.Completed);
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);

        var response = await client.PostAsJsonAsync("/api/booth-ui/return-to-welcome", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BoothUiReturnToWelcomeRejectsActiveAddOnAfterCompletedSession()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "addon-active-return-kiosk-token";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeOffer: true,
            includeActivation: true,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken);
        var parentTransactionId = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Completed, StatusValues.LumaboothSessionMode.Print);
        await factory.SeedBoothTransactionAsync(
            seed,
            StatusValues.TransactionType.ExtraPrintAddOn,
            StatusValues.Transaction.PendingCash,
            parentTransactionId);
        await factory.SetBoothStateAsync(seed.BoothId, StatusValues.Booth.Completed);
        client.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);

        var response = await client.PostAsJsonAsync("/api/booth-ui/return-to-welcome", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CashierActivatesSessionCountPackageAndCoveredSessionsSkipPayment()
    {
        await using var factory = new PhotoBizApiFactory();
        var ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var cashierClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var boothClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var agentClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string kioskToken = "session-count-kiosk-token";
        const string agentCredential = "session-count-agent-secret";
        var seed = await factory.SeedClientSetupAsync(
            StatusValues.Subscription.Active,
            activeBoothAllowance: 1,
            existingActiveBooths: 1,
            includeAppearance: true,
            includeFreshHeartbeat: true,
            kioskToken: kioskToken,
            agentCredential: agentCredential);
        var cashierEmail = await factory.SeedCashierAsync(seed, "cashier-plan-activation@photobiz.test");

        await LoginAsync(ownerClient, seed.ClientOwnerEmail);
        var offerResponse = await ownerClient.PostAsJsonAsync("/api/admin/offers", new
        {
            clientAccountId = seed.ClientAccountId,
            name = "Five Session Pass",
            description = "Session-count booth pass",
            offerType = StatusValues.OfferType.SessionCount,
            priceCents = 150000,
            currency = "PHP",
            includedPrintEntitlement = StatusValues.PrintEntitlement.TwoBySixOrOneByFour,
            durationHours = (int?)null,
            sessionAllowance = 2,
            allowsExtraPrintAddOn = false,
            extraPrintPriceCents = (int?)null,
            lumaboothSessionMode = StatusValues.LumaboothSessionMode.Print
        });
        offerResponse.EnsureSuccessStatusCode();
        var offer = await offerResponse.Content.ReadFromJsonAsync<OfferSummary>();
        Assert.NotNull(offer);

        var activationResponse = await ownerClient.PostAsJsonAsync($"/api/admin/booths/{seed.BoothId}/activate-offer", new
        {
            boothOfferId = offer.Id
        });
        activationResponse.EnsureSuccessStatusCode();
        var pendingActivation = await activationResponse.Content.ReadFromJsonAsync<OfferActivationSummary>();
        Assert.NotNull(pendingActivation);
        Assert.Equal(StatusValues.OfferActivation.PendingPayment, pendingActivation.Status);

        boothClient.DefaultRequestHeaders.Add("X-Kiosk-Token", kioskToken);
        var pendingConfig = await boothClient.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");
        Assert.NotNull(pendingConfig);
        Assert.Equal(StatusValues.OfferActivation.PendingPayment, pendingConfig.ActiveOffer?.ActivationStatus);
        Assert.Empty(pendingConfig.PaymentOptions);

        await LoginAsync(cashierClient, cashierEmail);
        var planActivationResponse = await cashierClient.PostAsJsonAsync($"/api/cashier/booths/{seed.BoothId}/plan-activation", new { });
        planActivationResponse.EnsureSuccessStatusCode();
        var planActivationTransaction = await planActivationResponse.Content.ReadFromJsonAsync<TransactionSummary>();
        Assert.NotNull(planActivationTransaction);
        Assert.Equal(StatusValues.TransactionType.PlanActivation, planActivationTransaction.TransactionType);
        Assert.Equal(StatusValues.Transaction.PendingCash, planActivationTransaction.Status);
        Assert.Equal(150000, planActivationTransaction.AmountCents);
        Assert.Equal("Five Session Pass", planActivationTransaction.OfferName);
        Assert.Equal(StatusValues.OfferType.SessionCount, planActivationTransaction.OfferType);
        Assert.Equal(StatusValues.PrintEntitlement.TwoBySixOrOneByFour, planActivationTransaction.IncludedPrintEntitlement);
        Assert.Equal(2, planActivationTransaction.SessionAllowance);

        var approvalResponse = await cashierClient.PostAsJsonAsync($"/api/cashier/transactions/{planActivationTransaction.Id}/approve-cash", new { });
        approvalResponse.EnsureSuccessStatusCode();
        agentClient.DefaultRequestHeaders.Add("X-Agent-Credential", agentCredential);
        var noPlanCommand = await agentClient.GetAsync($"/api/agent/commands/next?boothCode={Uri.EscapeDataString(seed.BoothCode)}");
        Assert.Equal(HttpStatusCode.NoContent, noPlanCommand.StatusCode);

        var firstCoveredSessionResponse = await boothClient.PostAsJsonAsync("/api/booth-ui/transactions", new { });
        firstCoveredSessionResponse.EnsureSuccessStatusCode();
        var firstCoveredSession = await firstCoveredSessionResponse.Content.ReadFromJsonAsync<TransactionSummary>();
        Assert.NotNull(firstCoveredSession);
        Assert.Equal(StatusValues.TransactionType.CoveredPlanSession, firstCoveredSession.TransactionType);
        Assert.Equal(StatusValues.Transaction.Paid, firstCoveredSession.Status);
        Assert.Equal(0, firstCoveredSession.AmountCents);
        Assert.Equal("Five Session Pass", firstCoveredSession.OfferName);
        Assert.Equal(StatusValues.OfferType.SessionCount, firstCoveredSession.OfferType);
        Assert.Equal(StatusValues.PrintEntitlement.TwoBySixOrOneByFour, firstCoveredSession.IncludedPrintEntitlement);
        Assert.Equal(2, firstCoveredSession.SessionAllowance);

        var commandResponse = await agentClient.GetAsync($"/api/agent/commands/next?boothCode={Uri.EscapeDataString(seed.BoothCode)}");
        commandResponse.EnsureSuccessStatusCode();
        var command = await commandResponse.Content.ReadFromJsonAsync<AgentCommandResponse>();
        Assert.NotNull(command);
        Assert.Equal("START_SESSION", command.Command);
        Assert.Equal(StatusValues.TransactionType.CoveredPlanSession, command.TransactionType);

        var startedResponse = await agentClient.PostAsJsonAsync($"/api/agent/transactions/{firstCoveredSession.Id}/session-started", new
        {
            boothCode = seed.BoothCode
        });
        var completedResponse = await agentClient.PostAsJsonAsync($"/api/agent/transactions/{firstCoveredSession.Id}/session-completed", new
        {
            boothCode = seed.BoothCode
        });
        startedResponse.EnsureSuccessStatusCode();
        completedResponse.EnsureSuccessStatusCode();

        var overview = await cashierClient.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");
        Assert.NotNull(overview);
        var activeActivation = Assert.Single(overview.Activations, item => item.Id == pendingActivation.Id);
        Assert.Equal(StatusValues.OfferActivation.Active, activeActivation.Status);
        Assert.Equal(1, activeActivation.SessionsUsed);
        var firstCoveredSessionSummary = Assert.Single(overview.Transactions, item => item.Id == firstCoveredSession.Id);
        Assert.Equal(1, firstCoveredSessionSummary.CoveredSessionSequence);

        var firstReturnResponse = await boothClient.PostAsJsonAsync("/api/booth-ui/return-to-welcome", new { });
        firstReturnResponse.EnsureSuccessStatusCode();

        var secondCoveredSessionResponse = await boothClient.PostAsJsonAsync("/api/booth-ui/transactions", new { });
        secondCoveredSessionResponse.EnsureSuccessStatusCode();
        var secondCoveredSession = await secondCoveredSessionResponse.Content.ReadFromJsonAsync<TransactionSummary>();
        Assert.NotNull(secondCoveredSession);
        var secondCommandResponse = await agentClient.GetAsync($"/api/agent/commands/next?boothCode={Uri.EscapeDataString(seed.BoothCode)}");
        secondCommandResponse.EnsureSuccessStatusCode();

        var secondStartedResponse = await agentClient.PostAsJsonAsync($"/api/agent/transactions/{secondCoveredSession.Id}/session-started", new
        {
            boothCode = seed.BoothCode
        });
        var secondCompletedResponse = await agentClient.PostAsJsonAsync($"/api/agent/transactions/{secondCoveredSession.Id}/session-completed", new
        {
            boothCode = seed.BoothCode
        });
        secondStartedResponse.EnsureSuccessStatusCode();
        secondCompletedResponse.EnsureSuccessStatusCode();

        overview = await cashierClient.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");
        Assert.NotNull(overview);
        var completedActivation = Assert.Single(overview.Activations, item => item.Id == pendingActivation.Id);
        Assert.Equal(StatusValues.OfferActivation.Completed, completedActivation.Status);
        Assert.Equal(2, completedActivation.SessionsUsed);
        Assert.Equal(1, Assert.Single(overview.Transactions, item => item.Id == firstCoveredSession.Id).CoveredSessionSequence);
        Assert.Equal(2, Assert.Single(overview.Transactions, item => item.Id == secondCoveredSession.Id).CoveredSessionSequence);

        var exhaustedResponse = await boothClient.PostAsJsonAsync("/api/booth-ui/transactions", new { });
        Assert.Equal(HttpStatusCode.BadRequest, exhaustedResponse.StatusCode);
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
        Assert.Equal("Per Session", addOn.OfferName);
        Assert.Equal(StatusValues.OfferType.PerSession, addOn.OfferType);
        Assert.Equal(StatusValues.PrintEntitlement.TwoBySixOrOneByFour, addOn.IncludedPrintEntitlement);

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

    [Fact]
    public async Task ExtraPrintAddOnAllowsPreviousSessionBeforeCurrentSessionReachesLumabooth()
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
        var cashierEmail = await factory.SeedCashierAsync(seed, "cashier-extra-print-current-pending@photobiz.test");
        var previousTransactionId = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Completed, StatusValues.LumaboothSessionMode.Print);
        await factory.BackdateCompletedTransactionAsync(previousTransactionId, TimeSpan.FromMinutes(5));
        var completedAddOnId = await factory.SeedBoothTransactionAsync(
            seed,
            StatusValues.TransactionType.ExtraPrintAddOn,
            StatusValues.Transaction.Completed,
            previousTransactionId);
        await factory.BackdateCompletedTransactionAsync(completedAddOnId, TimeSpan.FromMinutes(3));
        _ = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.PendingCash, StatusValues.LumaboothSessionMode.Print);
        await LoginAsync(client, cashierEmail);

        var response = await client.PostAsJsonAsync($"/api/cashier/transactions/{previousTransactionId}/extra-prints", new
        {
            copyCount = 1
        });

        response.EnsureSuccessStatusCode();
        var addOn = await response.Content.ReadFromJsonAsync<TransactionSummary>();
        Assert.NotNull(addOn);
        Assert.Equal(previousTransactionId, addOn.ParentTransactionId);
        Assert.Equal(StatusValues.TransactionType.ExtraPrintAddOn, addOn.TransactionType);
    }

    [Fact]
    public async Task ExtraPrintAddOnRejectsOlderEligibleSessionWhenPreviousTransactionIsIneligible()
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
        var cashierEmail = await factory.SeedCashierAsync(seed, "cashier-extra-print-previous-ineligible@photobiz.test");
        var olderEligibleTransactionId = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.Completed, StatusValues.LumaboothSessionMode.Print);
        await factory.BackdateCompletedTransactionAsync(olderEligibleTransactionId, TimeSpan.FromMinutes(10));
        var previousCoveredTransactionId = await factory.SeedSessionTransactionAsync(
            seed,
            StatusValues.Transaction.Completed,
            StatusValues.LumaboothSessionMode.Print,
            StatusValues.OfferType.SessionCount,
            StatusValues.TransactionType.CoveredPlanSession);
        await factory.BackdateCompletedTransactionAsync(previousCoveredTransactionId, TimeSpan.FromMinutes(5));
        _ = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.PendingCash, StatusValues.LumaboothSessionMode.Print);
        await LoginAsync(client, cashierEmail);

        var response = await client.PostAsJsonAsync($"/api/cashier/transactions/{olderEligibleTransactionId}/extra-prints", new
        {
            copyCount = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Extra prints are available only for the previous booth transaction.", body, StringComparison.Ordinal);
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
        Assert.Equal(StatusValues.CancellationActor.Cashier, recovered.CancelledByActorType);
        Assert.Equal(StatusValues.CancellationSource.CashierPosReturnToWelcome, recovered.CancellationSource);
        Assert.Equal(StatusValues.Transaction.Created, recovered.CancellationPreviousStatus);

        var config = await boothClient.GetFromJsonAsync<BoothConfigResponse>("/api/booth-ui/config");
        Assert.NotNull(config);
        Assert.NotNull(config.RecentTransaction);
        Assert.Equal(firstTransactionBody.Id, config.RecentTransaction.Id);
        Assert.Equal(StatusValues.Transaction.Cancelled, config.RecentTransaction.Status);
        Assert.Equal(StatusValues.TransactionType.SessionPurchase, config.RecentTransaction.TransactionType);
        Assert.Equal("Manual booth recovery returned the booth to welcome.", config.RecentTransaction.Reason);
        Assert.Equal(StatusValues.CancellationActor.Cashier, config.RecentTransaction.CancelledByActorType);
        Assert.Equal(StatusValues.CancellationSource.CashierPosReturnToWelcome, config.RecentTransaction.CancellationSource);
        Assert.Equal(StatusValues.Transaction.Created, config.RecentTransaction.CancellationPreviousStatus);

        var secondTransaction = await boothClient.PostAsJsonAsync("/api/booth-ui/transactions", new { });
        secondTransaction.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CashierEndpointsRespectSavedPermissions()
    {
        await using var factory = new PhotoBizApiFactory();
        var cashierClient = factory.CreateClient(new WebApplicationFactoryClientOptions
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
        var cashierEmail = await factory.SeedCashierAsync(
            seed,
            "cashier-permissions@photobiz.test",
            canApproveCash: false,
            canReturnBoothToWelcome: false,
            canCancelTransaction: false);
        var transactionId = await factory.SeedSessionTransactionAsync(seed, StatusValues.Transaction.PendingCash, StatusValues.LumaboothSessionMode.Print);
        await LoginAsync(cashierClient, cashierEmail);

        var approveResponse = await cashierClient.PostAsJsonAsync($"/api/cashier/transactions/{transactionId}/approve-cash", new { });
        var cancelResponse = await cashierClient.PostAsJsonAsync($"/api/cashier/transactions/{transactionId}/cancel", new { });
        var returnResponse = await cashierClient.PostAsJsonAsync($"/api/cashier/booths/{seed.BoothId}/return-to-welcome", new { });

        Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancelResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, returnResponse.StatusCode);
    }

    [Fact]
    public async Task CashierCancelTransactionTracksActorAndSource()
    {
        await using var factory = new PhotoBizApiFactory();
        var cashierClient = factory.CreateClient(new WebApplicationFactoryClientOptions
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
        var cashierEmail = await factory.SeedCashierAsync(seed, "cashier-cancel@photobiz.test");
        var cashierId = await factory.LoadUserIdByEmailAsync(cashierEmail);
        var transactionId = await factory.SeedSessionTransactionAsync(
            seed,
            StatusValues.Transaction.PendingCash,
            StatusValues.LumaboothSessionMode.Print);
        await LoginAsync(cashierClient, cashierEmail);

        var cancelResponse = await cashierClient.PostAsJsonAsync($"/api/cashier/transactions/{transactionId}/cancel", new { });
        cancelResponse.EnsureSuccessStatusCode();

        var record = await factory.LoadTransactionRecordAsync(transactionId);

        Assert.Equal(StatusValues.Transaction.Cancelled, record.Status);
        Assert.Equal(StatusValues.CancellationActor.Cashier, record.CancelledByActorType);
        Assert.Equal(cashierId, record.CancelledByUserId);
        Assert.Equal(StatusValues.CancellationSource.CashierPosCancelTransaction, record.CancellationSource);
        Assert.Equal(StatusValues.Transaction.PendingCash, record.CancellationPreviousStatus);
    }

    [Fact]
    public async Task ChangePasswordValidatesRequestAndUpdatesCredentials()
    {
        await using var factory = new PhotoBizApiFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var seed = await factory.SeedClientSetupAsync(StatusValues.Subscription.Active, activeBoothAllowance: 1);
        await LoginAsync(client, seed.ClientOwnerEmail);

        var wrongCurrentResponse = await client.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = "wrong-password",
            newPassword = "Updated123!",
            confirmPassword = "Updated123!"
        });
        var mismatchResponse = await client.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = PhotoBizApiFactory.Password,
            newPassword = "Updated123!",
            confirmPassword = "Different123!"
        });
        var shortPasswordResponse = await client.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = PhotoBizApiFactory.Password,
            newPassword = "short",
            confirmPassword = "short"
        });
        var samePasswordResponse = await client.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = PhotoBizApiFactory.Password,
            newPassword = PhotoBizApiFactory.Password,
            confirmPassword = PhotoBizApiFactory.Password
        });
        var changedSession = await ChangePasswordAsync(
            client,
            PhotoBizApiFactory.Password,
            "Updated123!",
            "Updated123!");

        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, mismatchResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, shortPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, samePasswordResponse.StatusCode);
        Assert.False(changedSession.MustChangePassword);

        var oldPasswordClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var oldPasswordResponse = await oldPasswordClient.PostAsJsonAsync("/api/auth/login", new
        {
            email = seed.ClientOwnerEmail,
            password = PhotoBizApiFactory.Password
        });
        Assert.Equal(HttpStatusCode.BadRequest, oldPasswordResponse.StatusCode);

        var newPasswordClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        await LoginAsync(newPasswordClient, seed.ClientOwnerEmail, "Updated123!");
        var overview = await newPasswordClient.GetFromJsonAsync<AdminOverviewResponse>("/api/admin/overview");

        Assert.NotNull(overview);
        Assert.Contains(overview.AuditLogs, item => item.Action == "user.password_changed");
    }

    private static async Task LoginAsync(HttpClient client, string email, string password = PhotoBizApiFactory.Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });
        response.EnsureSuccessStatusCode();

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookies.Select(cookie => cookie.Split(';', 2)[0]));
    }

    private static async Task<AuthSessionResponse> ChangePasswordAsync(
        HttpClient client,
        string currentPassword,
        string newPassword,
        string confirmPassword)
    {
        var response = await client.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword,
            newPassword,
            confirmPassword
        });
        response.EnsureSuccessStatusCode();

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookies.Select(cookie => cookie.Split(';', 2)[0]));

        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>();

        Assert.NotNull(session);
        return session;
    }

    private static string SignWebhook(string timestamp, string payload, string webhookSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"))).ToLowerInvariant();
    }

    private sealed class FakePayMongoClient : IPayMongoClient
    {
        public const string PaymentIntentId = "pi_test_photobiz";

        public Task VerifyCredentialsAsync(PayMongoCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<PayMongoQrPaymentResult> CreateQrPhPaymentAsync(
            PayMongoCredentials credentials,
            Transaction transaction,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new PayMongoQrPaymentResult(
                PaymentIntentId,
                "pm_test_qrph",
                "data:image/png;base64,cXJwaA==",
                """
                {
                  "data": {
                    "id": "pi_test_photobiz",
                    "attributes": {
                      "next_action": {
                        "code": {
                          "image_url": "data:image/png;base64,cXJwaA=="
                        }
                      }
                    }
                  }
                }
                """,
                DateTimeOffset.UtcNow.AddMinutes(30)));
        }
    }

    private sealed class PhotoBizApiFactory : WebApplicationFactory<Program>
    {
        public const string Password = "Test12345!";
        public const string DefaultInitialPassword = "PhotoBIZ!123";
        public const string PayMongoWebhookSecret = "whsec_test";

        private readonly string databaseName = $"photobiz-api-tests-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Database=photobiz_tests;Username=test;Password=test");
            builder.UseSetting("ForwardedHeaders:Enabled", "true");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<PhotoBizDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<PhotoBizDbContext>>();
                services.RemoveAll<IPayMongoClient>();
                services.AddDbContext<PhotoBizDbContext>(options =>
                {
                    options.UseInMemoryDatabase(databaseName);
                });
                services.AddSingleton<IPayMongoClient, FakePayMongoClient>();
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

        public async Task<Guid> SeedVerifiedPayMongoAsync(Guid clientAccountId)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var secretProtector = scope.ServiceProvider.GetRequiredService<PhotoBizSecretProtector>();
            var id = Guid.NewGuid();
            dbContext.ClientPaymentProviderConfigs.Add(new ClientPaymentProviderConfig
            {
                Id = id,
                ClientAccountId = clientAccountId,
                Provider = StatusValues.PaymentProvider.PayMongo,
                IntegrationType = StatusValues.PaymentMethod.PayMongoQrPh,
                Status = StatusValues.PaymentResource.Verified,
                BusinessAccountName = "PayMongo Test",
                PaymentMode = StatusValues.PaymentMode.Test,
                PublicKeyMasked = "pk_test_...1234",
                EncryptedSecretKey = secretProtector.Protect("sk_test_1234"),
                EncryptedWebhookSecret = secretProtector.Protect(PayMongoWebhookSecret),
                WebhookUrl = $"https://localhost/api/payments/paymongo/webhooks/{id}",
                VerifiedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();

            return id;
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
                        DefaultWelcomeSubtitle = "Test subtitle",
                        CompletionThankYouMessage = "Thanks for sharing your smile."
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

        public async Task<string> SeedCashierAsync(
            SeedResult seed,
            string email,
            bool canApproveCash = true,
            bool canReturnBoothToWelcome = true,
            bool canCancelTransaction = true)
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
                CanApproveCash = canApproveCash,
                CanReturnBoothToWelcome = canReturnBoothToWelcome,
                CanCancelTransaction = canCancelTransaction,
                CreatedAt = DateTimeOffset.UtcNow
            };
            cashier.PasswordHash = passwordHasher.HashPassword(cashier, Password);
            dbContext.Users.Add(cashier);
            await dbContext.SaveChangesAsync();

            return email;
        }

        public async Task<Guid> LoadUserIdByEmailAsync(string email)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            return await dbContext.Users
                .Where(user => user.Email == email)
                .Select(user => user.Id)
                .SingleAsync();
        }

        public async Task<Guid> SeedSessionTransactionAsync(
            SeedResult seed,
            string status,
            string lumaboothSessionMode,
            string offerType = StatusValues.OfferType.PerSession,
            string transactionType = StatusValues.TransactionType.SessionPurchase)
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
                TransactionType = transactionType,
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

        public async Task<Guid> SeedBoothTransactionAsync(
            SeedResult seed,
            string transactionType,
            string status,
            Guid? parentTransactionId = null)
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
                ParentTransactionId = parentTransactionId,
                TransactionNumber = $"TXN-{transactionId:N}"[..20],
                TransactionType = transactionType,
                PaymentMethod = StatusValues.PaymentMethod.Cash,
                Status = status,
                AmountCents = 5000,
                Currency = "PHP",
                OfferSnapshot = "{}",
                ExtraPrintCount = transactionType == StatusValues.TransactionType.ExtraPrintAddOn ? 1 : 0,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
            });

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

        public async Task<int> CountAuditLogsAsync(Guid clientAccountId, string action)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            return await dbContext.AuditLogs.CountAsync(
                item => item.ClientAccountId == clientAccountId && item.Action == action);
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

        public async Task SetClientStatusAsync(Guid clientAccountId, string status)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var client = await dbContext.ClientAccounts.SingleAsync(item => item.Id == clientAccountId);
            client.Status = status;
            await dbContext.SaveChangesAsync();
        }

        public async Task SetUserStatusAsync(Guid userId, string status)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var user = await dbContext.Users.SingleAsync(item => item.Id == userId);
            user.Status = status;
            await dbContext.SaveChangesAsync();
        }

        public async Task SetLocationStatusAsync(Guid locationId, string status)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var location = await dbContext.Locations.SingleAsync(item => item.Id == locationId);
            location.Status = status;
            await dbContext.SaveChangesAsync();
        }

        public async Task SetBoothStatusAsync(Guid boothId, string status)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var booth = await dbContext.Booths.SingleAsync(item => item.Id == boothId);
            booth.Status = status;
            if (status != StatusValues.Booth.Active)
            {
                booth.CurrentState = StatusValues.Booth.Offline;
            }

            await dbContext.SaveChangesAsync();
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

        public async Task SetBoothStateAsync(Guid boothId, string state)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var booth = await dbContext.Booths.SingleAsync(item => item.Id == boothId);
            booth.CurrentState = state;
            await dbContext.SaveChangesAsync();
        }

        public async Task SetBoothAppearanceAsync(
            Guid boothId,
            string themePreset,
            string sessionLabel,
            string welcomeHeadline,
            string welcomeSubtitle,
            string completionThankYouMessage = "Thanks for sharing your smile.")
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var appearance = await dbContext.BoothAppearanceConfigs.SingleAsync(item => item.BoothId == boothId);
            appearance.ThemePreset = themePreset;
            appearance.SessionLabel = sessionLabel;
            appearance.DefaultWelcomeHeadline = welcomeHeadline;
            appearance.DefaultWelcomeSubtitle = welcomeSubtitle;
            appearance.CompletionThankYouMessage = completionThankYouMessage;
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

        public async Task<TransactionRecord> LoadTransactionRecordAsync(Guid transactionId)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var transaction = await dbContext.Transactions.SingleAsync(item => item.Id == transactionId);
            var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId);
            return new TransactionRecord(
                transaction.Status,
                booth.CurrentState,
                transaction.FailureReason,
                transaction.CancelledByActorType,
                transaction.CancelledByUserId,
                transaction.CancellationSource,
                transaction.CancellationPreviousStatus);
        }

        public async Task MarkTransactionExpiredDueAsync(Guid transactionId)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var transaction = await dbContext.Transactions.SingleAsync(item => item.Id == transactionId);
            transaction.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await dbContext.SaveChangesAsync();
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

    private sealed record TransactionRecord(
        string Status,
        string BoothState,
        string? FailureReason,
        string? CancelledByActorType,
        Guid? CancelledByUserId,
        string? CancellationSource,
        string? CancellationPreviousStatus);
}
