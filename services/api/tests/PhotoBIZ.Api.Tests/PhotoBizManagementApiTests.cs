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
                    Code = $"BOOTH-{index + 1}-{clientId:N}"[..20],
                    Status = StatusValues.Booth.Active,
                    CurrentState = StatusValues.Booth.Welcome,
                    LastHeartbeatAt = includeFreshHeartbeat ? DateTimeOffset.UtcNow : null,
                    KioskTokenHash = index == 0 && kioskToken is not null ? tokenHasher.Hash(kioskToken) : null
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

            return new SeedResult(clientId, subscriptionId, locationId, ownerId, firstBoothId, offerId ?? Guid.Empty, ownerEmail);
        }
    }

    private sealed record SeedResult(
        Guid ClientAccountId,
        Guid SubscriptionId,
        Guid LocationId,
        Guid ClientOwnerId,
        Guid BoothId,
        Guid OfferId,
        string ClientOwnerEmail);
}
