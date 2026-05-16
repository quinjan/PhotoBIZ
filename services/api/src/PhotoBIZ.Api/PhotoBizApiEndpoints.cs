using System.Security.Claims;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhotoBIZ.Api.Data;

namespace PhotoBIZ.Api;

public static class PhotoBizApiEndpoints
{
    public static void MapPhotoBizApi(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/logout", (Delegate)LogoutAsync).RequireAuthorization();
        auth.MapGet("/session", GetSessionAsync).RequireAuthorization();

        var admin = app.MapGroup("/api/admin").RequireAuthorization();
        admin.MapGet("/overview", GetOverviewAsync);
        admin.MapPost("/clients", CreateClientAsync);
        admin.MapPost("/subscription-plans", CreateSubscriptionPlanAsync);
        admin.MapPost("/subscriptions", CreateSubscriptionAsync);
        admin.MapPost("/users", CreateUserAsync);
        admin.MapPost("/locations", CreateLocationAsync);
        admin.MapPost("/booths", CreateBoothAsync);
        admin.MapPost("/offers", CreateOfferAsync);
        admin.MapPost("/booths/{boothId:guid}/activate-offer", ActivateOfferAsync);
        admin.MapPut("/booths/{boothId:guid}/appearance", UpdateAppearanceAsync);
        admin.MapPost("/booths/{boothId:guid}/payment-options", AssignPaymentOptionAsync);

        var boothUi = app.MapGroup("/api/booth-ui");
        boothUi.MapGet("/config", GetBoothConfigAsync);
        boothUi.MapPost("/transactions", CreateBoothTransactionAsync);
        boothUi.MapPost("/transactions/{transactionId:guid}/payment-method", SelectPaymentMethodAsync);

        var cashier = app.MapGroup("/api/cashier").RequireAuthorization();
        cashier.MapPost("/transactions/{transactionId:guid}/approve-cash", ApproveCashAsync);
        cashier.MapPost("/transactions/{transactionId:guid}/cancel", CancelTransactionAsync);
        cashier.MapPost("/booths/{boothId:guid}/return-to-welcome", ReturnBoothToWelcomeAsync);

        var agent = app.MapGroup("/api/agent");
        agent.MapPost("/pair", PairAgentAsync);
        agent.MapPost("/heartbeat", AgentHeartbeatAsync);
        agent.MapGet("/commands/next", GetNextAgentCommandAsync);
        agent.MapPost("/transactions/{transactionId:guid}/session-started", AgentSessionStartedAsync);
        agent.MapPost("/transactions/{transactionId:guid}/session-completed", AgentSessionCompletedAsync);
        agent.MapPost("/transactions/{transactionId:guid}/session-failed", AgentSessionFailedAsync);
    }

