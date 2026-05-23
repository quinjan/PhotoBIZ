using System.Security.Claims;
using System.Text.Json;
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
    private const string DefaultSessionLabel = "Self Photo Booth";
    private const string DefaultWelcomeHeadline = "Ready To Pose?";
    private const string DefaultWelcomeSubtitle = "Tap start when you are ready.";
    private const string DefaultCompletionThankYouMessage = "Thanks for sharing your smile.";
    private const string DefaultInitialPassword = "PhotoBIZ!123";
    private const int MinimumPasswordLength = 8;
    private const int AgentVersionMaxLength = 80;
    private const int AgentRuntimeKindMaxLength = 80;
    private const int AgentLumaBoothModeMaxLength = 40;

    public static void MapPhotoBizApi(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization();
        auth.MapPost("/logout", (Delegate)LogoutAsync).RequireAuthorization();
        auth.MapGet("/session", GetSessionAsync).RequireAuthorization();

        var admin = app.MapGroup("/api/admin").RequireAuthorization();
        admin.AddEndpointFilter(RequireCompletedPasswordChangeAsync);
        admin.AddEndpointFilter(RequireActiveClientForAdminMutationAsync);
        admin.MapGet("/overview", GetOverviewAsync);
        admin.MapPost("/clients", CreateClientAsync);
        admin.MapPost("/clients/onboard", CreateClientWithOwnerAsync);
        admin.MapPut("/clients/{clientId:guid}", UpdateClientAsync);
        admin.MapPost("/clients/{clientId:guid}/transfer-owner", TransferClientOwnerAsync);
        admin.MapPost("/subscription-plans", CreateSubscriptionPlanAsync);
        admin.MapPut("/subscription-plans/{subscriptionPlanId:guid}", UpdateSubscriptionPlanAsync);
        admin.MapPost("/subscriptions", CreateSubscriptionAsync);
        admin.MapPut("/subscriptions/{subscriptionId:guid}", UpdateSubscriptionAsync);
        admin.MapPost("/users", CreateUserAsync);
        admin.MapPut("/users/{userId:guid}", UpdateUserAsync);
        admin.MapPost("/locations", CreateLocationAsync);
        admin.MapPut("/locations/{locationId:guid}", UpdateLocationAsync);
        admin.MapPost("/booths", CreateBoothAsync);
        admin.MapPut("/booths/{boothId:guid}", UpdateBoothAsync);
        admin.MapPost("/booths/{boothId:guid}/credentials", IssueBoothCredentialsAsync);
        admin.MapPost("/offers", CreateOfferAsync);
        admin.MapPut("/offers/{offerId:guid}", UpdateOfferAsync);
        admin.MapPost("/print-entitlements", CreatePrintEntitlementAsync);
        admin.MapPut("/print-entitlements/{printEntitlementId:guid}", UpdatePrintEntitlementAsync);
        admin.MapDelete("/print-entitlements/{printEntitlementId:guid}", DeletePrintEntitlementAsync);
        admin.MapPost("/booths/{boothId:guid}/activate-offer", ActivateOfferAsync);
        admin.MapPut("/booths/{boothId:guid}/appearance", UpdateAppearanceAsync);
        admin.MapPut("/payment-resources/{paymentMethod}", UpdatePaymentResourceAsync);
        admin.MapPost("/booths/{boothId:guid}/payment-options", AssignPaymentOptionAsync);
        admin.MapDelete("/booths/{boothId:guid}/payment-options/{paymentMethod}", DisablePaymentOptionAsync);

        var boothUi = app.MapGroup("/api/booth-ui");
        boothUi.MapGet("/config", GetBoothConfigAsync);
        boothUi.MapPost("/transactions", CreateBoothTransactionAsync);
        boothUi.MapPost("/transactions/{transactionId:guid}/payment-method", SelectPaymentMethodAsync);
        boothUi.MapPost("/transactions/{transactionId:guid}/cancel", CancelBoothUiTransactionAsync);
        boothUi.MapPost("/recent-transactions/{transactionId:guid}/acknowledge", AcknowledgeRecentBoothUiTransactionAsync);
        boothUi.MapPost("/return-to-welcome", ReturnBoothUiToWelcomeAsync);

        var cashier = app.MapGroup("/api/cashier").RequireAuthorization();
        cashier.AddEndpointFilter(RequireCompletedPasswordChangeAsync);
        cashier.MapPost("/transactions/{transactionId:guid}/approve-cash", ApproveCashAsync);
        cashier.MapPost("/transactions/{transactionId:guid}/cancel", CancelTransactionAsync);
        cashier.MapPost("/transactions/{parentTransactionId:guid}/extra-prints", CreateExtraPrintAddOnAsync);
        cashier.MapPost("/booths/{boothId:guid}/plan-activation", CreatePlanActivationAsync);
        cashier.MapPost("/booths/{boothId:guid}/return-to-welcome", ReturnBoothToWelcomeAsync);

        var agent = app.MapGroup("/api/agent");
        agent.MapPost("/pair", PairAgentAsync);
        agent.MapPost("/heartbeat", AgentHeartbeatAsync);
        agent.MapPost("/offline", AgentOfflineAsync);
        agent.MapPost("/booth-ui-launch", CreateAgentBoothUiLaunchAsync);
        agent.MapGet("/commands/next", GetNextAgentCommandAsync);
        agent.MapPost("/transactions/{transactionId:guid}/session-started", AgentSessionStartedAsync);
        agent.MapPost("/transactions/{transactionId:guid}/session-completed", AgentSessionCompletedAsync);
        agent.MapPost("/transactions/{transactionId:guid}/session-failed", AgentSessionFailedAsync);
        agent.MapPost("/transactions/{transactionId:guid}/print-completed", AgentPrintCompletedAsync);
        agent.MapPost("/transactions/{transactionId:guid}/print-failed", AgentPrintFailedAsync);
    }

    private static async Task<Results<Ok<AuthSessionResponse>, ValidationProblem>> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        IPasswordHasher<ApplicationUser> passwordHasher,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(item => item.ClientAccount)
            .SingleOrDefaultAsync(item => item.Email == request.Email.Trim(), cancellationToken);

        if (user is null || !PhotoBizAuthenticationGuards.CanAuthenticate(user))
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

        await SignInUserAsync(httpContext, user);

        return TypedResults.Ok(ToSessionResponse(user));
    }

    private static async Task<Results<Ok<AuthSessionResponse>, ValidationProblem>> ChangePasswordAsync(
        ChangePasswordRequest request,
        HttpContext httpContext,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        IPasswordHasher<ApplicationUser> passwordHasher,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();
        var user = await dbContext.Users.SingleAsync(item => item.Id == currentUser.UserId, cancellationToken);
        var currentPassword = request.CurrentPassword ?? string.Empty;
        var newPassword = request.NewPassword ?? string.Empty;
        var confirmPassword = request.ConfirmPassword ?? string.Empty;

        var currentVerification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);

        if (currentVerification == PasswordVerificationResult.Failed)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["The current password is incorrect."]
            });
        }

        if (newPassword.Length < MinimumPasswordLength)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["newPassword"] = [$"Password must be at least {MinimumPasswordLength} characters."]
            });
        }

        if (newPassword != confirmPassword)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["confirmPassword"] = ["Password confirmation does not match."]
            });
        }

        var newPasswordVerification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, newPassword);

        if (newPasswordVerification != PasswordVerificationResult.Failed)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["newPassword"] = ["New password must be different from the current password."]
            });
        }

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(currentUser, "user.password_changed", nameof(ApplicationUser), user.Id, new { }, cancellationToken);
        await SignInUserAsync(httpContext, user);

        return TypedResults.Ok(ToSessionResponse(user));
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.Ok();
    }

    private static async ValueTask<object?> RequireCompletedPasswordChangeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var principal = context.HttpContext.User;

        if (principal.Identity?.IsAuthenticated != true)
        {
            return await next(context);
        }

        var currentUser = principal.GetRequiredCurrentUser();
        var dbContext = context.HttpContext.RequestServices.GetRequiredService<PhotoBizDbContext>();
        var mustChangePassword = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == currentUser.UserId)
            .Select(user => user.MustChangePassword)
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        if (mustChangePassword)
        {
            return TypedResults.Forbid();
        }

        return await next(context);
    }

    private static async ValueTask<object?> RequireActiveClientForAdminMutationAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            return await next(context);
        }

        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return await next(context);
        }

        var currentUser = principal.GetRequiredCurrentUser();
        if (currentUser.IsApplicationOwner)
        {
            return await next(context);
        }

        if (!currentUser.ClientAccountId.HasValue)
        {
            return TypedResults.Forbid();
        }

        var dbContext = context.HttpContext.RequestServices.GetRequiredService<PhotoBizDbContext>();
        var clientStatus = await dbContext.ClientAccounts
            .AsNoTracking()
            .Where(item => item.Id == currentUser.ClientAccountId.Value)
            .Select(item => item.Status)
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        if (clientStatus != StatusValues.ClientAccount.Active)
        {
            return TypedResults.Forbid();
        }

        return await next(context);
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
        var user = await dbContext.Users
            .Include(item => item.ClientAccount)
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null || !PhotoBizAuthenticationGuards.CanMaintainAuthenticatedSession(user))
        {
            return TypedResults.Unauthorized();
        }

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
        var booths = await ApplyBoothScope(ApplyClientScope(dbContext.Booths.AsNoTracking(), currentUser, item => item.ClientAccountId), currentUser, item => item.Id).ToListAsync(cancellationToken);
        if (currentUser.IsCashier)
        {
            var scopedLocationIds = booths.Select(item => item.LocationId).ToHashSet();
            locations = locations.Where(item => scopedLocationIds.Contains(item.Id)).ToList();
        }
        var scopedBoothIds = booths.Select(item => item.Id).ToArray();
        var offers = await ApplyClientScope(dbContext.BoothOffers.AsNoTracking(), currentUser, item => item.ClientAccountId).ToListAsync(cancellationToken);
        var printEntitlements = await ApplyClientScope(dbContext.PrintEntitlements.AsNoTracking(), currentUser, item => item.ClientAccountId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var activations = await ApplyScopedBoothIds(dbContext.BoothOfferActivations.AsNoTracking(), currentUser, scopedBoothIds, item => item.BoothId).ToListAsync(cancellationToken);
        var paymentProviderConfigs = await ApplyClientScope(dbContext.ClientPaymentProviderConfigs.AsNoTracking(), currentUser, item => item.ClientAccountId).ToListAsync(cancellationToken);
        var mayaEcrDevices = await ApplyClientScope(dbContext.ClientMayaEcrDevices.AsNoTracking(), currentUser, item => item.ClientAccountId).ToListAsync(cancellationToken);
        var paymentAssignments = await ApplyScopedBoothIds(dbContext.BoothPaymentOptionAssignments.AsNoTracking(), currentUser, scopedBoothIds, item => item.BoothId).ToListAsync(cancellationToken);
        var appearanceConfigs = await ApplyScopedBoothIds(dbContext.BoothAppearanceConfigs.AsNoTracking(), currentUser, scopedBoothIds, item => item.BoothId).ToListAsync(cancellationToken);
        var subscriptions = await ApplyClientScope(dbContext.ClientSubscriptions.AsNoTracking(), currentUser, item => item.ClientAccountId)
            .OrderBy(item => item.StartsOn)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var subscriptionPlans = await dbContext.SubscriptionPlans.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var reportTransactions = await ApplyScopedBoothIds(dbContext.Transactions.AsNoTracking(), currentUser, scopedBoothIds, item => item.BoothId)
            .ToListAsync(cancellationToken);
        var transactions = ToTransactionSummaries(reportTransactions)
            .OrderByDescending(item => item.CreatedAt)
            .Take(25)
            .ToArray();
        var auditLogs = await ApplyAuditLogScope(dbContext.AuditLogs.AsNoTracking(), currentUser)
            .OrderByDescending(item => item.CreatedAt)
            .Take(25)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var boothSummaries = booths.Select(booth => BuildBoothSummary(booth, PhotoBizBoothAvailability.GetEffectiveState(booth, now))).ToArray();

        var sessionUser = users.Single(item => item.Id == currentUser.UserId);

        return TypedResults.Ok(new AdminOverviewResponse(
            ToSessionResponse(sessionUser),
            clients.Select(client => new ClientSummary(client.Id, client.Name, client.Status)).ToArray(),
            subscriptionPlans.Select(plan => new SubscriptionPlanSummary(plan.Id, plan.Name, plan.PricePerBoothCents, plan.Currency, plan.Active)).ToArray(),
            subscriptions.Select(subscription => new ClientSubscriptionSummary(subscription.Id, subscription.ClientAccountId, subscription.SubscriptionPlanId, subscription.Status, subscription.ActiveBoothAllowance)).ToArray(),
            users.Select(ToUserSummary).ToArray(),
            locations.Select(location => new LocationSummary(location.Id, location.ClientAccountId, location.Name, location.Address, location.Status)).ToArray(),
            boothSummaries,
            offers.Select(ToOfferSummary).ToArray(),
            printEntitlements.Select(ToPrintEntitlementSummary).ToArray(),
            activations.Select(ToOfferActivationSummary).ToArray(),
            BuildPaymentResourceSummaries(clients, paymentProviderConfigs, mayaEcrDevices),
            paymentAssignments.Select(assignment => new PaymentAssignmentSummary(assignment.Id, assignment.BoothId, assignment.PaymentMethod, assignment.RuntimeEnabled, assignment.Status)).ToArray(),
            appearanceConfigs.Select(config =>
            {
                var normalizedTheme = NormalizeThemePreset(config.ThemePreset);
                var scheme = GetThemeScheme(normalizedTheme);
                return new BoothAppearanceSummary(
                    config.Id,
                    config.BoothId,
                    normalizedTheme,
                    scheme.PrimaryColor,
                    scheme.AccentColor,
                    config.BackgroundImageUrl,
                    config.BackgroundImageDataUrl,
                    config.SessionLabel,
                    config.DefaultWelcomeHeadline,
                    config.DefaultWelcomeSubtitle,
                    config.CompletionThankYouMessage);
            }).ToArray(),
            transactions,
            BuildReportSummary(clients, subscriptions, subscriptionPlans, boothSummaries, offers, locations, reportTransactions, now),
            auditLogs.Select(auditLog => new AuditLogSummary(auditLog.Id, auditLog.ClientAccountId, auditLog.UserId, auditLog.Action, auditLog.EntityType, auditLog.EntityId, auditLog.Metadata, auditLog.CreatedAt)).ToArray()));
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
        AddDefaultPrintEntitlements(dbContext, client.Id);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "client.created", nameof(ClientAccount), client.Id, new { client.Name }, cancellationToken);

        return TypedResults.Ok(new ClientSummary(client.Id, client.Name, client.Status));
    }

    private static async Task<Results<Ok<ClientOnboardingResponse>, ForbidHttpResult, ValidationProblem>> CreateClientWithOwnerAsync(
        CreateClientWithOwnerRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        IPasswordHasher<ApplicationUser> passwordHasher,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();

        if (!currentUser.IsApplicationOwner)
        {
            return TypedResults.Forbid();
        }

        var clientName = request.ClientName?.Trim() ?? string.Empty;
        var ownerName = request.OwnerName?.Trim() ?? string.Empty;
        var ownerEmail = request.OwnerEmail?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(clientName) ||
            string.IsNullOrWhiteSpace(ownerName) ||
            string.IsNullOrWhiteSpace(ownerEmail))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["client"] = ["Client name, owner name, and owner email are required."]
            });
        }

        var emailExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(item => item.Email == ownerEmail, cancellationToken);

        if (emailExists)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["ownerEmail"] = ["A user with this email already exists."]
            });
        }

        var client = new ClientAccount
        {
            Id = Guid.NewGuid(),
            Name = clientName,
            Status = StatusValues.ClientAccount.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var owner = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            ClientAccountId = client.Id,
            Name = ownerName,
            Email = ownerEmail,
            Role = StatusValues.User.ClientOwner,
            Status = StatusValues.User.Active,
            MustChangePassword = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        owner.PasswordHash = passwordHasher.HashPassword(owner, DefaultInitialPassword);

        dbContext.ClientAccounts.Add(client);
        dbContext.Users.Add(owner);
        AddDefaultPrintEntitlements(dbContext, client.Id);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "client.onboarded", nameof(ClientAccount), client.Id, new { client.Name, OwnerEmail = owner.Email }, cancellationToken);
        await auditService.WriteAsync(currentUser, "user.created", nameof(ApplicationUser), owner.Id, new { owner.Email, owner.Role, owner.ClientAccountId }, cancellationToken);

        return TypedResults.Ok(new ClientOnboardingResponse(
            new ClientSummary(client.Id, client.Name, client.Status),
            ToUserSummary(owner)));
    }

    private static async Task<Results<Ok<ClientSummary>, ForbidHttpResult, ValidationProblem>> UpdateClientAsync(
        Guid clientId,
        UpdateClientRequest request,
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

        if (!IsKnownClientAccountStatus(request.Status))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Client account status is not supported."]
            });
        }

        var client = await dbContext.ClientAccounts.SingleOrDefaultAsync(item => item.Id == clientId, cancellationToken);

        if (client is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["clientId"] = ["Client account was not found."]
            });
        }

        client.Name = request.Name.Trim();
        client.Status = request.Status;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "client.updated", nameof(ClientAccount), client.Id, new { client.Name, client.Status }, cancellationToken);

        return TypedResults.Ok(new ClientSummary(client.Id, client.Name, client.Status));
    }

    private static async Task<Results<Ok<UserSummary>, ForbidHttpResult, ValidationProblem>> TransferClientOwnerAsync(
        Guid clientId,
        TransferClientOwnerRequest request,
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

        var clientExists = await dbContext.ClientAccounts
            .AsNoTracking()
            .AnyAsync(item => item.Id == clientId, cancellationToken);

        if (!clientExists)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["clientId"] = ["Client account was not found."]
            });
        }

        var newOwner = await dbContext.Users
            .SingleOrDefaultAsync(item => item.Id == request.NewOwnerUserId && item.ClientAccountId == clientId, cancellationToken);

        if (newOwner is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["newOwnerUserId"] = ["New owner must be an existing user in this client account."]
            });
        }

        if (newOwner.Status != StatusValues.User.Active)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["newOwnerUserId"] = ["New owner must be active."]
            });
        }

        var existingOwners = await dbContext.Users
            .Where(item => item.ClientAccountId == clientId && item.Role == StatusValues.User.ClientOwner)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        foreach (var owner in existingOwners.Where(owner => owner.Id != newOwner.Id))
        {
            owner.Role = StatusValues.User.ClientAdmin;
            owner.CanApproveCash = true;
            owner.CanReturnBoothToWelcome = true;
            owner.CanCancelTransaction = true;
        }

        newOwner.Role = StatusValues.User.ClientOwner;
        newOwner.CanApproveCash = true;
        newOwner.CanReturnBoothToWelcome = true;
        newOwner.CanCancelTransaction = true;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            currentUser,
            "client.owner_transferred",
            nameof(ClientAccount),
            clientId,
            new { NewOwnerUserId = newOwner.Id, newOwner.Email, PreviousOwnerIds = existingOwners.Where(owner => owner.Id != newOwner.Id).Select(owner => owner.Id).ToArray() },
            cancellationToken);

        return TypedResults.Ok(ToUserSummary(newOwner));
    }

    private static async Task<Results<Ok<SubscriptionPlanSummary>, ForbidHttpResult, ValidationProblem>> CreateSubscriptionPlanAsync(
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

        var validationProblem = await ValidateSubscriptionPlanRequestAsync(
            dbContext,
            request.Name,
            request.PricePerBoothCents,
            request.Currency,
            null,
            cancellationToken);

        if (validationProblem is not null)
        {
            return validationProblem;
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

    private static async Task<Results<Ok<SubscriptionPlanSummary>, ForbidHttpResult, ValidationProblem>> UpdateSubscriptionPlanAsync(
        Guid subscriptionPlanId,
        UpdateSubscriptionPlanRequest request,
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

        var validationProblem = await ValidateSubscriptionPlanRequestAsync(
            dbContext,
            request.Name,
            request.PricePerBoothCents,
            request.Currency,
            subscriptionPlanId,
            cancellationToken);

        if (validationProblem is not null)
        {
            return validationProblem;
        }

        var plan = await dbContext.SubscriptionPlans.SingleOrDefaultAsync(item => item.Id == subscriptionPlanId, cancellationToken);

        if (plan is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["subscriptionPlanId"] = ["Subscription was not found."]
            });
        }

        plan.Name = request.Name.Trim();
        plan.PricePerBoothCents = request.PricePerBoothCents;
        plan.Currency = request.Currency.Trim().ToUpperInvariant();
        plan.Active = request.Active;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "subscription_plan.updated", nameof(SubscriptionPlan), plan.Id, new { plan.Name, plan.PricePerBoothCents, plan.Active }, cancellationToken);

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

        if (!IsKnownSubscriptionStatus(request.Status))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Subscription status is not supported."]
            });
        }

        if (request.ActiveBoothAllowance < 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["activeBoothAllowance"] = ["Active booth allowance cannot be negative."]
            });
        }

        var clientExists = await dbContext.ClientAccounts
            .AsNoTracking()
            .AnyAsync(item => item.Id == request.ClientAccountId, cancellationToken);
        var planExists = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .AnyAsync(item => item.Id == request.SubscriptionPlanId && item.Active, cancellationToken);

        if (!clientExists || !planExists)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["subscription"] = ["Client account and active subscription plan are required."]
            });
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

    private static async Task<Results<Ok<ClientSubscriptionSummary>, ForbidHttpResult, ValidationProblem>> UpdateSubscriptionAsync(
        Guid subscriptionId,
        UpdateSubscriptionRequest request,
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

        if (!IsKnownSubscriptionStatus(request.Status))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Subscription status is not supported."]
            });
        }

        if (request.ActiveBoothAllowance < 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["activeBoothAllowance"] = ["Active booth allowance cannot be negative."]
            });
        }

        var subscription = await dbContext.ClientSubscriptions.SingleOrDefaultAsync(item => item.Id == subscriptionId, cancellationToken);

        if (subscription is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["subscriptionId"] = ["Client subscription was not found."]
            });
        }

        var activeBoothCount = await CountActiveBoothsAsync(dbContext, subscription.ClientAccountId, cancellationToken);

        if (request.ActiveBoothAllowance < activeBoothCount)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["activeBoothAllowance"] = ["Active booth allowance cannot be lower than the current active booth count."]
            });
        }

        if (request.SubscriptionPlanId is { } requestedPlanId && requestedPlanId != subscription.SubscriptionPlanId)
        {
            var planExists = await dbContext.SubscriptionPlans
                .AsNoTracking()
                .AnyAsync(item => item.Id == requestedPlanId && item.Active, cancellationToken);

            if (!planExists)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["subscriptionPlanId"] = ["Active subscription plan was not found."]
                });
            }

            subscription.SubscriptionPlanId = requestedPlanId;
        }

        subscription.Status = request.Status;
        subscription.ActiveBoothAllowance = request.ActiveBoothAllowance;
        subscription.EndsOn = request.EndsOn;
        subscription.Notes = request.Notes;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "client_subscription.updated", nameof(ClientSubscription), subscription.Id, new { subscription.ClientAccountId, subscription.Status, subscription.ActiveBoothAllowance }, cancellationToken);

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

        if (clientAccountId is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["clientAccountId"] = ["Client account is required for client users."]
            });
        }

        if (!currentUser.IsApplicationOwner && clientAccountId != currentUser.ClientAccountId)
        {
            return TypedResults.Forbid();
        }

        if (!IsKnownClientUserRole(request.Role))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = ["User role is not supported for client user management."]
            });
        }

        if (request.Role == StatusValues.User.ClientOwner)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = ["Client Owner is created during onboarding and transferred by the Application Owner."]
            });
        }

        var clientExists = await dbContext.ClientAccounts
            .AsNoTracking()
            .AnyAsync(item => item.Id == clientAccountId.Value, cancellationToken);

        if (!clientExists)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["clientAccountId"] = ["Client account was not found."]
            });
        }

        if (IsPosAssignableRole(request.Role) && request.AssignedBoothId.HasValue)
        {
            var assignedBoothValidation = await ValidateAssignedBoothAsync(
                dbContext,
                clientAccountId.Value,
                request.AssignedBoothId.Value,
                excludedUserId: null,
                cancellationToken);

            if (assignedBoothValidation is not null)
            {
                return assignedBoothValidation;
            }
        }
        else if (!IsPosAssignableRole(request.Role) && request.AssignedBoothId.HasValue)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedBoothId"] = ["Only Client Owner, Client Admin, or Cashier users can be assigned to a booth."]
            });
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
            CanApproveCash = IsCashierPermissionEnabled(request.Role, request.CanApproveCash),
            CanReturnBoothToWelcome = IsCashierPermissionEnabled(request.Role, request.CanReturnBoothToWelcome),
            CanCancelTransaction = IsCashierPermissionEnabled(request.Role, request.CanCancelTransaction),
            MustChangePassword = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, DefaultInitialPassword);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "user.created", nameof(ApplicationUser), user.Id, new { user.Email, user.Role }, cancellationToken);

        return TypedResults.Ok(ToUserSummary(user));
    }

    private static async Task<Results<Ok<UserSummary>, ForbidHttpResult, ValidationProblem>> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest request,
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

        if (!IsKnownClientUserRole(request.Role))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = ["User role is not supported for client user management."]
            });
        }

        if (!IsKnownUserStatus(request.Status))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["User status is not supported."]
            });
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null || user.ClientAccountId is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["userId"] = ["Client user was not found."]
            });
        }

        if (!currentUser.IsApplicationOwner && user.ClientAccountId != currentUser.ClientAccountId)
        {
            return TypedResults.Forbid();
        }

        if (currentUser.IsClientAdmin && (user.Role == StatusValues.User.ClientOwner || request.Role == StatusValues.User.ClientOwner))
        {
            return TypedResults.Forbid();
        }

        if (user.Id == currentUser.UserId && request.Status == StatusValues.User.Inactive)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["You cannot deactivate your own account."]
            });
        }

        if (user.Role != request.Role &&
            (user.Role == StatusValues.User.ClientOwner || request.Role == StatusValues.User.ClientOwner))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = ["Client Owner role changes must use Application Owner owner transfer."]
            });
        }

        if (IsPosAssignableRole(request.Role) && request.AssignedBoothId.HasValue)
        {
            var assignedBoothValidation = await ValidateAssignedBoothAsync(
                dbContext,
                user.ClientAccountId.Value,
                request.AssignedBoothId.Value,
                user.Id,
                cancellationToken);

            if (assignedBoothValidation is not null)
            {
                return assignedBoothValidation;
            }
        }
        else if (!IsPosAssignableRole(request.Role) && request.AssignedBoothId.HasValue)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedBoothId"] = ["Only Client Owner, Client Admin, or Cashier users can be assigned to a booth."]
            });
        }

        user.Name = request.Name.Trim();
        user.Email = request.Email.Trim();
        user.Role = request.Role;
        user.Status = request.Status;
        user.AssignedBoothId = IsPosAssignableRole(request.Role) ? request.AssignedBoothId : null;
        user.CanApproveCash = IsCashierPermissionEnabled(request.Role, request.CanApproveCash);
        user.CanReturnBoothToWelcome = IsCashierPermissionEnabled(request.Role, request.CanReturnBoothToWelcome);
        user.CanCancelTransaction = IsCashierPermissionEnabled(request.Role, request.CanCancelTransaction);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "user.updated", nameof(ApplicationUser), user.Id, new { user.Email, user.Role, user.Status, user.AssignedBoothId, user.CanApproveCash, user.CanReturnBoothToWelcome, user.CanCancelTransaction }, cancellationToken);

        return TypedResults.Ok(ToUserSummary(user));
    }

    private static async Task<Results<Ok<LocationSummary>, ForbidHttpResult, ValidationProblem>> CreateLocationAsync(
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

        var clientExists = await dbContext.ClientAccounts
            .AsNoTracking()
            .AnyAsync(item => item.Id == clientAccountId, cancellationToken);

        if (!clientExists)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["clientAccountId"] = ["Client account was not found."]
            });
        }

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

        return TypedResults.Ok(new LocationSummary(location.Id, location.ClientAccountId, location.Name, location.Address, location.Status));
    }

    private static async Task<Results<Ok<LocationSummary>, ForbidHttpResult, ValidationProblem>> UpdateLocationAsync(
        Guid locationId,
        UpdateLocationRequest request,
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

        if (!IsKnownLocationStatus(request.Status))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Location status is not supported."]
            });
        }

        var location = await ApplyClientScope(dbContext.Locations, currentUser, item => item.ClientAccountId)
            .SingleOrDefaultAsync(item => item.Id == locationId, cancellationToken);

        if (location is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["locationId"] = ["Location was not found in the selected client account."]
            });
        }

        location.Name = request.Name.Trim();
        location.Address = request.Address?.Trim();
        location.Status = request.Status;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "location.updated", nameof(Location), location.Id, new { location.Name, location.Status }, cancellationToken);

        return TypedResults.Ok(new LocationSummary(location.Id, location.ClientAccountId, location.Name, location.Address, location.Status));
    }

    private static async Task<Results<Ok<CreateBoothResponse>, ForbidHttpResult, ValidationProblem>> CreateBoothAsync(
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
        var location = await dbContext.Locations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.LocationId && item.ClientAccountId == clientAccountId, cancellationToken);

        if (location is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["locationId"] = ["Location must exist in the selected client account."]
            });
        }

        if (location.Status != StatusValues.Booth.Active)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["locationId"] = ["Location must be ACTIVE before registering a booth."]
            });
        }

        var subscriptionValidation = await ValidateSubscriptionAllowsNewBoothAsync(dbContext, clientAccountId, cancellationToken);

        if (subscriptionValidation is not null)
        {
            return subscriptionValidation;
        }

        ApplicationUser? assignedPosStaff = null;

        if (request.CashierUserId.HasValue)
        {
            assignedPosStaff = await dbContext.Users.SingleOrDefaultAsync(
                item => item.Id == request.CashierUserId.Value && item.ClientAccountId == clientAccountId,
                cancellationToken);

            if (assignedPosStaff is null)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["cashierUserId"] = ["Assigned POS staff must exist in the selected client account."]
                });
            }

            if (!IsPosAssignableRole(assignedPosStaff.Role))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["cashierUserId"] = ["Assigned user must be a Client Owner, Client Admin, or Cashier."]
                });
            }

            if (assignedPosStaff.AssignedBoothId.HasValue)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["cashierUserId"] = ["Assigned POS staff is already linked to another booth."]
                });
            }
        }

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
            ThemePreset = StatusValues.Theme.Vintage,
            PrimaryColor = GetThemeScheme(StatusValues.Theme.Vintage).PrimaryColor,
            AccentColor = GetThemeScheme(StatusValues.Theme.Vintage).AccentColor,
            SessionLabel = DefaultSessionLabel,
            DefaultWelcomeHeadline = DefaultWelcomeHeadline,
            DefaultWelcomeSubtitle = DefaultWelcomeSubtitle,
            CompletionThankYouMessage = DefaultCompletionThankYouMessage
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

        if (assignedPosStaff is not null)
        {
            assignedPosStaff.AssignedBoothId = booth.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth.created", nameof(Booth), booth.Id, new { booth.Name, booth.Code, request.CashierUserId }, cancellationToken);

        return TypedResults.Ok(new CreateBoothResponse(
            BuildBoothSummary(booth, booth.CurrentState),
            kioskToken,
            agentCredential));
    }

    private static async Task<Results<Ok<BoothSummary>, ForbidHttpResult, ValidationProblem>> UpdateBoothAsync(
        Guid boothId,
        UpdateBoothRequest request,
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

        if (!IsKnownBoothStatus(request.Status))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Booth status is not supported."]
            });
        }

        var booth = await ApplyClientScope(dbContext.Booths, currentUser, item => item.ClientAccountId)
            .SingleOrDefaultAsync(item => item.Id == boothId, cancellationToken);

        if (booth is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["boothId"] = ["Booth was not found in the selected client account."]
            });
        }

        var location = await dbContext.Locations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.LocationId && item.ClientAccountId == booth.ClientAccountId, cancellationToken);

        if (location is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["locationId"] = ["Location must exist in the booth's client account."]
            });
        }

        var isMovingBooth = request.LocationId != booth.LocationId;
        if (location.Status != StatusValues.Booth.Active &&
            (request.Status == StatusValues.Booth.Active || isMovingBooth))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["locationId"] = ["Location must be ACTIVE before moving or reactivating a booth there."]
            });
        }

        ApplicationUser? requestedPosStaff = null;

        if (request.CashierUserId.HasValue)
        {
            requestedPosStaff = await dbContext.Users.SingleOrDefaultAsync(
                item => item.Id == request.CashierUserId.Value && item.ClientAccountId == booth.ClientAccountId,
                cancellationToken);

            if (requestedPosStaff is null)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["cashierUserId"] = ["Assigned POS staff must exist in the booth's client account."]
                });
            }

            if (!IsPosAssignableRole(requestedPosStaff.Role))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["cashierUserId"] = ["Assigned user must be a Client Owner, Client Admin, or Cashier."]
                });
            }

            if (requestedPosStaff.AssignedBoothId.HasValue && requestedPosStaff.AssignedBoothId.Value != booth.Id)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["cashierUserId"] = ["Assigned POS staff is already linked to another booth."]
                });
            }
        }

        if (booth.Status != StatusValues.Booth.Active && request.Status == StatusValues.Booth.Active)
        {
            var subscriptionValidation = await ValidateSubscriptionAllowsNewBoothAsync(dbContext, booth.ClientAccountId, cancellationToken);

            if (subscriptionValidation is not null)
            {
                return subscriptionValidation;
            }
        }

        booth.LocationId = request.LocationId;
        booth.Name = request.Name.Trim();
        booth.Code = request.Code.Trim().ToUpperInvariant();
        booth.Status = request.Status;

        var existingPosStaff = await dbContext.Users.SingleOrDefaultAsync(
            item => item.ClientAccountId == booth.ClientAccountId && item.AssignedBoothId == booth.Id,
            cancellationToken);

        if (existingPosStaff is not null && existingPosStaff.Id != request.CashierUserId)
        {
            existingPosStaff.AssignedBoothId = null;
        }

        if (requestedPosStaff is not null)
        {
            requestedPosStaff.AssignedBoothId = booth.Id;
        }

        if (request.Status == StatusValues.Booth.Inactive)
        {
            booth.CurrentState = StatusValues.Booth.Offline;

            var activeActivations = await dbContext.BoothOfferActivations
                .Where(item => item.BoothId == booth.Id && item.Status == StatusValues.OfferActivation.Active)
                .ToListAsync(cancellationToken);

            foreach (var activation in activeActivations)
            {
                activation.Status = StatusValues.OfferActivation.Inactive;
                activation.DeactivatedAt = DateTimeOffset.UtcNow;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth.updated", nameof(Booth), booth.Id, new { booth.Name, booth.Code, booth.Status, request.CashierUserId }, cancellationToken);

        return TypedResults.Ok(BuildBoothSummary(booth, booth.CurrentState));
    }

    private static async Task<Results<Ok<BoothCredentialResponse>, ForbidHttpResult>> IssueBoothCredentialsAsync(
        Guid boothId,
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

        var booth = await LoadScopedBoothAsync(dbContext, currentUser, boothId, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Forbid();
        }

        var credentials = RotateBoothCredentials(booth, tokenHasher);

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(currentUser, "booth.credentials_issued", nameof(Booth), booth.Id, new { booth.Code }, cancellationToken);

        return TypedResults.Ok(credentials);
    }

    private static async Task<Results<Ok<OfferSummary>, ForbidHttpResult, ValidationProblem>> CreateOfferAsync(
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

        var clientExists = await dbContext.ClientAccounts
            .AsNoTracking()
            .AnyAsync(item => item.Id == clientAccountId, cancellationToken);

        if (!clientExists)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["clientAccountId"] = ["Client account was not found."]
            });
        }

        if (!IsKnownOfferType(request.OfferType))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["offerType"] = ["Booth offer type is not supported."]
            });
        }

        var lumaboothSessionMode = NormalizeLumaboothSessionMode(request.LumaboothSessionMode);
        if (lumaboothSessionMode is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["lumaboothSessionMode"] = ["LumaBooth session mode must be PRINT, GIF, BOOMERANG, or VIDEO."]
            });
        }

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
            LumaboothSessionMode = lumaboothSessionMode,
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.BoothOffers.Add(offer);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth_offer.created", nameof(BoothOffer), offer.Id, new { offer.Name, offer.OfferType }, cancellationToken);

        return TypedResults.Ok(ToOfferSummary(offer));
    }

    private static async Task<Results<Ok<OfferSummary>, ForbidHttpResult, ValidationProblem>> UpdateOfferAsync(
        Guid offerId,
        UpdateOfferRequest request,
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

        if (!IsKnownOfferType(request.OfferType))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["offerType"] = ["Booth offer type is not supported."]
            });
        }

        var lumaboothSessionMode = NormalizeLumaboothSessionMode(request.LumaboothSessionMode);
        if (lumaboothSessionMode is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["lumaboothSessionMode"] = ["LumaBooth session mode must be PRINT, GIF, BOOMERANG, or VIDEO."]
            });
        }

        var offer = await ApplyClientScope(dbContext.BoothOffers, currentUser, item => item.ClientAccountId)
            .SingleOrDefaultAsync(item => item.Id == offerId, cancellationToken);

        if (offer is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["offerId"] = ["Booth offer was not found in the selected client account."]
            });
        }

        offer.Name = request.Name.Trim();
        offer.Description = request.Description?.Trim();
        offer.OfferType = request.OfferType;
        offer.PriceCents = request.PriceCents;
        offer.Currency = request.Currency.Trim().ToUpperInvariant();
        offer.IncludedPrintEntitlement = request.IncludedPrintEntitlement.Trim();
        offer.DurationHours = request.DurationHours;
        offer.SessionAllowance = request.SessionAllowance;
        offer.AllowsExtraPrintAddOn = request.AllowsExtraPrintAddOn;
        offer.ExtraPrintPriceCents = request.ExtraPrintPriceCents;
        offer.LumaboothSessionMode = lumaboothSessionMode;
        offer.Active = request.Active;

        if (!offer.Active)
        {
            var activeActivations = await dbContext.BoothOfferActivations
                .Where(item => item.BoothOfferId == offer.Id && item.Status == StatusValues.OfferActivation.Active)
                .ToListAsync(cancellationToken);

            foreach (var activation in activeActivations)
            {
                activation.Status = StatusValues.OfferActivation.Inactive;
                activation.DeactivatedAt = DateTimeOffset.UtcNow;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth_offer.updated", nameof(BoothOffer), offer.Id, new { offer.Name, offer.OfferType, offer.Active }, cancellationToken);

        return TypedResults.Ok(ToOfferSummary(offer));
    }

    private static async Task<Results<Ok<PrintEntitlementSummary>, ForbidHttpResult, ValidationProblem>> CreatePrintEntitlementAsync(
        CreatePrintEntitlementRequest request,
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
        var validation = await ValidatePrintEntitlementRequestAsync(dbContext, clientAccountId, request.Name, null, cancellationToken);

        if (validation is not null)
        {
            return validation;
        }

        var entitlement = new PrintEntitlement
        {
            Id = Guid.NewGuid(),
            ClientAccountId = clientAccountId,
            Name = request.Name.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.PrintEntitlements.Add(entitlement);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "print_entitlement.created", nameof(PrintEntitlement), entitlement.Id, new { entitlement.Name }, cancellationToken);

        return TypedResults.Ok(ToPrintEntitlementSummary(entitlement));
    }

    private static async Task<Results<Ok<PrintEntitlementSummary>, ForbidHttpResult, ValidationProblem>> UpdatePrintEntitlementAsync(
        Guid printEntitlementId,
        UpdatePrintEntitlementRequest request,
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

        var entitlement = await ApplyClientScope(dbContext.PrintEntitlements, currentUser, item => item.ClientAccountId)
            .SingleOrDefaultAsync(item => item.Id == printEntitlementId, cancellationToken);

        if (entitlement is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["printEntitlementId"] = ["Print entitlement was not found in the selected client account."]
            });
        }

        var validation = await ValidatePrintEntitlementRequestAsync(dbContext, entitlement.ClientAccountId, request.Name, entitlement.Id, cancellationToken);

        if (validation is not null)
        {
            return validation;
        }

        entitlement.Name = request.Name.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "print_entitlement.updated", nameof(PrintEntitlement), entitlement.Id, new { entitlement.Name }, cancellationToken);

        return TypedResults.Ok(ToPrintEntitlementSummary(entitlement));
    }

    private static async Task<Results<NoContent, ForbidHttpResult, ValidationProblem>> DeletePrintEntitlementAsync(
        Guid printEntitlementId,
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

        var entitlement = await ApplyClientScope(dbContext.PrintEntitlements, currentUser, item => item.ClientAccountId)
            .SingleOrDefaultAsync(item => item.Id == printEntitlementId, cancellationToken);

        if (entitlement is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["printEntitlementId"] = ["Print entitlement was not found in the selected client account."]
            });
        }

        var isUsedByPackage = await dbContext.BoothOffers
            .AsNoTracking()
            .AnyAsync(
                item => item.ClientAccountId == entitlement.ClientAccountId &&
                    item.IncludedPrintEntitlement == entitlement.Name,
                cancellationToken);

        if (isUsedByPackage)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["printEntitlementId"] = ["Print entitlement is in use by one or more packages."]
            });
        }

        dbContext.PrintEntitlements.Remove(entitlement);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "print_entitlement.deleted", nameof(PrintEntitlement), entitlement.Id, new { entitlement.Name }, cancellationToken);

        return TypedResults.NoContent();
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
            .SingleOrDefaultAsync(item => item.Id == boothId, cancellationToken);

        if (booth is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["boothId"] = ["Booth was not found in the selected client account."]
            });
        }

        var activeOffer = await ApplyClientScope(dbContext.BoothOffers, currentUser, item => item.ClientAccountId)
            .SingleOrDefaultAsync(item => item.Id == request.BoothOfferId, cancellationToken);

        if (activeOffer is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["boothOfferId"] = ["Booth offer was not found in the selected client account."]
            });
        }

        if (booth.Status != StatusValues.Booth.Active)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["boothId"] = ["Booth must be ACTIVE before activating a package."]
            });
        }

        var locationIsActive = await dbContext.Locations
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.Id == booth.LocationId &&
                    item.ClientAccountId == booth.ClientAccountId &&
                    item.Status == StatusValues.Booth.Active,
                cancellationToken);

        if (!locationIsActive)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["location"] = ["Booth location must be ACTIVE before activating a package."]
            });
        }

        if (!activeOffer.Active)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["boothOfferId"] = ["Booth offer must be active before assignment."]
            });
        }

        var subscriptionValidation = await ValidateSubscriptionAllowsExistingBoothAsync(dbContext, booth.ClientAccountId, cancellationToken);

        if (subscriptionValidation is not null)
        {
            return subscriptionValidation;
        }

        var existingActive = await dbContext.BoothOfferActivations
            .Where(item => item.BoothId == boothId && item.Status == StatusValues.OfferActivation.Active)
            .ToListAsync(cancellationToken);

        foreach (var activation in existingActive)
        {
            activation.Status = StatusValues.OfferActivation.Inactive;
            activation.DeactivatedAt = DateTimeOffset.UtcNow;
        }

        var existingPending = await dbContext.BoothOfferActivations
            .Where(item => item.BoothId == boothId && item.Status == StatusValues.OfferActivation.PendingPayment)
            .ToListAsync(cancellationToken);

        foreach (var activation in existingPending)
        {
            activation.Status = StatusValues.OfferActivation.Cancelled;
            activation.DeactivatedAt = DateTimeOffset.UtcNow;
        }

        var activationStatus = activeOffer.OfferType == StatusValues.OfferType.PerSession
            ? StatusValues.OfferActivation.Active
            : StatusValues.OfferActivation.PendingPayment;

        var newActivation = new BoothOfferActivation
        {
            Id = Guid.NewGuid(),
            BoothId = booth.Id,
            BoothOfferId = activeOffer.Id,
            Status = activationStatus,
            ActivatedAt = DateTimeOffset.UtcNow,
            SessionAllowance = activeOffer.SessionAllowance,
            StartsAt = activationStatus == StatusValues.OfferActivation.Active && activeOffer.OfferType == StatusValues.OfferType.TimeUnlimited ? DateTimeOffset.UtcNow : null,
            EndsAt = activationStatus == StatusValues.OfferActivation.Active && activeOffer.OfferType == StatusValues.OfferType.TimeUnlimited && activeOffer.DurationHours.HasValue
                ? DateTimeOffset.UtcNow.AddHours(activeOffer.DurationHours.Value)
                : null
        };
        dbContext.BoothOfferActivations.Add(newActivation);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth_offer.activated", nameof(BoothOfferActivation), newActivation.Id, new { BoothId = booth.Id, BoothOfferId = activeOffer.Id }, cancellationToken);

        return TypedResults.Ok(ToOfferActivationSummary(newActivation));
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

        if (!IsKnownThemePreset(request.ThemePreset))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["themePreset"] = ["Theme preset is not supported."]
            });
        }

        if (!IsValidImageDataUrl(request.BackgroundImageDataUrl))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["backgroundImage"] = ["Background image must be a PNG, JPEG, or WebP data image up to 2 MB."]
            });
        }

        var booth = await ApplyClientScope(dbContext.Booths.AsNoTracking(), currentUser, item => item.ClientAccountId)
            .SingleOrDefaultAsync(item => item.Id == boothId, cancellationToken);

        if (booth is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["boothId"] = ["Booth was not found in the selected client account."]
            });
        }

        var appearance = await dbContext.BoothAppearanceConfigs
            .SingleOrDefaultAsync(item => item.BoothId == boothId, cancellationToken);

        if (appearance is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["boothId"] = ["Booth appearance was not found for this booth."]
            });
        }

        var normalizedTheme = NormalizeThemePreset(request.ThemePreset);
        var themeScheme = GetThemeScheme(normalizedTheme);
        appearance.ThemePreset = normalizedTheme;
        appearance.PrimaryColor = themeScheme.PrimaryColor;
        appearance.AccentColor = themeScheme.AccentColor;
        appearance.BackgroundImageUrl = null;
        appearance.BackgroundImageDataUrl = request.BackgroundImageDataUrl;
        appearance.SessionLabel = request.SessionLabel;
        appearance.DefaultWelcomeHeadline = request.DefaultWelcomeHeadline;
        appearance.DefaultWelcomeSubtitle = request.DefaultWelcomeSubtitle;
        appearance.CompletionThankYouMessage = request.CompletionThankYouMessage;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth_appearance.updated", nameof(BoothAppearanceConfig), appearance.Id, new { appearance.BoothId, appearance.ThemePreset }, cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<PaymentResourceSummary>, ForbidHttpResult, ValidationProblem>> UpdatePaymentResourceAsync(
        string paymentMethod,
        UpdatePaymentResourceRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();
        if (!currentUser.IsClientOwner && !currentUser.IsClientAdmin)
        {
            return TypedResults.Forbid();
        }

        if (!IsKnownPaymentMethod(paymentMethod))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["paymentMethod"] = ["Payment method is not supported."]
            });
        }

        var clientAccountId = currentUser.ClientAccountId!.Value;

        if (paymentMethod == StatusValues.PaymentMethod.Cash)
        {
            if (!request.Enabled)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["enabled"] = ["Cash is always enabled for client tenants."]
                });
            }

            return TypedResults.Ok(new PaymentResourceSummary(clientAccountId, StatusValues.PaymentMethod.Cash, true, StatusValues.PaymentResource.Verified));
        }

        PaymentResourceSummary summary;

        if (paymentMethod == StatusValues.PaymentMethod.MayaCheckoutQr)
        {
            var config = await dbContext.ClientPaymentProviderConfigs
                .SingleOrDefaultAsync(
                    item =>
                        item.ClientAccountId == clientAccountId &&
                        item.Provider == StatusValues.PaymentProvider.Maya &&
                        item.IntegrationType == StatusValues.PaymentMethod.MayaCheckoutQr,
                    cancellationToken);

            if (config is null)
            {
                config = new ClientPaymentProviderConfig
                {
                    Id = Guid.NewGuid(),
                    ClientAccountId = clientAccountId,
                    Provider = StatusValues.PaymentProvider.Maya,
                    IntegrationType = StatusValues.PaymentMethod.MayaCheckoutQr,
                    Status = request.Enabled ? StatusValues.PaymentResource.Draft : StatusValues.PaymentResource.Disabled
                };
                dbContext.ClientPaymentProviderConfigs.Add(config);
            }
            else
            {
                config.Status = request.Enabled ? StatusValues.PaymentResource.Draft : StatusValues.PaymentResource.Disabled;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await auditService.WriteAsync(currentUser, "payment_resource.updated", nameof(ClientPaymentProviderConfig), config.Id, new { paymentMethod, config.Status }, cancellationToken);
            summary = ToPaymentResourceSummary(clientAccountId, paymentMethod, config.Status);
        }
        else
        {
            var device = await dbContext.ClientMayaEcrDevices
                .OrderBy(item => item.DisplayName)
                .FirstOrDefaultAsync(
                    item =>
                        item.ClientAccountId == clientAccountId &&
                        item.Provider == StatusValues.PaymentProvider.Maya,
                    cancellationToken);

            if (device is null)
            {
                device = new ClientMayaEcrDevice
                {
                    Id = Guid.NewGuid(),
                    ClientAccountId = clientAccountId,
                    DisplayName = "Maya Terminal ECR",
                    DeviceId = "MAYA-ECR-DRAFT",
                    Provider = StatusValues.PaymentProvider.Maya,
                    Status = request.Enabled ? StatusValues.PaymentResource.Draft : StatusValues.PaymentResource.Disabled
                };
                dbContext.ClientMayaEcrDevices.Add(device);
            }
            else
            {
                device.Status = request.Enabled ? StatusValues.PaymentResource.Draft : StatusValues.PaymentResource.Disabled;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await auditService.WriteAsync(currentUser, "payment_resource.updated", nameof(ClientMayaEcrDevice), device.Id, new { paymentMethod, device.Status }, cancellationToken);
            summary = ToPaymentResourceSummary(clientAccountId, paymentMethod, device.Status);
        }

        return TypedResults.Ok(summary);
    }

    private static async Task<Results<Ok<PaymentAssignmentSummary>, ForbidHttpResult, ValidationProblem>> AssignPaymentOptionAsync(
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

        if (!IsKnownPaymentMethod(request.PaymentMethod))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["paymentMethod"] = ["Payment method is not supported."]
            });
        }

        var booth = await ApplyClientScope(dbContext.Booths.AsNoTracking(), currentUser, item => item.ClientAccountId)
            .SingleOrDefaultAsync(item => item.Id == boothId, cancellationToken);

        if (booth is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["boothId"] = ["Booth was not found in the selected client account."]
            });
        }

        var runtimeEnabled = request.PaymentMethod == StatusValues.PaymentMethod.Cash && request.RuntimeEnabled;

        if (request.PaymentMethod != StatusValues.PaymentMethod.Cash)
        {
            runtimeEnabled = false;
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
                RuntimeEnabled = runtimeEnabled,
                Status = runtimeEnabled ? StatusValues.PaymentAssignment.Assigned : StatusValues.PaymentAssignment.Locked,
                AssignedAt = DateTimeOffset.UtcNow
            };
            dbContext.BoothPaymentOptionAssignments.Add(assignment);
        }
        else
        {
            assignment.RuntimeEnabled = runtimeEnabled;
            assignment.Status = runtimeEnabled ? StatusValues.PaymentAssignment.Assigned : StatusValues.PaymentAssignment.Locked;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth_payment_option.updated", nameof(BoothPaymentOptionAssignment), assignment.Id, new { boothId, assignment.PaymentMethod, assignment.RuntimeEnabled }, cancellationToken);

        return TypedResults.Ok(new PaymentAssignmentSummary(assignment.Id, assignment.BoothId, assignment.PaymentMethod, assignment.RuntimeEnabled, assignment.Status));
    }

    private static async Task<Results<Ok<PaymentAssignmentSummary>, ForbidHttpResult, ValidationProblem>> DisablePaymentOptionAsync(
        Guid boothId,
        string paymentMethod,
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

        if (!IsKnownPaymentMethod(paymentMethod))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["paymentMethod"] = ["Payment method is not supported."]
            });
        }

        var booth = await ApplyClientScope(dbContext.Booths.AsNoTracking(), currentUser, item => item.ClientAccountId)
            .SingleOrDefaultAsync(item => item.Id == boothId, cancellationToken);

        if (booth is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["boothId"] = ["Booth was not found in the selected client account."]
            });
        }

        var assignment = await dbContext.BoothPaymentOptionAssignments
            .SingleOrDefaultAsync(item => item.BoothId == boothId && item.PaymentMethod == paymentMethod, cancellationToken);

        if (assignment is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["paymentMethod"] = ["Payment assignment was not found for this booth."]
            });
        }

        assignment.RuntimeEnabled = false;
        assignment.Status = StatusValues.PaymentAssignment.Disabled;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "booth_payment_option.disabled", nameof(BoothPaymentOptionAssignment), assignment.Id, new { boothId, assignment.PaymentMethod }, cancellationToken);

        return TypedResults.Ok(new PaymentAssignmentSummary(assignment.Id, assignment.BoothId, assignment.PaymentMethod, assignment.RuntimeEnabled, assignment.Status));
    }

    private static async Task<Results<Ok<BoothConfigResponse>, UnauthorizedHttpResult, ValidationProblem>> GetBoothConfigAsync(
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        PhotoBizTransactionWorkflow workflow,
        PhotoBizTokenHasher tokenHasher,
        CancellationToken cancellationToken)
    {
        var booth = await ResolveBoothFromKioskTokenAsync(httpContext, dbContext, tokenHasher, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Unauthorized();
        }

        await workflow.ExpirePendingTransactionsAsync(cancellationToken, booth.Id);

        var now = DateTimeOffset.UtcNow;
        var effectiveBoothState = PhotoBizBoothAvailability.GetEffectiveState(booth, now);
        var client = await dbContext.ClientAccounts.SingleAsync(item => item.Id == booth.ClientAccountId, cancellationToken);
        var location = await dbContext.Locations.SingleAsync(item => item.Id == booth.LocationId && item.ClientAccountId == booth.ClientAccountId, cancellationToken);
        var appearance = await dbContext.BoothAppearanceConfigs.SingleOrDefaultAsync(item => item.BoothId == booth.Id, cancellationToken);
        var activeTransaction = await GetActiveBoothTransactionAsync(
            dbContext,
            booth.Id,
            cancellationToken);
        var recentTransaction = await GetRecentBoothTerminalTransactionAsync(
            dbContext,
            booth.Id,
            cancellationToken);
        var runtimeGate = await PhotoBizRuntimeAvailability.CheckBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: false,
            cancellationToken);

        if (!runtimeGate.Succeeded)
        {
            return TypedResults.Ok(ToUnavailableConfig(booth, client, location, appearance, effectiveBoothState, runtimeGate.Message, activeTransaction, recentTransaction));
        }

        var selectedActivation = await dbContext.BoothOfferActivations
            .Include(item => item.BoothOffer)
            .Where(item =>
                item.BoothId == booth.Id &&
                (item.Status == StatusValues.OfferActivation.Active ||
                    item.Status == StatusValues.OfferActivation.PendingPayment))
            .OrderByDescending(item => item.Status == StatusValues.OfferActivation.PendingPayment)
            .ThenByDescending(item => item.ActivatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (selectedActivation?.BoothOffer is null)
        {
            return TypedResults.Ok(ToUnavailableConfig(booth, client, location, appearance, effectiveBoothState, "No active booth offer configured", activeTransaction, recentTransaction));
        }

        var paymentOptions = await dbContext.BoothPaymentOptionAssignments
            .Where(item => item.BoothId == booth.Id)
            .OrderBy(item => item.PaymentMethod)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new BoothConfigResponse(
            new BoothClientResponse(client.DisplayNameOrName(), null),
            ToThemeResponse(appearance),
            ToSessionResponse(appearance),
            ToBoothStateResponse(booth, location, effectiveBoothState),
            ToBoothOfferResponse(selectedActivation),
            paymentOptions
                .Where(item =>
                    selectedActivation.Status == StatusValues.OfferActivation.Active &&
                    selectedActivation.BoothOffer.OfferType == StatusValues.OfferType.PerSession &&
                    item.Status == StatusValues.PaymentAssignment.Assigned &&
                    item.RuntimeEnabled &&
                    item.PaymentMethod == StatusValues.PaymentMethod.Cash)
                .Select(item => new BoothPaymentOptionResponse(item.PaymentMethod, ToPaymentLabel(item.PaymentMethod), item.RuntimeEnabled))
                .ToArray(),
            activeTransaction,
            recentTransaction));
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

        var runtimeGate = await PhotoBizRuntimeAvailability.CheckBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: true,
            cancellationToken);

        if (!runtimeGate.Succeeded)
        {
            return ToValidationProblem(runtimeGate);
        }

        var activeActivation = await dbContext.BoothOfferActivations
            .Include(item => item.BoothOffer)
            .Where(item => item.BoothId == booth.Id && item.Status == StatusValues.OfferActivation.Active)
            .OrderByDescending(item => item.ActivatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeActivation?.BoothOffer is null)
        {
            var pendingActivation = await dbContext.BoothOfferActivations
                .AsNoTracking()
                .AnyAsync(
                    item => item.BoothId == booth.Id &&
                        item.Status == StatusValues.OfferActivation.PendingPayment,
                    cancellationToken);

            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["booth"] =
                [
                    pendingActivation
                        ? "This package is awaiting cashier activation."
                        : "The booth does not have an active offer."
                ]
            });
        }

        if (PhotoBizBoothAvailability.GetEffectiveState(booth, DateTimeOffset.UtcNow) != StatusValues.Booth.Welcome)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["booth"] = ["The booth is not ready for a new transaction yet."]
            });
        }

        try
        {
            var transaction = activeActivation.BoothOffer.OfferType == StatusValues.OfferType.PerSession
                ? await workflow.CreateTransactionAsync(booth, activeActivation, activeActivation.BoothOffer, cancellationToken)
                : await workflow.CreateCoveredPlanSessionAsync(booth, activeActivation, activeActivation.BoothOffer, cancellationToken);
            return TypedResults.Ok(ToTransactionSummary(transaction));
        }
        catch (InvalidOperationException exception) when (exception.Message == "The booth already has an active transaction.")
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["booth"] = ["The booth already has an active session in progress. Finish, cancel, or expire it before starting another one."]
            });
        }
        catch (InvalidOperationException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["offer"] = [exception.Message]
            });
        }
    }

    private static async Task<Results<Ok<TransactionSummary>, ForbidHttpResult, ValidationProblem>> CreatePlanActivationAsync(
        Guid boothId,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizTransactionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var currentUser = await ResolveCashierEndpointUserAsync(
            dbContext,
            principal.GetRequiredCurrentUser(),
            user => user.CanApproveCash,
            cancellationToken);

        if (currentUser is null)
        {
            return TypedResults.Forbid();
        }

        var booth = await LoadCashierScopedBoothAsync(dbContext, currentUser, boothId, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Forbid();
        }

        var runtimeGate = await PhotoBizRuntimeAvailability.CheckBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: false,
            cancellationToken);

        if (!runtimeGate.Succeeded)
        {
            return ToValidationProblem(runtimeGate);
        }

        var pendingActivation = await dbContext.BoothOfferActivations
            .Include(item => item.BoothOffer)
            .Where(item =>
                item.BoothId == booth.Id &&
                item.Status == StatusValues.OfferActivation.PendingPayment)
            .OrderByDescending(item => item.ActivatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (pendingActivation?.BoothOffer is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["activation"] = ["This booth does not have a package awaiting cashier activation."]
            });
        }

        try
        {
            var transaction = await workflow.CreatePlanActivationAsync(booth, pendingActivation, pendingActivation.BoothOffer, currentUser, cancellationToken);
            return TypedResults.Ok(ToTransactionSummary(transaction));
        }
        catch (InvalidOperationException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["activation"] = [exception.Message]
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

        var runtimeGate = await PhotoBizRuntimeAvailability.CheckBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: true,
            cancellationToken);

        if (!runtimeGate.Succeeded)
        {
            return ToValidationProblem(runtimeGate);
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
        catch (InvalidOperationException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["paymentMethod"] = [exception.Message]
            });
        }

        return TypedResults.Ok(ToTransactionSummary(transaction));
    }

    private static async Task<Results<Ok<TransactionSummary>, UnauthorizedHttpResult, ValidationProblem>> CancelBoothUiTransactionAsync(
        Guid transactionId,
        CancelBoothUiTransactionRequest request,
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

        var transaction = await dbContext.Transactions
            .SingleOrDefaultAsync(item => item.Id == transactionId && item.BoothId == booth.Id, cancellationToken);

        if (transaction is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["transaction"] = ["Transaction was not found for this booth."]
            });
        }

        var trigger = request.Trigger?.Trim().ToUpperInvariant();
        if (trigger is not (StatusValues.BoothUiCancelTrigger.BackButton or StatusValues.BoothUiCancelTrigger.IdleTimeout))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["trigger"] = ["Cancellation trigger must be BACK_BUTTON or IDLE_TIMEOUT."]
            });
        }

        try
        {
            transaction = await workflow.CancelFromKioskAsync(transaction, booth, trigger, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["transaction"] = [exception.Message]
            });
        }

        return TypedResults.Ok(ToTransactionSummary(transaction));
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult, ValidationProblem>> AcknowledgeRecentBoothUiTransactionAsync(
        Guid transactionId,
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

        var transaction = await dbContext.Transactions
            .SingleOrDefaultAsync(item => item.Id == transactionId && item.BoothId == booth.Id, cancellationToken);

        if (transaction is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["transaction"] = ["Transaction was not found for this booth."]
            });
        }

        if (!IsKioskAcknowledgeableTerminalStatus(transaction.Status))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["transaction"] = ["Only expired, cancelled, or failed payment notices can be acknowledged from the booth UI."]
            });
        }

        transaction.TerminalNoticeAcknowledgedAt ??= DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult, ValidationProblem>> ReturnBoothUiToWelcomeAsync(
        HttpContext httpContext,
        PhotoBizDbContext dbContext,
        PhotoBizTransactionWorkflow workflow,
        PhotoBizTokenHasher tokenHasher,
        CancellationToken cancellationToken)
    {
        var booth = await ResolveBoothFromKioskTokenAsync(httpContext, dbContext, tokenHasher, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await workflow.ReturnCompletedBoothToWelcomeAsync(booth, completedAtCutoff: null, cancellationToken);

        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["booth"] = [GetReturnToWelcomeValidationMessage(result.Status)]
            });
        }

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<TransactionSummary>, ForbidHttpResult, ValidationProblem>> ApproveCashAsync(
        Guid transactionId,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizTransactionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var currentUser = await ResolveCashierEndpointUserAsync(
            dbContext,
            principal.GetRequiredCurrentUser(),
            user => user.CanApproveCash,
            cancellationToken);

        if (currentUser is null)
        {
            return TypedResults.Forbid();
        }

        var transaction = await LoadCashierScopedTransactionAsync(dbContext, currentUser, transactionId, cancellationToken);

        if (transaction is null)
        {
            return TypedResults.Forbid();
        }

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
        var runtimeGate = await PhotoBizRuntimeAvailability.CheckBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: transaction.TransactionType != StatusValues.TransactionType.PlanActivation,
            cancellationToken);

        if (!runtimeGate.Succeeded)
        {
            return ToValidationProblem(runtimeGate);
        }

        try
        {
            var updated = await workflow.ApproveCashAsync(transaction, currentUser, cancellationToken);
            return TypedResults.Ok(ToTransactionSummary(updated));
        }
        catch (InvalidOperationException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["transaction"] = [exception.Message]
            });
        }
    }

    private static async Task<Results<Ok<TransactionSummary>, ForbidHttpResult, ValidationProblem>> CancelTransactionAsync(
        Guid transactionId,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizTransactionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var currentUser = await ResolveCashierEndpointUserAsync(
            dbContext,
            principal.GetRequiredCurrentUser(),
            user => user.CanCancelTransaction,
            cancellationToken);

        if (currentUser is null)
        {
            return TypedResults.Forbid();
        }

        var transaction = await LoadCashierScopedTransactionAsync(dbContext, currentUser, transactionId, cancellationToken);

        if (transaction is null)
        {
            return TypedResults.Forbid();
        }

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == transaction.BoothId, cancellationToken);
        var runtimeGate = await PhotoBizRuntimeAvailability.CheckBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: false,
            cancellationToken);

        if (!runtimeGate.Succeeded)
        {
            return ToValidationProblem(runtimeGate);
        }

        try
        {
            var updated = await workflow.CancelAsync(transaction, currentUser, cancellationToken);
            return TypedResults.Ok(ToTransactionSummary(updated));
        }
        catch (InvalidOperationException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["transaction"] = [exception.Message]
            });
        }
    }

    private static async Task<Results<Ok<TransactionSummary>, ForbidHttpResult, ValidationProblem>> CreateExtraPrintAddOnAsync(
        Guid parentTransactionId,
        CreateExtraPrintAddOnRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizTransactionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var currentUser = await ResolveCashierEndpointUserAsync(
            dbContext,
            principal.GetRequiredCurrentUser(),
            _ => true,
            cancellationToken);

        if (currentUser is null)
        {
            return TypedResults.Forbid();
        }

        var parentTransaction = await LoadCashierScopedTransactionAsync(dbContext, currentUser, parentTransactionId, cancellationToken);

        if (parentTransaction is null)
        {
            return TypedResults.Forbid();
        }

        var booth = await dbContext.Booths.SingleAsync(item => item.Id == parentTransaction.BoothId, cancellationToken);
        var runtimeGate = await PhotoBizRuntimeAvailability.CheckBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: true,
            cancellationToken);

        if (!runtimeGate.Succeeded)
        {
            return ToValidationProblem(runtimeGate);
        }

        try
        {
            var addOn = await workflow.CreateExtraPrintAddOnAsync(parentTransaction, currentUser, request.CopyCount, cancellationToken);
            return TypedResults.Ok(ToTransactionSummary(addOn));
        }
        catch (InvalidOperationException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["extraPrints"] = [exception.Message]
            });
        }
    }

    private static async Task<Results<Ok, ForbidHttpResult, ValidationProblem>> ReturnBoothToWelcomeAsync(
        Guid boothId,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizTransactionWorkflow workflow,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = await ResolveCashierEndpointUserAsync(
            dbContext,
            principal.GetRequiredCurrentUser(),
            user => user.CanReturnBoothToWelcome,
            cancellationToken);

        if (currentUser is null)
        {
            return TypedResults.Forbid();
        }

        var booth = await LoadCashierScopedBoothAsync(dbContext, currentUser, boothId, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Forbid();
        }

        var runtimeGate = await PhotoBizRuntimeAvailability.CheckBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: false,
            cancellationToken);

        if (!runtimeGate.Succeeded)
        {
            return ToValidationProblem(runtimeGate);
        }

        await workflow.ReturnBoothToWelcomeAsync(booth, currentUser, cancellationToken);

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

        if (!await IsAgentSurfaceAvailableAsync(dbContext, booth, cancellationToken))
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(new AgentPairResponse(booth.Id, booth.Name, booth.Code));
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult>> AgentHeartbeatAsync(
        AgentHeartbeatRequest request,
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

        if (!await IsAgentSurfaceAvailableAsync(dbContext, booth, cancellationToken))
        {
            return TypedResults.Unauthorized();
        }

        var now = DateTimeOffset.UtcNow;
        PhotoBizBoothAvailability.MarkAgentHeartbeat(booth, now);
        ApplyAgentHeartbeatMetadata(booth, request, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult>> AgentOfflineAsync(
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

        if (!await IsAgentSurfaceAvailableAsync(dbContext, booth, cancellationToken))
        {
            return TypedResults.Unauthorized();
        }

        PhotoBizBoothAvailability.MarkAgentOffline(booth, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<AgentBoothUiLaunchResponse>, UnauthorizedHttpResult>> CreateAgentBoothUiLaunchAsync(
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

        if (!await IsAgentSurfaceAvailableAsync(dbContext, booth, cancellationToken))
        {
            return TypedResults.Unauthorized();
        }

        var kioskToken = tokenHasher.GenerateOpaqueToken();
        booth.KioskTokenHash = tokenHasher.Hash(kioskToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new AgentBoothUiLaunchResponse(booth.Id, booth.Code, kioskToken));
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

        if (!await IsAgentSurfaceAvailableAsync(dbContext, booth, cancellationToken))
        {
            return TypedResults.Unauthorized();
        }

        var runtimeGate = await PhotoBizRuntimeAvailability.CheckBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: true,
            requireAgent: false,
            cancellationToken);

        if (!runtimeGate.Succeeded)
        {
            return TypedResults.NoContent();
        }

        var command = await workflow.TryAcquireNextAgentCommandAsync(booth, cancellationToken);

        if (command is null)
        {
            return TypedResults.NoContent();
        }

        var commandName = command.TransactionType == StatusValues.TransactionType.ExtraPrintAddOn
            ? "PRINT_COPIES"
            : "START_SESSION";

        return TypedResults.Ok(new AgentCommandResponse(
            command.TransactionId,
            command.TransactionNumber,
            commandName,
            command.LumaboothSessionMode,
            command.OfferType,
            command.TransactionType,
            command.IncludedPrintEntitlement,
            command.ExtraPrintCount));
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

        await workflow.MarkSessionStartedAsync(transactionId, booth.Id, request.LumaboothSessionRef, cancellationToken);
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

        await workflow.MarkSessionCompletedAsync(transactionId, booth.Id, request.LumaboothSessionRef, cancellationToken);
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

        await workflow.MarkSessionFailedAsync(transactionId, booth.Id, request.Reason, request.LumaboothSessionRef, cancellationToken);
        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult>> AgentPrintCompletedAsync(
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

        await workflow.MarkPrintCompletedAsync(transactionId, booth.Id, cancellationToken);
        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult>> AgentPrintFailedAsync(
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

        await workflow.MarkPrintFailedAsync(transactionId, booth.Id, request.Reason, cancellationToken);
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
        if (!currentUser.AssignedBoothId.HasValue)
        {
            return query.Where(_ => false);
        }

        var body = Expression.Equal(
            boothIdSelector.Body,
            Expression.Constant(currentUser.AssignedBoothId.Value));

        return query.Where(Expression.Lambda<Func<T, bool>>(body, parameter));
    }

    private static IQueryable<T> ApplyScopedBoothIds<T>(
        IQueryable<T> query,
        PhotoBizCurrentUser currentUser,
        IReadOnlyCollection<Guid> boothIds,
        Expression<Func<T, Guid>> boothIdSelector)
    {
        if (currentUser.IsApplicationOwner)
        {
            return query;
        }

        var parameter = boothIdSelector.Parameters[0];
        var body = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Contains),
            [typeof(Guid)],
            Expression.Constant(boothIds),
            boothIdSelector.Body);

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
            if (!currentUser.AssignedBoothId.HasValue)
            {
                return query.Where(item => item.Id == currentUser.UserId);
            }

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

    private static IQueryable<AuditLog> ApplyAuditLogScope(IQueryable<AuditLog> query, PhotoBizCurrentUser currentUser)
    {
        if (currentUser.IsApplicationOwner)
        {
            return query;
        }

        if (currentUser.IsCashier)
        {
            return query.Where(item => item.UserId == currentUser.UserId);
        }

        return query.Where(item => item.ClientAccountId == currentUser.ClientAccountId);
    }

    private static ValidationProblem ToValidationProblem(PhotoBizRuntimeGateResult runtimeGate)
    {
        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [string.IsNullOrWhiteSpace(runtimeGate.Key) ? "booth" : runtimeGate.Key] = [runtimeGate.Message]
        });
    }

    private static BoothSummary BuildBoothSummary(Booth booth, string currentState)
    {
        return new BoothSummary(
            booth.Id,
            booth.ClientAccountId,
            booth.LocationId,
            booth.Name,
            booth.Code,
            booth.Status,
            currentState,
            booth.LastHeartbeatAt,
            new AgentStatusSummary(
                booth.AgentHealthStatus,
                StatusValues.AgentUpdate.Unknown,
                booth.AgentVersion,
                booth.AgentRuntimeKind,
                booth.AgentKioskRunning ?? false,
                booth.AgentLumaBoothMode,
                booth.AgentApiReachable,
                booth.AgentChromeLaunched,
                booth.AgentTriggerListenerRunning,
                booth.AgentLumaBoothReachable,
                booth.AgentMetadataUpdatedAt));
    }

    private static void ApplyAgentHeartbeatMetadata(Booth booth, AgentHeartbeatRequest request, DateTimeOffset now)
    {
        booth.AgentVersion = NormalizeAgentMetadataValue(request.AgentVersion, AgentVersionMaxLength);
        booth.AgentRuntimeKind = NormalizeAgentMetadataValue(request.RuntimeKind, AgentRuntimeKindMaxLength);
        booth.AgentKioskRunning = request.KioskRunning;
        booth.AgentLumaBoothMode = NormalizeAgentMetadataValue(request.LumaBoothMode, AgentLumaBoothModeMaxLength);
        booth.AgentApiReachable = request.ApiReachable;
        booth.AgentChromeLaunched = request.ChromeLaunched;
        booth.AgentTriggerListenerRunning = request.TriggerListenerRunning;
        booth.AgentLumaBoothReachable = request.LumaBoothReachable;
        booth.AgentHealthStatus = DetermineAgentHealthStatus(request);
        booth.AgentMetadataUpdatedAt = now;
    }

    private static string DetermineAgentHealthStatus(AgentHeartbeatRequest request)
    {
        var flags = new[]
        {
            request.KioskRunning,
            request.ApiReachable,
            request.ChromeLaunched,
            request.TriggerListenerRunning,
            request.LumaBoothReachable
        };

        return flags.Any(flag => flag == false)
            ? StatusValues.AgentHealth.Degraded
            : StatusValues.AgentHealth.Ok;
    }

    private static string? NormalizeAgentMetadataValue(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static async Task<bool> IsAgentSurfaceAvailableAsync(
        PhotoBizDbContext dbContext,
        Booth booth,
        CancellationToken cancellationToken)
    {
        var gate = await PhotoBizRuntimeAvailability.CheckBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription: false,
            requireAgent: false,
            cancellationToken);

        return gate.Succeeded;
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
        if (string.IsNullOrWhiteSpace(boothCode))
        {
            return null;
        }

        if (!httpContext.Request.Headers.TryGetValue("X-Agent-Credential", out var credentialValues))
        {
            return null;
        }

        var credential = credentialValues.ToString();
        var normalizedBoothCode = boothCode.Trim().ToUpperInvariant();
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

    private static async Task<Transaction?> LoadCashierScopedTransactionAsync(
        PhotoBizDbContext dbContext,
        PhotoBizCurrentUser currentUser,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.AssignedBoothId.HasValue)
        {
            return null;
        }

        var transaction = await dbContext.Transactions.SingleOrDefaultAsync(item => item.Id == transactionId, cancellationToken);

        return transaction?.BoothId == currentUser.AssignedBoothId.Value ? transaction : null;
    }

    private static async Task<Booth?> LoadScopedBoothAsync(
        PhotoBizDbContext dbContext,
        PhotoBizCurrentUser currentUser,
        Guid boothId,
        CancellationToken cancellationToken)
    {
        var booth = await dbContext.Booths.SingleOrDefaultAsync(item => item.Id == boothId, cancellationToken);

        if (booth is null)
        {
            return null;
        }

        if (currentUser.IsApplicationOwner)
        {
            return booth;
        }

        if (currentUser.IsCashier)
        {
            return booth.Id == currentUser.AssignedBoothId ? booth : null;
        }

        return booth.ClientAccountId == currentUser.ClientAccountId ? booth : null;
    }

    private static async Task<Booth?> LoadCashierScopedBoothAsync(
        PhotoBizDbContext dbContext,
        PhotoBizCurrentUser currentUser,
        Guid boothId,
        CancellationToken cancellationToken)
    {
        if (currentUser.AssignedBoothId != boothId)
        {
            return null;
        }

        return await dbContext.Booths.SingleOrDefaultAsync(item => item.Id == boothId, cancellationToken);
    }

    private static async Task<PhotoBizCurrentUser?> ResolveCashierEndpointUserAsync(
        PhotoBizDbContext dbContext,
        PhotoBizCurrentUser currentUser,
        Func<ApplicationUser, bool> permissionSelector,
        CancellationToken cancellationToken)
    {
        if (!IsPosAssignableRole(currentUser.Role))
        {
            return null;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Include(item => item.ClientAccount)
            .SingleOrDefaultAsync(item => item.Id == currentUser.UserId, cancellationToken);

        if (user is null ||
            user.Status != StatusValues.User.Active ||
            user.ClientAccount?.Status != StatusValues.ClientAccount.Active ||
            !IsPosAssignableRole(user.Role) ||
            !user.AssignedBoothId.HasValue)
        {
            return null;
        }

        if (user.Role == StatusValues.User.Cashier && !permissionSelector(user))
        {
            return null;
        }

        return new PhotoBizCurrentUser(user.Id, user.Role, user.ClientAccountId, user.AssignedBoothId);
    }

    private static AuthSessionResponse ToSessionResponse(ApplicationUser user)
    {
        return new AuthSessionResponse(
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.ClientAccountId,
            user.AssignedBoothId,
            user.MustChangePassword,
            IsCashierPermissionEnabled(user.Role, user.CanApproveCash),
            IsCashierPermissionEnabled(user.Role, user.CanReturnBoothToWelcome),
            IsCashierPermissionEnabled(user.Role, user.CanCancelTransaction));
    }

    private static async Task SignInUserAsync(HttpContext httpContext, ApplicationUser user)
    {
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
    }

    private static UserSummary ToUserSummary(ApplicationUser user)
    {
        return new UserSummary(
            user.Id,
            user.ClientAccountId,
            user.Name,
            user.Email,
            user.Role,
            user.Status,
            user.AssignedBoothId,
            IsCashierPermissionEnabled(user.Role, user.CanApproveCash),
            IsCashierPermissionEnabled(user.Role, user.CanReturnBoothToWelcome),
            IsCashierPermissionEnabled(user.Role, user.CanCancelTransaction));
    }

    private static bool IsCashierPermissionEnabled(string role, bool? permission)
    {
        return role == StatusValues.User.Cashier
            ? permission.GetValueOrDefault(true)
            : IsPosAssignableRole(role);
    }

    private static TransactionSummary ToTransactionSummary(Transaction transaction)
    {
        var extraPrintUnitPriceCents = TryGetExtraPrintUnitPriceCents(transaction);
        var offerSnapshot = ReadOfferSnapshot(transaction);

        return new TransactionSummary(
            transaction.Id,
            transaction.BoothId,
            transaction.BoothOfferActivationId,
            transaction.TransactionNumber,
            transaction.TransactionType,
            transaction.Status,
            transaction.PaymentMethod,
            transaction.AmountCents,
            transaction.ParentTransactionId,
            transaction.ExtraPrintCount,
            CanCreateExtraPrintAddOn(transaction, isExtraPrintReference: false, extraPrintUnitPriceCents),
            extraPrintUnitPriceCents,
            offerSnapshot.OfferName,
            offerSnapshot.OfferType,
            offerSnapshot.IncludedPrintEntitlement,
            offerSnapshot.SessionAllowance,
            (int?)null,
            transaction.CreatedAt,
            transaction.PaidAt,
            transaction.CompletedAt,
            transaction.CancelledAt,
            transaction.FailureReason,
            transaction.CancelledByActorType,
            transaction.CancelledByUserId,
            transaction.CancellationSource,
            transaction.CancellationPreviousStatus);
    }

    private static BoothActiveTransactionResponse ToBoothActiveTransactionResponse(Transaction transaction)
    {
        return new BoothActiveTransactionResponse(
            transaction.Id,
            transaction.TransactionNumber,
            transaction.TransactionType,
            transaction.Status,
            transaction.PaymentMethod,
            transaction.AmountCents,
            transaction.Currency,
            transaction.CreatedAt,
            transaction.ExpiresAt);
    }

    private static IEnumerable<TransactionSummary> ToTransactionSummaries(IReadOnlyCollection<Transaction> transactions)
    {
        var extraPrintReferenceIdsByBooth = transactions
            .GroupBy(transaction => transaction.BoothId)
            .ToDictionary(
                group => group.Key,
                group => ResolveExtraPrintReferenceTransactionId(group
                    .OrderByDescending(transaction => transaction.CreatedAt)
                    .ThenByDescending(transaction => transaction.Id)
                    .ToArray()));
        var coveredSessionSequencesById = transactions
            .Where(transaction =>
                transaction.TransactionType == StatusValues.TransactionType.CoveredPlanSession &&
                transaction.Status == StatusValues.Transaction.Completed &&
                transaction.BoothOfferActivationId.HasValue)
            .GroupBy(transaction => transaction.BoothOfferActivationId!.Value)
            .SelectMany(group => group
                .OrderBy(transaction => transaction.CompletedAt ?? transaction.CreatedAt)
                .ThenBy(transaction => transaction.CreatedAt)
                .ThenBy(transaction => transaction.Id)
                .Select((transaction, index) => new { transaction.Id, Sequence = index + 1 }))
            .ToDictionary(item => item.Id, item => item.Sequence);

        foreach (var transaction in transactions)
        {
            var extraPrintUnitPriceCents = TryGetExtraPrintUnitPriceCents(transaction);
            var offerSnapshot = ReadOfferSnapshot(transaction);
            var isExtraPrintReference =
                extraPrintReferenceIdsByBooth.TryGetValue(transaction.BoothId, out var referenceTransactionId) &&
                referenceTransactionId == transaction.Id;

            yield return new TransactionSummary(
                transaction.Id,
                transaction.BoothId,
                transaction.BoothOfferActivationId,
                transaction.TransactionNumber,
                transaction.TransactionType,
                transaction.Status,
                transaction.PaymentMethod,
                transaction.AmountCents,
                transaction.ParentTransactionId,
                transaction.ExtraPrintCount,
                CanCreateExtraPrintAddOn(transaction, isExtraPrintReference, extraPrintUnitPriceCents),
                extraPrintUnitPriceCents,
                offerSnapshot.OfferName,
                offerSnapshot.OfferType,
                offerSnapshot.IncludedPrintEntitlement,
                offerSnapshot.SessionAllowance,
                coveredSessionSequencesById.TryGetValue(transaction.Id, out var coveredSessionSequence)
                    ? coveredSessionSequence
                    : null,
                transaction.CreatedAt,
                transaction.PaidAt,
                transaction.CompletedAt,
                transaction.CancelledAt,
                transaction.FailureReason,
                transaction.CancelledByActorType,
                transaction.CancelledByUserId,
                transaction.CancellationSource,
                transaction.CancellationPreviousStatus);
        }
    }

    private static bool CanCreateExtraPrintAddOn(
        Transaction transaction,
        bool isExtraPrintReference,
        int? extraPrintUnitPriceCents)
    {
        if (!isExtraPrintReference ||
            transaction.TransactionType != StatusValues.TransactionType.SessionPurchase ||
            transaction.Status != StatusValues.Transaction.Completed ||
            extraPrintUnitPriceCents is null or <= 0)
        {
            return false;
        }

        try
        {
            using var snapshot = JsonDocument.Parse(transaction.OfferSnapshot);
            var root = snapshot.RootElement;
            return root.TryGetProperty("OfferType", out var offerType) &&
                offerType.GetString() == StatusValues.OfferType.PerSession &&
                root.TryGetProperty("AllowsExtraPrintAddOn", out var allowsAddOn) &&
                allowsAddOn.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Guid? ResolveExtraPrintReferenceTransactionId(Transaction[] orderedTransactions)
    {
        foreach (var transaction in orderedTransactions)
        {
            if (IsTransactionInLumaboothSession(transaction))
            {
                return null;
            }

            if (IsTransactionBeforeLumaboothSession(transaction))
            {
                continue;
            }

            if (!IsSessionTransaction(transaction))
            {
                if (!IsTerminalTransactionStatus(transaction.Status))
                {
                    return null;
                }

                continue;
            }

            return transaction.Id;
        }

        return null;
    }

    private static bool IsTransactionBeforeLumaboothSession(Transaction transaction)
    {
        return IsSessionTransaction(transaction) &&
            transaction.Status is StatusValues.Transaction.Created
                or StatusValues.Transaction.PendingCash
                or StatusValues.Transaction.Paid;
    }

    private static bool IsSessionTransaction(Transaction transaction)
    {
        return transaction.TransactionType is StatusValues.TransactionType.SessionPurchase
            or StatusValues.TransactionType.CoveredPlanSession;
    }

    private static bool IsTransactionInLumaboothSession(Transaction transaction)
    {
        return transaction.Status is StatusValues.Transaction.StartingSession or StatusValues.Transaction.InSession;
    }

    private static int? TryGetExtraPrintUnitPriceCents(Transaction transaction)
    {
        try
        {
            using var snapshot = JsonDocument.Parse(transaction.OfferSnapshot);
            return snapshot.RootElement.TryGetProperty("ExtraPrintPriceCents", out var price) &&
                price.ValueKind == JsonValueKind.Number &&
                price.TryGetInt32(out var value)
                    ? value
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TransactionOfferSnapshot ReadOfferSnapshot(Transaction transaction)
    {
        try
        {
            using var snapshot = JsonDocument.Parse(transaction.OfferSnapshot);
            var root = snapshot.RootElement;
            return new TransactionOfferSnapshot(
                GetOptionalSnapshotString(root, "Name"),
                GetOptionalSnapshotString(root, "OfferType"),
                GetOptionalSnapshotString(root, "IncludedPrintEntitlement"),
                GetOptionalSnapshotInt(root, "SessionAllowance"));
        }
        catch (JsonException)
        {
            return new TransactionOfferSnapshot(null, null, null, null);
        }
    }

    private static string? GetOptionalSnapshotString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static int? GetOptionalSnapshotInt(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var result)
                ? result
                : null;
    }

    private static OfferSummary ToOfferSummary(BoothOffer offer)
    {
        return new OfferSummary(
            offer.Id,
            offer.ClientAccountId,
            offer.Name,
            offer.Description,
            offer.OfferType,
            offer.PriceCents,
            offer.Currency,
            offer.IncludedPrintEntitlement,
            offer.DurationHours,
            offer.SessionAllowance,
            offer.AllowsExtraPrintAddOn,
            offer.ExtraPrintPriceCents,
            offer.LumaboothSessionMode,
            offer.Active);
    }

    private static OfferActivationSummary ToOfferActivationSummary(BoothOfferActivation activation)
    {
        return new OfferActivationSummary(
            activation.Id,
            activation.BoothId,
            activation.BoothOfferId,
            activation.Status,
            activation.StartsAt,
            activation.EndsAt,
            activation.SessionAllowance,
            activation.SessionsUsed);
    }

    private static BoothOfferResponse ToBoothOfferResponse(BoothOfferActivation activation)
    {
        var offer = activation.BoothOffer ?? throw new InvalidOperationException("Offer activation was loaded without its booth offer.");
        return new BoothOfferResponse(
            offer.Id,
            offer.Name,
            offer.OfferType,
            offer.PriceCents,
            offer.Currency,
            offer.IncludedPrintEntitlement,
            offer.AllowsExtraPrintAddOn,
            offer.ExtraPrintPriceCents,
            activation.Status,
            activation.StartsAt,
            activation.EndsAt,
            activation.SessionAllowance,
            activation.SessionsUsed);
    }

    private static PrintEntitlementSummary ToPrintEntitlementSummary(PrintEntitlement entitlement)
    {
        return new PrintEntitlementSummary(
            entitlement.Id,
            entitlement.ClientAccountId,
            entitlement.Name);
    }

    private static ReportSummary BuildReportSummary(
        IReadOnlyCollection<ClientAccount> clients,
        IReadOnlyCollection<ClientSubscription> subscriptions,
        IReadOnlyCollection<SubscriptionPlan> subscriptionPlans,
        IReadOnlyCollection<BoothSummary> booths,
        IReadOnlyCollection<BoothOffer> offers,
        IReadOnlyCollection<Location> locations,
        IReadOnlyCollection<Transaction> transactions,
        DateTimeOffset now)
    {
        var todayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var completedTransactions = transactions
            .Where(item => item.Status == StatusValues.Transaction.Completed)
            .ToArray();
        var todayCompletedTransactions = completedTransactions
            .Where(item => (item.CompletedAt ?? item.CreatedAt) >= todayStart)
            .ToArray();
        var subscriptionPlansById = subscriptionPlans.ToDictionary(item => item.Id);
        var latestSubscriptionsByClient = subscriptions
            .GroupBy(item => item.ClientAccountId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.StartsOn).First());
        var activeBoothCountsByClient = booths
            .Where(item => item.Status == StatusValues.Booth.Active)
            .GroupBy(item => item.ClientAccountId)
            .ToDictionary(group => group.Key, group => group.Count());
        var manualMrrCents = latestSubscriptionsByClient.Values.Sum(subscription =>
            (subscription.Status is StatusValues.Subscription.Active or StatusValues.Subscription.Trial) &&
            subscriptionPlansById.TryGetValue(subscription.SubscriptionPlanId, out var plan)
                ? plan.PricePerBoothCents * subscription.ActiveBoothAllowance
                : 0);
        var clientsOverAllowance = latestSubscriptionsByClient.Count(item =>
            activeBoothCountsByClient.TryGetValue(item.Key, out var activeBoothCount) &&
            activeBoothCount > item.Value.ActiveBoothAllowance);

        return new ReportSummary(
            new PlatformReportSummary(
                clients.Count(item => item.Status == StatusValues.ClientAccount.Active),
                booths.Count(item => item.Status == StatusValues.Booth.Active),
                booths.Count(item => item.CurrentState == StatusValues.Booth.Offline),
                subscriptions.Count(item => item.Status == StatusValues.Subscription.Trial),
                subscriptions.Count(item => item.Status == StatusValues.Subscription.Active),
                subscriptions.Count(item => item.Status == StatusValues.Subscription.Suspended),
                subscriptions.Count(item => item.Status == StatusValues.Subscription.Cancelled),
                manualMrrCents,
                clientsOverAllowance),
            new SalesReportSummary(
                todayCompletedTransactions.Sum(item => item.AmountCents),
                todayCompletedTransactions.Count(item => item.TransactionType == StatusValues.TransactionType.SessionPurchase),
                todayCompletedTransactions.Where(item => item.PaymentMethod == StatusValues.PaymentMethod.Cash).Sum(item => item.AmountCents),
                transactions.Count(item => item.Status == StatusValues.Transaction.PendingCash),
                transactions.Count(item => item.Status is StatusValues.Transaction.Expired or StatusValues.Transaction.SessionFailed)),
            BuildBoothSalesSummaries(booths, completedTransactions).ToArray(),
            BuildLocationSalesSummaries(locations, booths, completedTransactions).ToArray(),
            BuildOfferSalesSummaries(offers, completedTransactions).ToArray());
    }

    private static IEnumerable<BoothSalesSummary> BuildBoothSalesSummaries(
        IReadOnlyCollection<BoothSummary> booths,
        IReadOnlyCollection<Transaction> completedTransactions)
    {
        foreach (var booth in booths.OrderBy(item => item.Name))
        {
            var transactions = completedTransactions.Where(item => item.BoothId == booth.Id).ToArray();
            yield return new BoothSalesSummary(
                booth.Id,
                booth.Name,
                transactions.Count(item => item.TransactionType == StatusValues.TransactionType.SessionPurchase),
                transactions.Sum(item => item.AmountCents));
        }
    }

    private static IEnumerable<LocationSalesSummary> BuildLocationSalesSummaries(
        IReadOnlyCollection<Location> locations,
        IReadOnlyCollection<BoothSummary> booths,
        IReadOnlyCollection<Transaction> completedTransactions)
    {
        var boothLocations = booths.ToDictionary(item => item.Id, item => item.LocationId);
        foreach (var location in locations.OrderBy(item => item.Name))
        {
            var transactions = completedTransactions
                .Where(item => boothLocations.TryGetValue(item.BoothId, out var locationId) && locationId == location.Id)
                .ToArray();
            yield return new LocationSalesSummary(
                location.Id,
                location.Name,
                transactions.Count(item => item.TransactionType == StatusValues.TransactionType.SessionPurchase),
                transactions.Sum(item => item.AmountCents));
        }
    }

    private static IEnumerable<OfferSalesSummary> BuildOfferSalesSummaries(
        IReadOnlyCollection<BoothOffer> offers,
        IReadOnlyCollection<Transaction> completedTransactions)
    {
        foreach (var offer in offers.OrderBy(item => item.Name))
        {
            var transactions = completedTransactions.Where(item => item.BoothOfferId == offer.Id).ToArray();
            yield return new OfferSalesSummary(
                offer.Id,
                offer.Name,
                offer.OfferType,
                transactions.Count(item => item.TransactionType == StatusValues.TransactionType.SessionPurchase),
                transactions.Sum(item => item.AmountCents));
        }
    }

    private static async Task<ValidationProblem?> ValidateSubscriptionAllowsNewBoothAsync(
        PhotoBizDbContext dbContext,
        Guid clientAccountId,
        CancellationToken cancellationToken)
    {
        var subscriptionProblem = await ValidateLatestSubscriptionAllowsNewActivationAsync(dbContext, clientAccountId, cancellationToken);

        if (subscriptionProblem is not null)
        {
            return subscriptionProblem;
        }

        var subscription = await GetLatestSubscriptionAsync(dbContext, clientAccountId, cancellationToken);
        var activeBoothCount = await CountActiveBoothsAsync(dbContext, clientAccountId, cancellationToken);

        if (subscription is not null && activeBoothCount >= subscription.ActiveBoothAllowance)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["subscription"] = ["Client active booth allowance has been reached."]
            });
        }

        return null;
    }

    private static async Task<ValidationProblem?> ValidateSubscriptionAllowsExistingBoothAsync(
        PhotoBizDbContext dbContext,
        Guid clientAccountId,
        CancellationToken cancellationToken)
    {
        var subscriptionProblem = await ValidateLatestSubscriptionAllowsNewActivationAsync(dbContext, clientAccountId, cancellationToken);

        if (subscriptionProblem is not null)
        {
            return subscriptionProblem;
        }

        var subscription = await GetLatestSubscriptionAsync(dbContext, clientAccountId, cancellationToken);
        var activeBoothCount = await CountActiveBoothsAsync(dbContext, clientAccountId, cancellationToken);

        if (subscription is not null && activeBoothCount > subscription.ActiveBoothAllowance)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["subscription"] = ["Client active booth allowance has been exceeded."]
            });
        }

        return null;
    }

    private static async Task<ValidationProblem?> ValidateLatestSubscriptionAllowsNewActivationAsync(
        PhotoBizDbContext dbContext,
        Guid clientAccountId,
        CancellationToken cancellationToken)
    {
        var subscription = await GetLatestSubscriptionAsync(dbContext, clientAccountId, cancellationToken);

        if (subscription is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["subscription"] = ["Client must have a manual subscription before booth activation."]
            });
        }

        if (subscription.Status is not StatusValues.Subscription.Trial and not StatusValues.Subscription.Active)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["subscription"] = ["Client subscription must be TRIAL or ACTIVE for new booth activation."]
            });
        }

        return null;
    }

    private static Task<ClientSubscription?> GetLatestSubscriptionAsync(
        PhotoBizDbContext dbContext,
        Guid clientAccountId,
        CancellationToken cancellationToken)
    {
        return dbContext.ClientSubscriptions
            .AsNoTracking()
            .Where(item => item.ClientAccountId == clientAccountId)
            .OrderByDescending(item => item.StartsOn)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static Task<int> CountActiveBoothsAsync(
        PhotoBizDbContext dbContext,
        Guid clientAccountId,
        CancellationToken cancellationToken)
    {
        return dbContext.Booths
            .AsNoTracking()
            .CountAsync(item => item.ClientAccountId == clientAccountId && item.Status == StatusValues.Booth.Active, cancellationToken);
    }

    private static BoothConfigResponse ToUnavailableConfig(
        Booth booth,
        ClientAccount client,
        Location location,
        BoothAppearanceConfig? appearance,
        string boothState,
        string message,
        BoothActiveTransactionResponse? activeTransaction,
        BoothRecentTransactionResponse? recentTransaction)
    {
        return new BoothConfigResponse(
            new BoothClientResponse(client.DisplayNameOrName(), null),
            ToThemeResponse(appearance),
            new BoothSessionResponse(
                appearance?.SessionLabel ?? DefaultSessionLabel,
                "Booth unavailable",
                message,
                appearance?.CompletionThankYouMessage ?? DefaultCompletionThankYouMessage),
            ToBoothStateResponse(booth, location, boothState),
            null,
            [],
            activeTransaction,
            recentTransaction);
    }

    private static BoothStateResponse ToBoothStateResponse(Booth booth, Location location, string state)
    {
        return new BoothStateResponse(booth.Id, state, booth.Name, booth.Code, location.Name);
    }

    private static async Task<BoothActiveTransactionResponse?> GetActiveBoothTransactionAsync(
        PhotoBizDbContext dbContext,
        Guid boothId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions
            .AsNoTracking()
            .Where(item =>
                item.BoothId == boothId &&
                (item.Status == StatusValues.Transaction.Created ||
                 item.Status == StatusValues.Transaction.PendingCash ||
                 item.Status == StatusValues.Transaction.Paid ||
                 item.Status == StatusValues.Transaction.StartingSession ||
                 item.Status == StatusValues.Transaction.InSession ||
                 item.Status == StatusValues.Transaction.SessionFailed))
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return transaction is null ? null : ToBoothActiveTransactionResponse(transaction);
    }

    private static async Task<BoothRecentTransactionResponse?> GetRecentBoothTerminalTransactionAsync(
        PhotoBizDbContext dbContext,
        Guid boothId,
        CancellationToken cancellationToken)
    {
        var recent = await dbContext.Transactions
            .AsNoTracking()
            .Where(item =>
                item.BoothId == boothId &&
                item.TerminalNoticeAcknowledgedAt == null &&
                (item.Status == StatusValues.Transaction.Cancelled ||
                 item.Status == StatusValues.Transaction.Expired ||
                 item.Status == StatusValues.Transaction.PaymentFailed))
            .OrderByDescending(item => item.CancelledAt ?? item.CompletedAt ?? (item.Status == StatusValues.Transaction.Expired ? item.ExpiresAt : item.CreatedAt))
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return recent is null
            ? null
            : new BoothRecentTransactionResponse(
                recent.Id,
                recent.Status,
                recent.TransactionType,
                GetTransactionEventTime(recent),
                recent.FailureReason,
                recent.CancelledByActorType,
                recent.CancelledByUserId,
                recent.CancellationSource,
                recent.CancellationPreviousStatus);
    }

    private static DateTimeOffset GetTransactionEventTime(Transaction transaction)
    {
        return transaction.CancelledAt ??
            transaction.CompletedAt ??
            (transaction.Status == StatusValues.Transaction.Expired ? transaction.ExpiresAt : transaction.CreatedAt);
    }

    private static bool IsValidHexColor(string value)
    {
        return value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
    }

    private static bool IsKioskAcknowledgeableTerminalStatus(string status)
    {
        return status is StatusValues.Transaction.Expired
            or StatusValues.Transaction.Cancelled
            or StatusValues.Transaction.PaymentFailed;
    }

    private static BoothThemeResponse ToThemeResponse(BoothAppearanceConfig? appearance)
    {
        var normalizedTheme = NormalizeThemePreset(appearance?.ThemePreset);
        var scheme = GetThemeScheme(normalizedTheme);
        return new BoothThemeResponse(
            normalizedTheme,
            scheme.PrimaryColor,
            scheme.AccentColor,
            appearance?.BackgroundImageUrl,
            appearance?.BackgroundImageDataUrl,
            scheme.FontMode);
    }

    private static BoothSessionResponse ToSessionResponse(BoothAppearanceConfig? appearance)
    {
        return new BoothSessionResponse(
            appearance?.SessionLabel ?? DefaultSessionLabel,
            appearance?.DefaultWelcomeHeadline ?? DefaultWelcomeHeadline,
            appearance?.DefaultWelcomeSubtitle ?? DefaultWelcomeSubtitle,
            appearance?.CompletionThankYouMessage ?? DefaultCompletionThankYouMessage);
    }

    private static bool IsKnownThemePreset(string? value)
    {
        return value?.ToUpperInvariant() is
            StatusValues.Theme.Vintage or
            StatusValues.Theme.VintageFilm or
            StatusValues.Theme.CleanModern or
            "MODERN_CLEAN" or
            "CLASSIC_LIGHT" or
            StatusValues.Theme.Pop or
            StatusValues.Theme.ModernPop;
    }

    private static string NormalizeThemePreset(string? value)
    {
        return value?.ToUpperInvariant() switch
        {
            StatusValues.Theme.Vintage or StatusValues.Theme.VintageFilm => StatusValues.Theme.Vintage,
            StatusValues.Theme.CleanModern or "MODERN_CLEAN" or "CLASSIC_LIGHT" => StatusValues.Theme.CleanModern,
            StatusValues.Theme.Pop or StatusValues.Theme.ModernPop => StatusValues.Theme.Pop,
            _ => StatusValues.Theme.Vintage
        };
    }

    private static BoothThemeScheme GetThemeScheme(string themePreset)
    {
        return NormalizeThemePreset(themePreset) switch
        {
            StatusValues.Theme.Pop => new BoothThemeScheme("#0bbbe6", "#ff0090", "sans"),
            StatusValues.Theme.CleanModern => new BoothThemeScheme("#111827", "#2563eb", "sans"),
            _ => new BoothThemeScheme("#4f2d1d", "#f5d27e", "serif")
        };
    }

    private static bool IsValidImageDataUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var commaIndex = value.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex < 0)
        {
            return false;
        }

        var header = value[..commaIndex].ToLowerInvariant();
        if (header is not ("data:image/png;base64" or "data:image/jpeg;base64" or "data:image/webp;base64"))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(value[(commaIndex + 1)..]);
            return bytes.Length <= 2 * 1024 * 1024;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static BoothCredentialResponse RotateBoothCredentials(Booth booth, PhotoBizTokenHasher tokenHasher)
    {
        var kioskToken = tokenHasher.GenerateOpaqueToken();
        var agentCredential = tokenHasher.GenerateOpaqueToken();
        booth.KioskTokenHash = tokenHasher.Hash(kioskToken);
        booth.AgentCredentialHash = tokenHasher.Hash(agentCredential);
        PhotoBizBoothAvailability.MarkAgentOffline(booth, DateTimeOffset.UtcNow);
        booth.AgentVersion = null;
        booth.AgentRuntimeKind = null;
        booth.AgentLumaBoothMode = null;
        booth.AgentApiReachable = null;
        booth.AgentChromeLaunched = null;
        booth.AgentTriggerListenerRunning = null;
        booth.AgentLumaBoothReachable = null;

        return new BoothCredentialResponse(booth.Id, booth.Code, kioskToken, agentCredential);
    }

    private static async Task<ValidationProblem?> ValidateSubscriptionPlanRequestAsync(
        PhotoBizDbContext dbContext,
        string name,
        int pricePerBoothCents,
        string currency,
        Guid? existingSubscriptionPlanId,
        CancellationToken cancellationToken)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedCurrency = currency?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Subscription name is required."]
            });
        }

        if (pricePerBoothCents < 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pricePerBoothCents"] = ["Price per booth cannot be negative."]
            });
        }

        if (string.IsNullOrWhiteSpace(normalizedCurrency))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["currency"] = ["Currency is required."]
            });
        }

        var nameExists = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .AnyAsync(
                item => item.Name == normalizedName &&
                    (!existingSubscriptionPlanId.HasValue || item.Id != existingSubscriptionPlanId.Value),
                cancellationToken);

        if (nameExists)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["A subscription with this name already exists."]
            });
        }

        return null;
    }

    private static async Task<ValidationProblem?> ValidatePrintEntitlementRequestAsync(
        PhotoBizDbContext dbContext,
        Guid clientAccountId,
        string name,
        Guid? existingPrintEntitlementId,
        CancellationToken cancellationToken)
    {
        var normalizedName = name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Print entitlement name is required."]
            });
        }

        var nameExists = await dbContext.PrintEntitlements
            .AsNoTracking()
            .AnyAsync(
                item => item.ClientAccountId == clientAccountId &&
                    item.Name == normalizedName &&
                    (!existingPrintEntitlementId.HasValue || item.Id != existingPrintEntitlementId.Value),
                cancellationToken);

        if (nameExists)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["A print entitlement with this name already exists."]
            });
        }

        return null;
    }

    private static async Task<ValidationProblem?> ValidateAssignedBoothAsync(
        PhotoBizDbContext dbContext,
        Guid clientAccountId,
        Guid assignedBoothId,
        Guid? excludedUserId,
        CancellationToken cancellationToken)
    {
        var boothIsInClient = await dbContext.Booths
            .AsNoTracking()
            .AnyAsync(item => item.Id == assignedBoothId && item.ClientAccountId == clientAccountId, cancellationToken);

        if (!boothIsInClient)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedBoothId"] = ["Assigned booth must belong to the user's client account."]
            });
        }

        var assignedToAnotherUser = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.AssignedBoothId == assignedBoothId &&
                    (!excludedUserId.HasValue || item.Id != excludedUserId.Value),
                cancellationToken);

        if (assignedToAnotherUser)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedBoothId"] = ["Assigned booth is already linked to another POS staff user."]
            });
        }

        return null;
    }

    private static void AddDefaultPrintEntitlements(PhotoBizDbContext dbContext, Guid clientAccountId)
    {
        foreach (var name in new[]
        {
            StatusValues.PrintEntitlement.TwoBySixOrOneByFour,
            StatusValues.PrintEntitlement.TwoBySix,
            StatusValues.PrintEntitlement.OneByFour
        })
        {
            dbContext.PrintEntitlements.Add(new PrintEntitlement
            {
                Id = Guid.NewGuid(),
                ClientAccountId = clientAccountId,
                Name = name,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private static PaymentResourceSummary[] BuildPaymentResourceSummaries(
        IReadOnlyCollection<ClientAccount> clients,
        IReadOnlyCollection<ClientPaymentProviderConfig> paymentProviderConfigs,
        IReadOnlyCollection<ClientMayaEcrDevice> mayaEcrDevices)
    {
        return clients
            .SelectMany(client =>
            {
                var mayaQrStatus =
                    paymentProviderConfigs
                        .FirstOrDefault(
                            config =>
                                config.ClientAccountId == client.Id &&
                                config.Provider == StatusValues.PaymentProvider.Maya &&
                                config.IntegrationType == StatusValues.PaymentMethod.MayaCheckoutQr)
                        ?.Status ?? StatusValues.PaymentResource.NotConfigured;
                var mayaEcrStatus =
                    mayaEcrDevices
                        .FirstOrDefault(
                            device =>
                                device.ClientAccountId == client.Id &&
                                device.Provider == StatusValues.PaymentProvider.Maya)
                        ?.Status ?? StatusValues.PaymentResource.NotConfigured;

                return new[]
                {
                    new PaymentResourceSummary(client.Id, StatusValues.PaymentMethod.Cash, true, StatusValues.PaymentResource.Verified),
                    ToPaymentResourceSummary(client.Id, StatusValues.PaymentMethod.MayaCheckoutQr, mayaQrStatus),
                    ToPaymentResourceSummary(client.Id, StatusValues.PaymentMethod.MayaTerminalEcr, mayaEcrStatus)
                };
            })
            .ToArray();
    }

    private static PaymentResourceSummary ToPaymentResourceSummary(Guid clientAccountId, string paymentMethod, string status)
    {
        return new PaymentResourceSummary(
            clientAccountId,
            paymentMethod,
            status is not StatusValues.PaymentResource.NotConfigured and not StatusValues.PaymentResource.Disabled,
            status);
    }

    private static bool IsKnownClientUserRole(string role)
    {
        return role is StatusValues.User.ClientOwner or StatusValues.User.ClientAdmin or StatusValues.User.Cashier;
    }

    private static bool IsPosAssignableRole(string role)
    {
        return role is StatusValues.User.ClientOwner or StatusValues.User.ClientAdmin or StatusValues.User.Cashier;
    }

    private static bool IsKnownClientAccountStatus(string status)
    {
        return status is StatusValues.ClientAccount.Active or StatusValues.ClientAccount.Suspended or StatusValues.ClientAccount.Archived;
    }

    private static bool IsKnownUserStatus(string status)
    {
        return status is StatusValues.User.Active or StatusValues.User.Inactive;
    }

    private static bool IsKnownLocationStatus(string status)
    {
        return status is StatusValues.Booth.Active or StatusValues.Booth.Inactive;
    }

    private static bool IsKnownBoothStatus(string status)
    {
        return status is StatusValues.Booth.Active or StatusValues.Booth.Inactive;
    }

    private static bool IsKnownOfferType(string offerType)
    {
        return offerType is StatusValues.OfferType.PerSession or StatusValues.OfferType.TimeUnlimited or StatusValues.OfferType.SessionCount;
    }

    private static string? NormalizeLumaboothSessionMode(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            StatusValues.LumaboothSessionMode.Print or StatusValues.LumaboothSessionMode.LegacySessionStandard => StatusValues.LumaboothSessionMode.Print,
            StatusValues.LumaboothSessionMode.Gif => StatusValues.LumaboothSessionMode.Gif,
            StatusValues.LumaboothSessionMode.Boomerang => StatusValues.LumaboothSessionMode.Boomerang,
            StatusValues.LumaboothSessionMode.Video => StatusValues.LumaboothSessionMode.Video,
            _ => null
        };
    }

    private static bool IsKnownPaymentMethod(string paymentMethod)
    {
        return paymentMethod is StatusValues.PaymentMethod.Cash or StatusValues.PaymentMethod.MayaCheckoutQr or StatusValues.PaymentMethod.MayaTerminalEcr;
    }

    private static bool IsKnownSubscriptionStatus(string status)
    {
        return status is StatusValues.Subscription.Trial or StatusValues.Subscription.Active or StatusValues.Subscription.Suspended or StatusValues.Subscription.Cancelled;
    }

    private static bool IsTerminalTransactionStatus(string status)
    {
        return status is StatusValues.Transaction.Completed or StatusValues.Transaction.Expired or StatusValues.Transaction.Cancelled;
    }

    private static string GetReturnToWelcomeValidationMessage(ReturnCompletedBoothToWelcomeStatus status)
    {
        return status switch
        {
            ReturnCompletedBoothToWelcomeStatus.ActiveTransaction => "The booth has an active transaction and cannot return to welcome yet.",
            ReturnCompletedBoothToWelcomeStatus.NoCompletedSession => "The booth has no completed session to return from.",
            ReturnCompletedBoothToWelcomeStatus.NotReady => "The completed session prompt is not ready to return to welcome yet.",
            _ => "The booth is not ready to return to welcome."
        };
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
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword, string? ConfirmPassword);
public sealed record AuthSessionResponse(
    Guid UserId,
    string Name,
    string Email,
    string Role,
    Guid? ClientAccountId,
    Guid? AssignedBoothId,
    bool MustChangePassword,
    bool CanApproveCash,
    bool CanReturnBoothToWelcome,
    bool CanCancelTransaction);
public sealed record ClientSummary(Guid Id, string Name, string Status);
public sealed record SubscriptionPlanSummary(Guid Id, string Name, int PricePerBoothCents, string Currency, bool Active);
public sealed record ClientSubscriptionSummary(Guid Id, Guid ClientAccountId, Guid SubscriptionPlanId, string Status, int ActiveBoothAllowance);
public sealed record UserSummary(
    Guid Id,
    Guid? ClientAccountId,
    string Name,
    string Email,
    string Role,
    string Status,
    Guid? AssignedBoothId,
    bool CanApproveCash,
    bool CanReturnBoothToWelcome,
    bool CanCancelTransaction);
public sealed record ClientOnboardingResponse(ClientSummary Client, UserSummary Owner);
public sealed record LocationSummary(Guid Id, Guid ClientAccountId, string Name, string? Address, string Status);
public sealed record BoothSummary(
    Guid Id,
    Guid ClientAccountId,
    Guid LocationId,
    string Name,
    string Code,
    string Status,
    string CurrentState,
    DateTimeOffset? LastHeartbeatAt,
    AgentStatusSummary AgentStatus);
public sealed record AgentStatusSummary(
    string HealthStatus,
    string UpdateStatus,
    string? Version,
    string? RuntimeKind,
    bool KioskRunning,
    string? LumaBoothMode,
    bool? ApiReachable,
    bool? ChromeLaunched,
    bool? TriggerListenerRunning,
    bool? LumaBoothReachable,
    DateTimeOffset? MetadataUpdatedAt);
public sealed record OfferSummary(
    Guid Id,
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
    string LumaboothSessionMode,
    bool Active);
public sealed record PrintEntitlementSummary(Guid Id, Guid ClientAccountId, string Name);
public sealed record OfferActivationSummary(
    Guid Id,
    Guid BoothId,
    Guid BoothOfferId,
    string Status,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    int? SessionAllowance,
    int SessionsUsed);
public sealed record PaymentResourceSummary(Guid ClientAccountId, string PaymentMethod, bool Enabled, string Status);
public sealed record PaymentAssignmentSummary(Guid Id, Guid BoothId, string PaymentMethod, bool RuntimeEnabled, string Status);
public sealed record BoothAppearanceSummary(
    Guid Id,
    Guid BoothId,
    string ThemePreset,
    string PrimaryColor,
    string AccentColor,
    string? BackgroundImageUrl,
    string? BackgroundImageDataUrl,
    string SessionLabel,
    string DefaultWelcomeHeadline,
    string DefaultWelcomeSubtitle,
    string CompletionThankYouMessage);
public sealed record TransactionSummary(
    Guid Id,
    Guid BoothId,
    Guid? BoothOfferActivationId,
    string TransactionNumber,
    string TransactionType,
    string Status,
    string PaymentMethod,
    int AmountCents,
    Guid? ParentTransactionId,
    int ExtraPrintCount,
    bool CanCreateExtraPrintAddOn,
    int? ExtraPrintUnitPriceCents,
    string? OfferName,
    string? OfferType,
    string? IncludedPrintEntitlement,
    int? SessionAllowance,
    int? CoveredSessionSequence,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string? FailureReason,
    string? CancelledByActorType,
    Guid? CancelledByUserId,
    string? CancellationSource,
    string? CancellationPreviousStatus);
public sealed record TransactionOfferSnapshot(
    string? OfferName,
    string? OfferType,
    string? IncludedPrintEntitlement,
    int? SessionAllowance);
public sealed record AdminOverviewResponse(
    AuthSessionResponse Session,
    IReadOnlyCollection<ClientSummary> Clients,
    IReadOnlyCollection<SubscriptionPlanSummary> SubscriptionPlans,
    IReadOnlyCollection<ClientSubscriptionSummary> Subscriptions,
    IReadOnlyCollection<UserSummary> Users,
    IReadOnlyCollection<LocationSummary> Locations,
    IReadOnlyCollection<BoothSummary> Booths,
    IReadOnlyCollection<OfferSummary> Offers,
    IReadOnlyCollection<PrintEntitlementSummary> PrintEntitlements,
    IReadOnlyCollection<OfferActivationSummary> Activations,
    IReadOnlyCollection<PaymentResourceSummary> PaymentResources,
    IReadOnlyCollection<PaymentAssignmentSummary> PaymentAssignments,
    IReadOnlyCollection<BoothAppearanceSummary> AppearanceConfigs,
    IReadOnlyCollection<TransactionSummary> Transactions,
    ReportSummary Reports,
    IReadOnlyCollection<AuditLogSummary> AuditLogs);
public sealed record ReportSummary(
    PlatformReportSummary Platform,
    SalesReportSummary Sales,
    IReadOnlyCollection<BoothSalesSummary> BoothSales,
    IReadOnlyCollection<LocationSalesSummary> LocationSales,
    IReadOnlyCollection<OfferSalesSummary> OfferSales);
public sealed record PlatformReportSummary(
    int ActiveClients,
    int ActiveBooths,
    int OfflineBooths,
    int TrialSubscriptions,
    int ActiveSubscriptions,
    int SuspendedSubscriptions,
    int CancelledSubscriptions,
    int ManualMrrCents,
    int ClientsOverAllowance);
public sealed record SalesReportSummary(
    int TodayGrossSalesCents,
    int TodayCompletedSessions,
    int TodayCashSalesCents,
    int PendingCashCount,
    int FailedOrExpiredCount);
public sealed record BoothSalesSummary(Guid BoothId, string BoothName, int CompletedSessions, int GrossSalesCents);
public sealed record LocationSalesSummary(Guid LocationId, string LocationName, int CompletedSessions, int GrossSalesCents);
public sealed record OfferSalesSummary(Guid OfferId, string OfferName, string OfferType, int CompletedSessions, int GrossSalesCents);
public sealed record AuditLogSummary(
    Guid Id,
    Guid? ClientAccountId,
    Guid? UserId,
    string Action,
    string EntityType,
    Guid? EntityId,
    string Metadata,
    DateTimeOffset CreatedAt);

public sealed record CreateClientRequest(string Name);
public sealed record CreateClientWithOwnerRequest(string? ClientName, string? OwnerName, string? OwnerEmail);
public sealed record UpdateClientRequest(string Name, string Status);
public sealed record TransferClientOwnerRequest(Guid NewOwnerUserId);
public sealed record CreateSubscriptionPlanRequest(string Name, int PricePerBoothCents, string Currency);
public sealed record UpdateSubscriptionPlanRequest(string Name, int PricePerBoothCents, string Currency, bool Active);
public sealed record CreateSubscriptionRequest(Guid ClientAccountId, Guid SubscriptionPlanId, string Status, int ActiveBoothAllowance, string? Notes);
public sealed record UpdateSubscriptionRequest(Guid? SubscriptionPlanId, string Status, int ActiveBoothAllowance, DateOnly? EndsOn, string? Notes);
public sealed record CreateUserRequest(
    Guid? ClientAccountId,
    Guid? AssignedBoothId,
    string Name,
    string Email,
    string Role,
    bool? CanApproveCash,
    bool? CanReturnBoothToWelcome,
    bool? CanCancelTransaction);

public sealed record UpdateUserRequest(
    Guid? AssignedBoothId,
    string Name,
    string Email,
    string Role,
    string Status,
    bool? CanApproveCash,
    bool? CanReturnBoothToWelcome,
    bool? CanCancelTransaction);
public sealed record CreateLocationRequest(Guid ClientAccountId, string Name, string? Address);
public sealed record UpdateLocationRequest(string Name, string? Address, string Status);
public sealed record CreateBoothRequest(Guid ClientAccountId, Guid LocationId, string Name, string Code, Guid? CashierUserId);
public sealed record UpdateBoothRequest(Guid LocationId, string Name, string Code, string Status, Guid? CashierUserId);
public sealed record CreateBoothResponse(BoothSummary Booth, string KioskToken, string AgentCredential);
public sealed record BoothCredentialResponse(Guid BoothId, string BoothCode, string KioskToken, string AgentCredential);
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
public sealed record UpdateOfferRequest(
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
    string LumaboothSessionMode,
    bool Active);
public sealed record CreatePrintEntitlementRequest(Guid ClientAccountId, string Name);
public sealed record UpdatePrintEntitlementRequest(string Name);
public sealed record ActivateOfferRequest(Guid BoothOfferId);
public sealed record UpdateAppearanceRequest(
    string ThemePreset,
    string SessionLabel,
    string DefaultWelcomeHeadline,
    string DefaultWelcomeSubtitle,
    string CompletionThankYouMessage,
    string? BackgroundImageDataUrl,
    string? PrimaryColor = null,
    string? AccentColor = null,
    string? BackgroundImageUrl = null);
public sealed record UpdatePaymentResourceRequest(bool Enabled);
public sealed record AssignPaymentOptionRequest(string PaymentMethod, bool RuntimeEnabled);
public sealed record BoothClientResponse(string DisplayName, string? LogoUrl);
public sealed record BoothThemeResponse(string Preset, string PrimaryColor, string AccentColor, string? BackgroundImageUrl, string? BackgroundImageDataUrl, string FontMode);
public sealed record BoothThemeScheme(string PrimaryColor, string AccentColor, string FontMode);
public sealed record BoothSessionResponse(string Label, string WelcomeHeadline, string WelcomeSubtitle, string CompletionThankYouMessage);
public sealed record BoothStateResponse(Guid Id, string State, string Name, string Code, string LocationName);
public sealed record BoothOfferResponse(
    Guid Id,
    string Name,
    string Type,
    int PriceCents,
    string Currency,
    string IncludedPrintEntitlement,
    bool AllowsExtraPrintAddOn,
    int? ExtraPrintPriceCents,
    string ActivationStatus,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    int? SessionAllowance,
    int SessionsUsed);
public sealed record BoothPaymentOptionResponse(string Method, string Label, bool RuntimeEnabled);
public sealed record BoothRecentTransactionResponse(
    Guid Id,
    string Status,
    string TransactionType,
    DateTimeOffset OccurredAt,
    string? Reason,
    string? CancelledByActorType,
    Guid? CancelledByUserId,
    string? CancellationSource,
    string? CancellationPreviousStatus);
public sealed record BoothActiveTransactionResponse(
    Guid Id,
    string TransactionNumber,
    string TransactionType,
    string Status,
    string PaymentMethod,
    int AmountCents,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
public sealed record BoothConfigResponse(
    BoothClientResponse Client,
    BoothThemeResponse Theme,
    BoothSessionResponse Session,
    BoothStateResponse Booth,
    BoothOfferResponse? ActiveOffer,
    IReadOnlyCollection<BoothPaymentOptionResponse> PaymentOptions,
    BoothActiveTransactionResponse? ActiveTransaction,
    BoothRecentTransactionResponse? RecentTransaction);
public sealed record SelectPaymentMethodRequest(string Method);
public sealed record CancelBoothUiTransactionRequest(string? Trigger);
public sealed record CreateExtraPrintAddOnRequest(int CopyCount);
public sealed record AgentBoothRequest(string BoothCode, string? LumaboothSessionRef = null, string? LumaboothEventType = null);
public sealed record AgentHeartbeatRequest(
    string BoothCode,
    string? AgentVersion = null,
    string? RuntimeKind = null,
    bool? KioskRunning = null,
    string? LumaBoothMode = null,
    bool? ApiReachable = null,
    bool? ChromeLaunched = null,
    bool? TriggerListenerRunning = null,
    bool? LumaBoothReachable = null);
public sealed record AgentPairResponse(Guid BoothId, string BoothName, string BoothCode);
public sealed record AgentBoothUiLaunchResponse(Guid BoothId, string BoothCode, string KioskToken);
public sealed record AgentCommandResponse(
    Guid TransactionId,
    string TransactionNumber,
    string Command,
    string LumaboothSessionMode,
    string OfferType,
    string TransactionType,
    string IncludedPrintEntitlement,
    int ExtraPrintCount);
public sealed record AgentSessionFailedRequest(string BoothCode, string? Reason, string? LumaboothSessionRef = null, string? LumaboothEventType = null);