    private static async Task<Results<Ok<AuthSessionResponse>, ValidationProblem>> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        IPasswordHasher<ApplicationUser> passwordHasher,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Email == request.Email.Trim(), cancellationToken);

        if (user is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["email"] = ["The email or password is incorrect."]
            });
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = ["The email or password is incorrect."]
            });
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

        if (user.ClientAccountId.HasValue)
        {
            claims.Add(new Claim("client_account_id", user.ClientAccountId.Value.ToString()));
        }

        if (user.AssignedBoothId.HasValue)
        {
            claims.Add(new Claim("assigned_booth_id", user.AssignedBoothId.Value.ToString()));
        }

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return TypedResults.Ok(ToSessionResponse(user));
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<AuthSessionResponse>, UnauthorizedHttpResult>> GetSessionAsync(
        PhotoBizDbContext dbContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return TypedResults.Unauthorized();
        }

        var userId = principal.GetRequiredCurrentUser().UserId;
        var user = await dbContext.Users.SingleAsync(item => item.Id == userId, cancellationToken);

        return TypedResults.Ok(ToSessionResponse(user));
    }

    private static async Task<Results<Ok<AdminOverviewResponse>, ForbidHttpResult>> GetOverviewAsync(
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();
        var query = ApplyUserScope(dbContext.Users.AsNoTracking(), currentUser, item => item.ClientAccountId, item => item.AssignedBoothId);
        var users = await query.ToListAsync(cancellationToken);

        var clients = await ApplyClientScope(dbContext.ClientAccounts.AsNoTracking(), currentUser, item => item.Id).ToListAsync(cancellationToken);
        var locations = await ApplyClientScope(dbContext.Locations.AsNoTracking(), currentUser, item => item.ClientAccountId).ToListAsync(cancellationToken);
        var booths = await ApplyClientScope(dbContext.Booths.AsNoTracking(), currentUser, item => item.ClientAccountId).ToListAsync(cancellationToken);
        var offers = await ApplyClientScope(dbContext.BoothOffers.AsNoTracking(), currentUser, item => item.ClientAccountId).ToListAsync(cancellationToken);
        var activations = await ApplyBoothScope(dbContext.BoothOfferActivations.AsNoTracking(), currentUser, item => item.BoothId).ToListAsync(cancellationToken);
        var paymentAssignments = await ApplyBoothScope(dbContext.BoothPaymentOptionAssignments.AsNoTracking(), currentUser, item => item.BoothId).ToListAsync(cancellationToken);
        var subscriptions = await ApplyClientScope(dbContext.ClientSubscriptions.AsNoTracking(), currentUser, item => item.ClientAccountId).ToListAsync(cancellationToken);
        var subscriptionPlans = await dbContext.SubscriptionPlans.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var transactions = await ApplyBoothScope(dbContext.Transactions.AsNoTracking(), currentUser, item => item.BoothId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(25)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        return TypedResults.Ok(new AdminOverviewResponse(
            new AuthSessionResponse(currentUser.UserId, users.Single(item => item.Id == currentUser.UserId).Name, users.Single(item => item.Id == currentUser.UserId).Email, currentUser.Role, currentUser.ClientAccountId, currentUser.AssignedBoothId),
            clients.Select(client => new ClientSummary(client.Id, client.Name, client.Status)).ToArray(),
            subscriptionPlans.Select(plan => new SubscriptionPlanSummary(plan.Id, plan.Name, plan.PricePerBoothCents, plan.Currency, plan.Active)).ToArray(),
            subscriptions.Select(subscription => new ClientSubscriptionSummary(subscription.Id, subscription.ClientAccountId, subscription.SubscriptionPlanId, subscription.Status, subscription.ActiveBoothAllowance)).ToArray(),
            users.Select(user => new UserSummary(user.Id, user.ClientAccountId, user.Name, user.Email, user.Role, user.AssignedBoothId)).ToArray(),
            locations.Select(location => new LocationSummary(location.Id, location.ClientAccountId, location.Name, location.Address)).ToArray(),
            booths.Select(booth => new BoothSummary(booth.Id, booth.ClientAccountId, booth.LocationId, booth.Name, booth.Code, booth.Status, PhotoBizBoothAvailability.GetEffectiveState(booth, now), booth.LastHeartbeatAt)).ToArray(),
            offers.Select(offer => new OfferSummary(offer.Id, offer.ClientAccountId, offer.Name, offer.OfferType, offer.PriceCents, offer.Currency, offer.AllowsExtraPrintAddOn, offer.Active)).ToArray(),
            activations.Select(activation => new OfferActivationSummary(activation.Id, activation.BoothId, activation.BoothOfferId, activation.Status)).ToArray(),
            paymentAssignments.Select(assignment => new PaymentAssignmentSummary(assignment.Id, assignment.BoothId, assignment.PaymentMethod, assignment.RuntimeEnabled, assignment.Status)).ToArray(),
            transactions.Select(transaction => new TransactionSummary(transaction.Id, transaction.BoothId, transaction.TransactionNumber, transaction.Status, transaction.PaymentMethod, transaction.AmountCents, transaction.CreatedAt, transaction.PaidAt, transaction.CompletedAt)).ToArray()));
    }

    private static async Task<Results<Ok<ClientSummary>, ForbidHttpResult, ValidationProblem>> CreateClientAsync(
        CreateClientRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();

        if (!currentUser.IsApplicationOwner)
        {
            return TypedResults.Forbid();
        }

        var client = new ClientAccount
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Status = StatusValues.ClientAccount.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ClientAccounts.Add(client);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "client.created", nameof(ClientAccount), client.Id, new { client.Name }, cancellationToken);

        return TypedResults.Ok(new ClientSummary(client.Id, client.Name, client.Status));
    }

    private static async Task<Results<Ok<SubscriptionPlanSummary>, ForbidHttpResult>> CreateSubscriptionPlanAsync(
        CreateSubscriptionPlanRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();

        if (!currentUser.IsApplicationOwner)
        {
            return TypedResults.Forbid();
        }

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            PricePerBoothCents = request.PricePerBoothCents,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Active = true
        };
        dbContext.SubscriptionPlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "subscription_plan.created", nameof(SubscriptionPlan), plan.Id, new { plan.Name }, cancellationToken);

        return TypedResults.Ok(new SubscriptionPlanSummary(plan.Id, plan.Name, plan.PricePerBoothCents, plan.Currency, plan.Active));
    }

    private static async Task<Results<Ok<ClientSubscriptionSummary>, ForbidHttpResult, ValidationProblem>> CreateSubscriptionAsync(
        CreateSubscriptionRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();

        if (!currentUser.IsApplicationOwner)
        {
            return TypedResults.Forbid();
        }

        var subscription = new ClientSubscription
        {
            Id = Guid.NewGuid(),
            ClientAccountId = request.ClientAccountId,
            SubscriptionPlanId = request.SubscriptionPlanId,
            Status = request.Status,
            ActiveBoothAllowance = request.ActiveBoothAllowance,
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow),
            Notes = request.Notes
        };
        dbContext.ClientSubscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "client_subscription.created", nameof(ClientSubscription), subscription.Id, new { subscription.ClientAccountId }, cancellationToken);

        return TypedResults.Ok(new ClientSubscriptionSummary(subscription.Id, subscription.ClientAccountId, subscription.SubscriptionPlanId, subscription.Status, subscription.ActiveBoothAllowance));
    }

    private static async Task<Results<Ok<UserSummary>, ForbidHttpResult, ValidationProblem>> CreateUserAsync(
        CreateUserRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        IPasswordHasher<ApplicationUser> passwordHasher,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();

        if (!currentUser.IsApplicationOwner && !currentUser.IsClientScopedAdmin)
        {
            return TypedResults.Forbid();
        }

        var clientAccountId = currentUser.IsApplicationOwner ? request.ClientAccountId : currentUser.ClientAccountId;

        if (!currentUser.IsApplicationOwner && clientAccountId != currentUser.ClientAccountId)
        {
            return TypedResults.Forbid();
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            ClientAccountId = clientAccountId,
            AssignedBoothId = request.AssignedBoothId,
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Role = request.Role,
            Status = StatusValues.User.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "user.created", nameof(ApplicationUser), user.Id, new { user.Email, user.Role }, cancellationToken);

        return TypedResults.Ok(new UserSummary(user.Id, user.ClientAccountId, user.Name, user.Email, user.Role, user.AssignedBoothId));
    }

    private static async Task<Results<Ok<LocationSummary>, ForbidHttpResult>> CreateLocationAsync(
        CreateLocationRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();

        if (!currentUser.IsClientScopedAdmin)
        {
            return TypedResults.Forbid();
        }

        var clientAccountId = currentUser.IsApplicationOwner ? request.ClientAccountId : currentUser.ClientAccountId!.Value;
        var location = new Location
        {
            Id = Guid.NewGuid(),
            ClientAccountId = clientAccountId,
            Name = request.Name.Trim(),
            Address = request.Address?.Trim(),
            Status = StatusValues.ClientAccount.Active
        };

        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "location.created", nameof(Location), location.Id, new { location.Name }, cancellationToken);

        return TypedResults.Ok(new LocationSummary(location.Id, location.ClientAccountId, location.Name, location.Address));
    }

    private static async Task<Results<Ok<CreateBoothResponse>, ForbidHttpResult>> CreateBoothAsync(
        CreateBoothRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizTokenHasher tokenHasher,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();

        if (!currentUser.IsClientScopedAdmin)
        {
            return TypedResults.Forbid();
        }

        var clientAccountId = currentUser.IsApplicationOwner ? request.ClientAccountId : currentUser.ClientAccountId!.Value;
        var kioskToken = tokenHasher.GenerateOpaqueToken();
        var agentCredential = tokenHasher.GenerateOpaqueToken();

        var booth = new Booth
        {
            Id = Guid.NewGuid(),
            ClientAccountId = clientAccountId,
            LocationId = request.LocationId,
            Name = request.Name.Trim(),
            Code = request.Code.Trim().ToUpperInvariant(),
            Status = StatusValues.Booth.Active,
            CurrentState = StatusValues.Booth.Offline,
            KioskTokenHash = tokenHasher.Hash(kioskToken),
            AgentCredentialHash = tokenHasher.Hash(agentCredential)
        };
        dbContext.Booths.Add(booth);

        dbContext.BoothAppearanceConfigs.Add(new BoothAppearanceConfig
        {
            Id = Guid.NewGuid(),
            BoothId = booth.Id,
            ThemePreset = StatusValues.Theme.VintageFilm,
            PrimaryColor = "#2f6868",
            AccentColor = "#f5d27e",
            DefaultWelcomeHeadline = "Step Into The Memory Box",
            DefaultWelcomeSubtitle = "Review today's booth offer, pay at the counter, then strike your best pose."
        });

        dbContext.BoothPaymentOptionAssignments.Add(new BoothPaymentOptionAssignment
        {
            Id = Guid.NewGuid(),
            BoothId = booth.Id,
            PaymentMethod = StatusValues.PaymentMethod.Cash,
            RuntimeEnabled = true,
            Status = StatusValues.PaymentAssignment.Assigned,
            AssignedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth.created", nameof(Booth), booth.Id, new { booth.Name, booth.Code }, cancellationToken);

        return TypedResults.Ok(new CreateBoothResponse(
            new BoothSummary(booth.Id, booth.ClientAccountId, booth.LocationId, booth.Name, booth.Code, booth.Status, booth.CurrentState, booth.LastHeartbeatAt),
            kioskToken,
            agentCredential));
    }

    private static async Task<Results<Ok<OfferSummary>, ForbidHttpResult>> CreateOfferAsync(
        CreateOfferRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();

        if (!currentUser.IsClientScopedAdmin)
        {
            return TypedResults.Forbid();
        }

        var clientAccountId = currentUser.IsApplicationOwner ? request.ClientAccountId : currentUser.ClientAccountId!.Value;
        var offer = new BoothOffer
        {
            Id = Guid.NewGuid(),
            ClientAccountId = clientAccountId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            OfferType = request.OfferType,
            PriceCents = request.PriceCents,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            IncludedPrintEntitlement = request.IncludedPrintEntitlement.Trim(),
            DurationHours = request.DurationHours,
            SessionAllowance = request.SessionAllowance,
            AllowsExtraPrintAddOn = request.AllowsExtraPrintAddOn,
            ExtraPrintPriceCents = request.ExtraPrintPriceCents,
            LumaboothSessionMode = request.LumaboothSessionMode.Trim(),
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.BoothOffers.Add(offer);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth_offer.created", nameof(BoothOffer), offer.Id, new { offer.Name, offer.OfferType }, cancellationToken);

        return TypedResults.Ok(new OfferSummary(offer.Id, offer.ClientAccountId, offer.Name, offer.OfferType, offer.PriceCents, offer.Currency, offer.AllowsExtraPrintAddOn, offer.Active));
    }

    private static async Task<Results<Ok<OfferActivationSummary>, ForbidHttpResult, ValidationProblem>> ActivateOfferAsync(
        Guid boothId,
        ActivateOfferRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();
        if (!currentUser.IsClientScopedAdmin)
        {
            return TypedResults.Forbid();
        }

        var booth = await ApplyClientScope(dbContext.Booths, currentUser, item => item.ClientAccountId)
            .SingleAsync(item => item.Id == boothId, cancellationToken);

        var activeOffer = await ApplyClientScope(dbContext.BoothOffers, currentUser, item => item.ClientAccountId)
            .SingleAsync(item => item.Id == request.BoothOfferId, cancellationToken);

        var existingActive = await dbContext.BoothOfferActivations
            .Where(item => item.BoothId == boothId && item.Status == StatusValues.OfferActivation.Active)
            .ToListAsync(cancellationToken);

        foreach (var activation in existingActive)
        {
            activation.Status = StatusValues.OfferActivation.Inactive;
            activation.DeactivatedAt = DateTimeOffset.UtcNow;
        }

        var newActivation = new BoothOfferActivation
        {
            Id = Guid.NewGuid(),
            BoothId = booth.Id,
            BoothOfferId = activeOffer.Id,
            Status = StatusValues.OfferActivation.Active,
            ActivatedAt = DateTimeOffset.UtcNow,
            SessionAllowance = activeOffer.SessionAllowance,
            StartsAt = activeOffer.OfferType == StatusValues.OfferType.TimeUnlimited ? DateTimeOffset.UtcNow : null,
            EndsAt = activeOffer.OfferType == StatusValues.OfferType.TimeUnlimited && activeOffer.DurationHours.HasValue
                ? DateTimeOffset.UtcNow.AddHours(activeOffer.DurationHours.Value)
                : null
        };
        dbContext.BoothOfferActivations.Add(newActivation);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth_offer.activated", nameof(BoothOfferActivation), newActivation.Id, new { BoothId = booth.Id, BoothOfferId = activeOffer.Id }, cancellationToken);

        return TypedResults.Ok(new OfferActivationSummary(newActivation.Id, newActivation.BoothId, newActivation.BoothOfferId, newActivation.Status));
    }

    private static async Task<Results<Ok, ForbidHttpResult, ValidationProblem>> UpdateAppearanceAsync(
        Guid boothId,
        UpdateAppearanceRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();
        if (!currentUser.IsClientScopedAdmin)
        {
            return TypedResults.Forbid();
        }

        if (!IsValidHexColor(request.PrimaryColor) || !IsValidHexColor(request.AccentColor))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["theme"] = ["Theme colors must be valid hex values."]
            });
        }

        var appearance = await ApplyBoothScope(dbContext.BoothAppearanceConfigs, currentUser, item => item.BoothId)
            .SingleAsync(item => item.BoothId == boothId, cancellationToken);

        appearance.ThemePreset = request.ThemePreset;
        appearance.PrimaryColor = request.PrimaryColor;
        appearance.AccentColor = request.AccentColor;
        appearance.BackgroundImageUrl = request.BackgroundImageUrl;
        appearance.SessionLabel = request.SessionLabel;
        appearance.DefaultWelcomeHeadline = request.DefaultWelcomeHeadline;
        appearance.DefaultWelcomeSubtitle = request.DefaultWelcomeSubtitle;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth_appearance.updated", nameof(BoothAppearanceConfig), appearance.Id, new { appearance.BoothId, appearance.ThemePreset }, cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<PaymentAssignmentSummary>, ForbidHttpResult>> AssignPaymentOptionAsync(
        Guid boothId,
        AssignPaymentOptionRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();
        if (!currentUser.IsClientScopedAdmin)
        {
            return TypedResults.Forbid();
        }

        if (request.PaymentMethod != StatusValues.PaymentMethod.Cash)
        {
            request = request with { RuntimeEnabled = false };
        }

        var assignment = await ApplyBoothScope(dbContext.BoothPaymentOptionAssignments, currentUser, item => item.BoothId)
            .SingleOrDefaultAsync(item => item.BoothId == boothId && item.PaymentMethod == request.PaymentMethod, cancellationToken);

        if (assignment is null)
        {
            assignment = new BoothPaymentOptionAssignment
            {
                Id = Guid.NewGuid(),
                BoothId = boothId,
                PaymentMethod = request.PaymentMethod,
                RuntimeEnabled = request.RuntimeEnabled,
                Status = request.RuntimeEnabled ? StatusValues.PaymentAssignment.Assigned : StatusValues.PaymentAssignment.Locked,
                AssignedAt = DateTimeOffset.UtcNow
            };
            dbContext.BoothPaymentOptionAssignments.Add(assignment);
        }
        else
        {
            assignment.RuntimeEnabled = request.RuntimeEnabled;
            assignment.Status = request.RuntimeEnabled ? StatusValues.PaymentAssignment.Assigned : StatusValues.PaymentAssignment.Locked;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth_payment_option.updated", nameof(BoothPaymentOptionAssignment), assignment.Id, new { boothId, assignment.PaymentMethod, assignment.RuntimeEnabled }, cancellationToken);

        return TypedResults.Ok(new PaymentAssignmentSummary(assignment.Id, assignment.BoothId, assignment.PaymentMethod, assignment.RuntimeEnabled, assignment.Status));
    }

    private static async Task<Results<Ok<BoothConfigResponse>, UnauthorizedHttpResult, ValidationProblem>> GetBoothConfigAsync(
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        PhotoBizTokenHasher tokenHasher,
        CancellationToken cancellationToken)
    {
        var booth = await ResolveBoothFromKioskTokenAsync(httpContext, dbContext, tokenHasher, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Unauthorized();
        }

        var now = DateTimeOffset.UtcNow;
        var effectiveBoothState = PhotoBizBoothAvailability.GetEffectiveState(booth, now);
        var subscription = await dbContext.ClientSubscriptions
            .Where(item => item.ClientAccountId == booth.ClientAccountId)
            .OrderByDescending(item => item.StartsOn)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null || subscription.Status is StatusValues.Subscription.Suspended or StatusValues.Subscription.Cancelled)
        {
            return TypedResults.Ok(ToUnavailableConfig(booth, effectiveBoothState, "Subscription inactive"));
        }

        var activeActivation = await dbContext.BoothOfferActivations
            .Include(item => item.BoothOffer)
            .Where(item => item.BoothId == booth.Id && item.Status == StatusValues.OfferActivation.Active)
            .OrderByDescending(item => item.ActivatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeActivation?.BoothOffer is null)
        {
            return TypedResults.Ok(ToUnavailableConfig(booth, effectiveBoothState, "No active booth offer configured"));
        }

        var client = await dbContext.ClientAccounts.SingleAsync(item => item.Id == booth.ClientAccountId, cancellationToken);
        var appearance = await dbContext.BoothAppearanceConfigs.SingleAsync(item => item.BoothId == booth.Id, cancellationToken);
        var paymentOptions = await dbContext.BoothPaymentOptionAssignments
            .Where(item => item.BoothId == booth.Id)
            .OrderBy(item => item.PaymentMethod)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new BoothConfigResponse(
            new BoothClientResponse(client.DisplayNameOrName(), null),
            new BoothThemeResponse(appearance.ThemePreset, appearance.PrimaryColor, appearance.AccentColor, appearance.BackgroundImageUrl, "serif"),
            new BoothSessionResponse(appearance.SessionLabel, appearance.DefaultWelcomeHeadline, appearance.DefaultWelcomeSubtitle),
            new BoothStateResponse(booth.Id, effectiveBoothState),
            new BoothOfferResponse(activeActivation.BoothOffer.Id, activeActivation.BoothOffer.Name, activeActivation.BoothOffer.OfferType, activeActivation.BoothOffer.PriceCents, activeActivation.BoothOffer.Currency, activeActivation.BoothOffer.IncludedPrintEntitlement, activeActivation.BoothOffer.AllowsExtraPrintAddOn),
            paymentOptions.Select(item => new BoothPaymentOptionResponse(item.PaymentMethod, ToPaymentLabel(item.PaymentMethod), item.RuntimeEnabled)).ToArray()));
    }

    private static async Task<Results<Ok<TransactionSummary>, UnauthorizedHttpResult, ValidationProblem>> CreateBoothTransactionAsync(
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        PhotoBizTokenHasher tokenHasher,
        PhotoBizTransactionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var booth = await ResolveBoothFromKioskTokenAsync(httpContext, dbContext, tokenHasher, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Unauthorized();
        }

        if (PhotoBizBoothAvailability.IsAgentOffline(booth, DateTimeOffset.UtcNow))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["booth"] = ["The booth agent is offline. Start the Windows Agent before creating a transaction."]
            });
        }

        var activeActivation = await dbContext.BoothOfferActivations
            .Include(item => item.BoothOffer)
            .Where(item => item.BoothId == booth.Id && item.Status == StatusValues.OfferActivation.Active)
            .OrderByDescending(item => item.ActivatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeActivation?.BoothOffer is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["booth"] = ["The booth does not have an active offer."]
            });
        }

        if (activeActivation.BoothOffer.OfferType != StatusValues.OfferType.PerSession)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["offer"] = ["This MVP build currently starts kiosk transactions only for PER_SESSION offers."]
            });
        }

        try
        {
            var transaction = await workflow.CreateTransactionAsync(booth, activeActivation, activeActivation.BoothOffer, cancellationToken);
            return TypedResults.Ok(ToTransactionSummary(transaction));
        }
        catch (InvalidOperationException exception) when (exception.Message == "The booth already has an active transaction.")
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["booth"] = ["The booth already has an active session in progress. Finish, cancel, or expire it before starting another one."]
            });
        }
    }

    private static async Task<Results<Ok<TransactionSummary>, UnauthorizedHttpResult, ValidationProblem>> SelectPaymentMethodAsync(
        Guid transactionId,
        SelectPaymentMethodRequest request,
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        PhotoBizTokenHasher tokenHasher,
        PhotoBizTransactionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var booth = await ResolveBoothFromKioskTokenAsync(httpContext, dbContext, tokenHasher, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Unauthorized();
        }

        if (PhotoBizBoothAvailability.IsAgentOffline(booth, DateTimeOffset.UtcNow))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["booth"] = ["The booth agent is offline. Start the Windows Agent before selecting payment."]
            });
        }

        var transaction = await dbContext.Transactions.SingleAsync(item => item.Id == transactionId && item.BoothId == booth.Id, cancellationToken);

        try
        {
            transaction = await workflow.SetPaymentMethodAsync(transaction, booth, request.Method, cancellationToken);
        }
        catch (InvalidOperationException exception) when (exception.Message == "The booth already has an active transaction.")
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["booth"] = ["The booth already has an active session in progress. Finish, cancel, or expire it before starting another one."]
            });
        }

        return TypedResults.Ok(ToTransactionSummary(transaction));
    }

    private static async Task<Results<Ok<TransactionSummary>, ForbidHttpResult, ValidationProblem>> ApproveCashAsync(
        Guid transactionId,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizTransactionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();
        var transaction = await LoadScopedTransactionAsync(dbContext, currentUser, transactionId, cancellationToken);

        if (transaction is null)
        {
            return TypedResults.Forbid();
        }

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
        if (PhotoBizBoothAvailability.IsAgentOffline(booth, DateTimeOffset.UtcNow))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["booth"] = ["The booth agent is offline. Start the Windows Agent before approving cash."]
            });
        }

        var updated = await workflow.ApproveCashAsync(transaction, currentUser, cancellationToken);
        return TypedResults.Ok(ToTransactionSummary(updated));
    }

    private static async Task<Results<Ok<TransactionSummary>, ForbidHttpResult>> CancelTransactionAsync(
        Guid transactionId,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizTransactionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();
        var transaction = await LoadScopedTransactionAsync(dbContext, currentUser, transactionId, cancellationToken);

        if (transaction is null)
        {
            return TypedResults.Forbid();
        }

        var updated = await workflow.CancelAsync(transaction, currentUser, cancellationToken);
        return TypedResults.Ok(ToTransactionSummary(updated));
    }

    private static async Task<Results<Ok, ForbidHttpResult>> ReturnBoothToWelcomeAsync(
        Guid boothId,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();
        var booth = await ApplyBoothScope(dbContext.Booths, currentUser, item => item.Id)
            .SingleOrDefaultAsync(item => item.Id == boothId, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Forbid();
        }

        booth.CurrentState = StatusValues.Booth.Welcome;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth.returned_to_welcome", nameof(Booth), booth.Id, new { booth.Code }, cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<AgentPairResponse>, UnauthorizedHttpResult>> PairAgentAsync(
        AgentBoothRequest request,
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        PhotoBizTokenHasher tokenHasher,
        CancellationToken cancellationToken)
    {
        var booth = await ResolveBoothFromAgentCredentialAsync(request.BoothCode, httpContext, dbContext, tokenHasher, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Unauthorized();
        }

        PhotoBizBoothAvailability.MarkAgentHeartbeat(booth, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new AgentPairResponse(booth.Id, booth.Name, booth.Code));
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult>> AgentHeartbeatAsync(
        AgentBoothRequest request,
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        PhotoBizTokenHasher tokenHasher,
        CancellationToken cancellationToken)
    {
        var booth = await ResolveBoothFromAgentCredentialAsync(request.BoothCode, httpContext, dbContext, tokenHasher, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Unauthorized();
        }

        PhotoBizBoothAvailability.MarkAgentHeartbeat(booth, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<AgentCommandResponse>, NoContent, UnauthorizedHttpResult>> GetNextAgentCommandAsync(
        string boothCode,
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        PhotoBizTokenHasher tokenHasher,
        PhotoBizTransactionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var booth = await ResolveBoothFromAgentCredentialAsync(boothCode, httpContext, dbContext, tokenHasher, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Unauthorized();
        }

        var command = await workflow.TryAcquireNextAgentCommandAsync(booth, cancellationToken);

        if (command is null)
        {
            return TypedResults.NoContent();
        }

        return TypedResults.Ok(new AgentCommandResponse(command.TransactionId, command.TransactionNumber, "START_SESSION"));
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult>> AgentSessionStartedAsync(
        Guid transactionId,
        AgentBoothRequest request,
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        PhotoBizTokenHasher tokenHasher,
        PhotoBizTransactionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var booth = await ResolveBoothFromAgentCredentialAsync(request.BoothCode, httpContext, dbContext, tokenHasher, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Unauthorized();
        }

        await workflow.MarkSessionStartedAsync(transactionId, cancellationToken);
        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult>> AgentSessionCompletedAsync(
        Guid transactionId,
        AgentBoothRequest request,
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        PhotoBizTokenHasher tokenHasher,
        PhotoBizTransactionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var booth = await ResolveBoothFromAgentCredentialAsync(request.BoothCode, httpContext, dbContext, tokenHasher, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Unauthorized();
        }

        await workflow.MarkSessionCompletedAsync(transactionId, cancellationToken);
        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult>> AgentSessionFailedAsync(
        Guid transactionId,
        AgentSessionFailedRequest request,
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        PhotoBizTokenHasher tokenHasher,
        PhotoBizTransactionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var booth = await ResolveBoothFromAgentCredentialAsync(request.BoothCode, httpContext, dbContext, tokenHasher, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Unauthorized();
        }

        await workflow.MarkSessionFailedAsync(transactionId, request.Reason, cancellationToken);
        return TypedResults.Ok();
    }

    private static IQueryable<T> ApplyClientScope<T>(IQueryable<T> query, PhotoBizCurrentUser currentUser, Expression<Func<T, Guid>> clientAccountSelector)
    {
        if (currentUser.IsApplicationOwner)
        {
            return query;
        }

        var parameter = clientAccountSelector.Parameters[0];
        var body = Expression.Equal(
            clientAccountSelector.Body,
            Expression.Constant(currentUser.ClientAccountId!.Value));

        return query.Where(Expression.Lambda<Func<T, bool>>(body, parameter));
    }

    private static IQueryable<T> ApplyBoothScope<T>(IQueryable<T> query, PhotoBizCurrentUser currentUser, Expression<Func<T, Guid>> boothIdSelector)
    {
        if (currentUser.IsApplicationOwner || currentUser.IsClientOwner || currentUser.IsClientAdmin)
        {
            return query;
        }

        var parameter = boothIdSelector.Parameters[0];
        var body = Expression.Equal(
            boothIdSelector.Body,
            Expression.Constant(currentUser.AssignedBoothId!.Value));

        return query.Where(Expression.Lambda<Func<T, bool>>(body, parameter));
    }

    private static IQueryable<ApplicationUser> ApplyUserScope(
        IQueryable<ApplicationUser> query,
        PhotoBizCurrentUser currentUser,
        Expression<Func<ApplicationUser, Guid?>> clientAccountSelector,
        Expression<Func<ApplicationUser, Guid?>> assignedBoothSelector)
    {
        if (currentUser.IsApplicationOwner)
        {
            return query;
        }

        if (currentUser.IsCashier)
        {
            var cashierBody = Expression.Equal(
                assignedBoothSelector.Body,
                Expression.Constant(currentUser.AssignedBoothId, typeof(Guid?)));

            return query.Where(Expression.Lambda<Func<ApplicationUser, bool>>(cashierBody, assignedBoothSelector.Parameters[0]));
        }

        var clientBody = Expression.Equal(
            clientAccountSelector.Body,
            Expression.Constant(currentUser.ClientAccountId, typeof(Guid?)));

        return query.Where(Expression.Lambda<Func<ApplicationUser, bool>>(clientBody, clientAccountSelector.Parameters[0]));
    }

    private static async Task<Booth?> ResolveBoothFromKioskTokenAsync(
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        PhotoBizTokenHasher tokenHasher,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Headers.TryGetValue("X-Kiosk-Token", out var tokenValues))
        {
            return null;
        }

        var token = tokenValues.ToString();
        var booths = await dbContext.Booths.Where(item => item.KioskTokenHash != null).ToListAsync(cancellationToken);

        return booths.SingleOrDefault(booth => tokenHasher.Verify(token, booth.KioskTokenHash));
    }

    private static async Task<Booth?> ResolveBoothFromAgentCredentialAsync(
        string boothCode,
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        PhotoBizTokenHasher tokenHasher,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Headers.TryGetValue("X-Agent-Credential", out var credentialValues))
        {
            return null;
        }

        var credential = credentialValues.ToString();
        var normalizedBoothCode = boothCode.ToUpperInvariant();
        var booths = await dbContext.Booths.Where(item => item.Code == normalizedBoothCode).ToListAsync(cancellationToken);
        var booth = booths.SingleOrDefault();

        return booth is not null && tokenHasher.Verify(credential, booth.AgentCredentialHash) ? booth : null;
    }

    private static async Task<Transaction?> LoadScopedTransactionAsync(
        PhotoBizDbContext dbContext,
        PhotoBizCurrentUser currentUser,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions.SingleOrDefaultAsync(item => item.Id == transactionId, cancellationToken);

        if (transaction is null)
        {
            return null;
        }

        if (currentUser.IsApplicationOwner)
        {
            return transaction;
        }

        if (currentUser.IsCashier)
        {
            return transaction.BoothId == currentUser.AssignedBoothId ? transaction : null;
        }

        return transaction.ClientAccountId == currentUser.ClientAccountId ? transaction : null;
    }

    private static AuthSessionResponse ToSessionResponse(ApplicationUser user)
    {
        return new AuthSessionResponse(user.Id, user.Name, user.Email, user.Role, user.ClientAccountId, user.AssignedBoothId);
    }

    private static TransactionSummary ToTransactionSummary(Transaction transaction)
    {
        return new TransactionSummary(
            transaction.Id,
            transaction.BoothId,
            transaction.TransactionNumber,
            transaction.Status,
            transaction.PaymentMethod,
            transaction.AmountCents,
            transaction.CreatedAt,
            transaction.PaidAt,
            transaction.CompletedAt);
    }

    private static BoothConfigResponse ToUnavailableConfig(Booth booth, string boothState, string message)
    {
        return new BoothConfigResponse(
            new BoothClientResponse("PhotoBIZ", null),
            new BoothThemeResponse(StatusValues.Theme.VintageFilm, "#2f6868", "#f5d27e", null, "serif"),
            new BoothSessionResponse(string.Empty, "Booth unavailable", message),
            new BoothStateResponse(booth.Id, boothState),
            null,
            []);
    }

    private static bool IsValidHexColor(string value)
    {
        return value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
    }

    private static string ToPaymentLabel(string paymentMethod)
    {
        return paymentMethod switch
        {
            StatusValues.PaymentMethod.Cash => "Cash",
            StatusValues.PaymentMethod.MayaCheckoutQr => "Maya Checkout QR",
            StatusValues.PaymentMethod.MayaTerminalEcr => "Maya Terminal ECR",
            _ => paymentMethod
        };
    }
}

internal static class ClientAccountExtensions
{
    public static string DisplayNameOrName(this ClientAccount client)
    {
        return client.Name;
    }
}

public sealed record LoginRequest(string Email, string Password);
public sealed record AuthSessionResponse(Guid UserId, string Name, string Email, string Role, Guid? ClientAccountId, Guid? AssignedBoothId);
public sealed record ClientSummary(Guid Id, string Name, string Status);
public sealed record SubscriptionPlanSummary(Guid Id, string Name, int PricePerBoothCents, string Currency, bool Active);
public sealed record ClientSubscriptionSummary(Guid Id, Guid ClientAccountId, Guid SubscriptionPlanId, string Status, int ActiveBoothAllowance);
public sealed record UserSummary(Guid Id, Guid? ClientAccountId, string Name, string Email, string Role, Guid? AssignedBoothId);
public sealed record LocationSummary(Guid Id, Guid ClientAccountId, string Name, string? Address);
public sealed record BoothSummary(Guid Id, Guid ClientAccountId, Guid LocationId, string Name, string Code, string Status, string CurrentState, DateTimeOffset? LastHeartbeatAt);
public sealed record OfferSummary(Guid Id, Guid ClientAccountId, string Name, string OfferType, int PriceCents, string Currency, bool AllowsExtraPrintAddOn, bool Active);
public sealed record OfferActivationSummary(Guid Id, Guid BoothId, Guid BoothOfferId, string Status);
public sealed record PaymentAssignmentSummary(Guid Id, Guid BoothId, string PaymentMethod, bool RuntimeEnabled, string Status);
public sealed record TransactionSummary(Guid Id, Guid BoothId, string TransactionNumber, string Status, string PaymentMethod, int AmountCents, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt, DateTimeOffset? CompletedAt);
public sealed record AdminOverviewResponse(
    AuthSessionResponse Session,
    IReadOnlyCollection<ClientSummary> Clients,
    IReadOnlyCollection<SubscriptionPlanSummary> SubscriptionPlans,
    IReadOnlyCollection<ClientSubscriptionSummary> Subscriptions,
    IReadOnlyCollection<UserSummary> Users,
    IReadOnlyCollection<LocationSummary> Locations,
    IReadOnlyCollection<BoothSummary> Booths,
    IReadOnlyCollection<OfferSummary> Offers,
    IReadOnlyCollection<OfferActivationSummary> Activations,
    IReadOnlyCollection<PaymentAssignmentSummary> PaymentAssignments,
    IReadOnlyCollection<TransactionSummary> Transactions);

public sealed record CreateClientRequest(string Name);
public sealed record CreateSubscriptionPlanRequest(string Name, int PricePerBoothCents, string Currency);
public sealed record CreateSubscriptionRequest(Guid ClientAccountId, Guid SubscriptionPlanId, string Status, int ActiveBoothAllowance, string? Notes);
public sealed record CreateUserRequest(Guid? ClientAccountId, Guid? AssignedBoothId, string Name, string Email, string Password, string Role);
public sealed record CreateLocationRequest(Guid ClientAccountId, string Name, string? Address);
public sealed record CreateBoothRequest(Guid ClientAccountId, Guid LocationId, string Name, string Code);
public sealed record CreateBoothResponse(BoothSummary Booth, string KioskToken, string AgentCredential);
public sealed record CreateOfferRequest(
    Guid ClientAccountId,
    string Name,
    string? Description,
    string OfferType,
    int PriceCents,
    string Currency,
    string IncludedPrintEntitlement,
    int? DurationHours,
    int? SessionAllowance,
    bool AllowsExtraPrintAddOn,
    int? ExtraPrintPriceCents,
    string LumaboothSessionMode);
public sealed record ActivateOfferRequest(Guid BoothOfferId);
public sealed record UpdateAppearanceRequest(
    string ThemePreset,
    string PrimaryColor,
    string AccentColor,
    string? BackgroundImageUrl,
    string SessionLabel,
    string DefaultWelcomeHeadline,
    string DefaultWelcomeSubtitle);
public sealed record AssignPaymentOptionRequest(string PaymentMethod, bool RuntimeEnabled);
public sealed record BoothClientResponse(string DisplayName, string? LogoUrl);
public sealed record BoothThemeResponse(string Preset, string PrimaryColor, string AccentColor, string? BackgroundImageUrl, string FontMode);
public sealed record BoothSessionResponse(string Label, string WelcomeHeadline, string WelcomeSubtitle);
public sealed record BoothStateResponse(Guid Id, string State);
public sealed record BoothOfferResponse(Guid Id, string Name, string Type, int PriceCents, string Currency, string IncludedPrintEntitlement, bool AllowsExtraPrintAddOn);
public sealed record BoothPaymentOptionResponse(string Method, string Label, bool RuntimeEnabled);
public sealed record BoothConfigResponse(
    BoothClientResponse Client,
    BoothThemeResponse Theme,
    BoothSessionResponse Session,
    BoothStateResponse Booth,
    BoothOfferResponse? ActiveOffer,
    IReadOnlyCollection<BoothPaymentOptionResponse> PaymentOptions);
public sealed record SelectPaymentMethodRequest(string Method);
public sealed record AgentBoothRequest(string BoothCode);
public sealed record AgentPairResponse(Guid BoothId, string BoothName, string BoothCode);
public sealed record AgentCommandResponse(Guid TransactionId, string TransactionNumber, string Command);
public sealed record AgentSessionFailedRequest(string BoothCode, string? Reason);
