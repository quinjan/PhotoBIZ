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
    public static void MapPhotoBizApi(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/logout", (Delegate)LogoutAsync).RequireAuthorization();
        auth.MapGet("/session", GetSessionAsync).RequireAuthorization();

        var admin = app.MapGroup("/api/admin").RequireAuthorization();
        admin.MapGet("/overview", GetOverviewAsync);
        admin.MapPost("/clients", CreateClientAsync);
        admin.MapPut("/clients/{clientId:guid}", UpdateClientAsync);
        admin.MapPost("/subscription-plans", CreateSubscriptionPlanAsync);
        admin.MapPost("/subscriptions", CreateSubscriptionAsync);
        admin.MapPut("/subscriptions/{subscriptionId:guid}", UpdateSubscriptionAsync);
        admin.MapPost("/users", CreateUserAsync);
        admin.MapPut("/users/{userId:guid}", UpdateUserAsync);
        admin.MapPost("/locations", CreateLocationAsync);
        admin.MapPut("/locations/{locationId:guid}", UpdateLocationAsync);
        admin.MapPost("/booths", CreateBoothAsync);
        admin.MapPut("/booths/{boothId:guid}", UpdateBoothAsync);
        admin.MapPost("/offers", CreateOfferAsync);
        admin.MapPut("/offers/{offerId:guid}", UpdateOfferAsync);
        admin.MapPost("/booths/{boothId:guid}/activate-offer", ActivateOfferAsync);
        admin.MapPut("/booths/{boothId:guid}/appearance", UpdateAppearanceAsync);
        admin.MapPost("/booths/{boothId:guid}/payment-options", AssignPaymentOptionAsync);
        admin.MapDelete("/booths/{boothId:guid}/payment-options/{paymentMethod}", DisablePaymentOptionAsync);

        var boothUi = app.MapGroup("/api/booth-ui");
        boothUi.MapGet("/config", GetBoothConfigAsync);
        boothUi.MapPost("/transactions", CreateBoothTransactionAsync);
        boothUi.MapPost("/transactions/{transactionId:guid}/payment-method", SelectPaymentMethodAsync);

        var cashier = app.MapGroup("/api/cashier").RequireAuthorization();
        cashier.MapPost("/transactions/{transactionId:guid}/approve-cash", ApproveCashAsync);
        cashier.MapPost("/transactions/{transactionId:guid}/cancel", CancelTransactionAsync);
        cashier.MapPost("/transactions/{parentTransactionId:guid}/extra-prints", CreateExtraPrintAddOnAsync);
        cashier.MapPost("/booths/{boothId:guid}/return-to-welcome", ReturnBoothToWelcomeAsync);

        var agent = app.MapGroup("/api/agent");
        agent.MapPost("/pair", PairAgentAsync);
        agent.MapPost("/heartbeat", AgentHeartbeatAsync);
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
        var booths = await ApplyBoothScope(ApplyClientScope(dbContext.Booths.AsNoTracking(), currentUser, item => item.ClientAccountId), currentUser, item => item.Id).ToListAsync(cancellationToken);
        if (currentUser.IsCashier)
        {
            var scopedLocationIds = booths.Select(item => item.LocationId).ToHashSet();
            locations = locations.Where(item => scopedLocationIds.Contains(item.Id)).ToList();
        }
        var scopedBoothIds = booths.Select(item => item.Id).ToArray();
        var offers = await ApplyClientScope(dbContext.BoothOffers.AsNoTracking(), currentUser, item => item.ClientAccountId).ToListAsync(cancellationToken);
        var activations = await ApplyScopedBoothIds(dbContext.BoothOfferActivations.AsNoTracking(), currentUser, scopedBoothIds, item => item.BoothId).ToListAsync(cancellationToken);
        var paymentAssignments = await ApplyScopedBoothIds(dbContext.BoothPaymentOptionAssignments.AsNoTracking(), currentUser, scopedBoothIds, item => item.BoothId).ToListAsync(cancellationToken);
        var subscriptions = await ApplyClientScope(dbContext.ClientSubscriptions.AsNoTracking(), currentUser, item => item.ClientAccountId).ToListAsync(cancellationToken);
        var subscriptionPlans = await dbContext.SubscriptionPlans.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var reportTransactions = await ApplyScopedBoothIds(dbContext.Transactions.AsNoTracking(), currentUser, scopedBoothIds, item => item.BoothId)
            .ToListAsync(cancellationToken);
        var transactions = reportTransactions
            .OrderByDescending(item => item.CreatedAt)
            .Take(25)
            .ToList();
        var auditLogs = await ApplyAuditLogScope(dbContext.AuditLogs.AsNoTracking(), currentUser)
            .OrderByDescending(item => item.CreatedAt)
            .Take(25)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var boothSummaries = booths.Select(booth => new BoothSummary(booth.Id, booth.ClientAccountId, booth.LocationId, booth.Name, booth.Code, booth.Status, PhotoBizBoothAvailability.GetEffectiveState(booth, now), booth.LastHeartbeatAt)).ToArray();

        return TypedResults.Ok(new AdminOverviewResponse(
            new AuthSessionResponse(currentUser.UserId, users.Single(item => item.Id == currentUser.UserId).Name, users.Single(item => item.Id == currentUser.UserId).Email, currentUser.Role, currentUser.ClientAccountId, currentUser.AssignedBoothId),
            clients.Select(client => new ClientSummary(client.Id, client.Name, client.Status)).ToArray(),
            subscriptionPlans.Select(plan => new SubscriptionPlanSummary(plan.Id, plan.Name, plan.PricePerBoothCents, plan.Currency, plan.Active)).ToArray(),
            subscriptions.Select(subscription => new ClientSubscriptionSummary(subscription.Id, subscription.ClientAccountId, subscription.SubscriptionPlanId, subscription.Status, subscription.ActiveBoothAllowance)).ToArray(),
            users.Select(user => new UserSummary(user.Id, user.ClientAccountId, user.Name, user.Email, user.Role, user.Status, user.AssignedBoothId)).ToArray(),
            locations.Select(location => new LocationSummary(location.Id, location.ClientAccountId, location.Name, location.Address, location.Status)).ToArray(),
            boothSummaries,
            offers.Select(ToOfferSummary).ToArray(),
            activations.Select(activation => new OfferActivationSummary(activation.Id, activation.BoothId, activation.BoothOfferId, activation.Status)).ToArray(),
            paymentAssignments.Select(assignment => new PaymentAssignmentSummary(assignment.Id, assignment.BoothId, assignment.PaymentMethod, assignment.RuntimeEnabled, assignment.Status)).ToArray(),
            ToTransactionSummaries(transactions).ToArray(),
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
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "client.created", nameof(ClientAccount), client.Id, new { client.Name }, cancellationToken);

        return TypedResults.Ok(new ClientSummary(client.Id, client.Name, client.Status));
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

        if (currentUser.IsClientAdmin && request.Role == StatusValues.User.ClientOwner)
        {
            return TypedResults.Forbid();
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

        if (request.Role == StatusValues.User.Cashier)
        {
            if (!request.AssignedBoothId.HasValue)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["assignedBoothId"] = ["Cashiers must be assigned to exactly one booth."]
                });
            }

            var boothIsInClient = await dbContext.Booths
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.AssignedBoothId.Value && item.ClientAccountId == clientAccountId.Value, cancellationToken);

            if (!boothIsInClient)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["assignedBoothId"] = ["Assigned booth must belong to the user's client account."]
                });
            }
        }
        else if (request.AssignedBoothId.HasValue)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedBoothId"] = ["Only cashier users can be assigned to a booth."]
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
            CreatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "user.created", nameof(ApplicationUser), user.Id, new { user.Email, user.Role }, cancellationToken);

        return TypedResults.Ok(new UserSummary(user.Id, user.ClientAccountId, user.Name, user.Email, user.Role, user.Status, user.AssignedBoothId));
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

        if (request.Role == StatusValues.User.Cashier)
        {
            if (!request.AssignedBoothId.HasValue)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["assignedBoothId"] = ["Cashiers must be assigned to exactly one booth."]
                });
            }

            var boothIsInClient = await dbContext.Booths
                .AsNoTracking()
                .AnyAsync(item => item.Id == request.AssignedBoothId.Value && item.ClientAccountId == user.ClientAccountId.Value, cancellationToken);

            if (!boothIsInClient)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["assignedBoothId"] = ["Assigned booth must belong to the user's client account."]
                });
            }
        }
        else if (request.AssignedBoothId.HasValue)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["assignedBoothId"] = ["Only cashier users can be assigned to a booth."]
            });
        }

        user.Name = request.Name.Trim();
        user.Email = request.Email.Trim();
        user.Role = request.Role;
        user.Status = request.Status;
        user.AssignedBoothId = request.Role == StatusValues.User.Cashier ? request.AssignedBoothId : null;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(currentUser, "user.updated", nameof(ApplicationUser), user.Id, new { user.Email, user.Role, user.Status, user.AssignedBoothId }, cancellationToken);

        return TypedResults.Ok(new UserSummary(user.Id, user.ClientAccountId, user.Name, user.Email, user.Role, user.Status, user.AssignedBoothId));
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

        var subscriptionValidation = await ValidateSubscriptionAllowsNewBoothAsync(dbContext, clientAccountId, cancellationToken);

        if (subscriptionValidation is not null)
        {
            return subscriptionValidation;
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

        await auditService.WriteAsync(currentUser, "booth.updated", nameof(Booth), booth.Id, new { booth.Name, booth.Code, booth.Status }, cancellationToken);

        return TypedResults.Ok(new BoothSummary(booth.Id, booth.ClientAccountId, booth.LocationId, booth.Name, booth.Code, booth.Status, booth.CurrentState, booth.LastHeartbeatAt));
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
            new BoothOfferResponse(activeActivation.BoothOffer.Id, activeActivation.BoothOffer.Name, activeActivation.BoothOffer.OfferType, activeActivation.BoothOffer.PriceCents, activeActivation.BoothOffer.Currency, activeActivation.BoothOffer.IncludedPrintEntitlement, activeActivation.BoothOffer.AllowsExtraPrintAddOn, activeActivation.BoothOffer.ExtraPrintPriceCents),
            paymentOptions
                .Where(item =>
                    item.Status == StatusValues.PaymentAssignment.Assigned &&
                    item.RuntimeEnabled &&
                    item.PaymentMethod == StatusValues.PaymentMethod.Cash)
                .Select(item => new BoothPaymentOptionResponse(item.PaymentMethod, ToPaymentLabel(item.PaymentMethod), item.RuntimeEnabled))
                .ToArray()));
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

        if (PhotoBizBoothAvailability.GetEffectiveState(booth, DateTimeOffset.UtcNow) != StatusValues.Booth.Welcome)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["booth"] = ["The booth is not ready for a new transaction yet."]
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

    private static async Task<Results<Ok<TransactionSummary>, ForbidHttpResult, ValidationProblem>> CreateExtraPrintAddOnAsync(
        Guid parentTransactionId,
        CreateExtraPrintAddOnRequest request,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizTransactionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();
        var parentTransaction = await LoadScopedTransactionAsync(dbContext, currentUser, parentTransactionId, cancellationToken);

        if (parentTransaction is null)
        {
            return TypedResults.Forbid();
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

    private static async Task<Results<Ok, ForbidHttpResult>> ReturnBoothToWelcomeAsync(
        Guid boothId,
        ClaimsPrincipal principal,
        PhotoBizDbContext dbContext,
        PhotoBizTransactionWorkflow workflow,
        PhotoBizAuditService auditService,
        CancellationToken cancellationToken)
    {
        var currentUser = principal.GetRequiredCurrentUser();
        var booth = await LoadScopedBoothAsync(dbContext, currentUser, boothId, cancellationToken);

        if (booth is null)
        {
            return TypedResults.Forbid();
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
        var body = Expression.Equal(
            boothIdSelector.Body,
            Expression.Constant(currentUser.AssignedBoothId!.Value));

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

    private static AuthSessionResponse ToSessionResponse(ApplicationUser user)
    {
        return new AuthSessionResponse(user.Id, user.Name, user.Email, user.Role, user.ClientAccountId, user.AssignedBoothId);
    }

    private static TransactionSummary ToTransactionSummary(Transaction transaction)
    {
        var extraPrintUnitPriceCents = TryGetExtraPrintUnitPriceCents(transaction);

        return new TransactionSummary(
            transaction.Id,
            transaction.BoothId,
            transaction.TransactionNumber,
            transaction.TransactionType,
            transaction.Status,
            transaction.PaymentMethod,
            transaction.AmountCents,
            transaction.ParentTransactionId,
            transaction.ExtraPrintCount,
            CanCreateExtraPrintAddOn(transaction, isLatestCompletedSession: false, hasActiveBoothTransaction: false, extraPrintUnitPriceCents),
            extraPrintUnitPriceCents,
            transaction.CreatedAt,
            transaction.PaidAt,
            transaction.CompletedAt);
    }

    private static IEnumerable<TransactionSummary> ToTransactionSummaries(IReadOnlyCollection<Transaction> transactions)
    {
        var latestCompletedSessionIdsByBooth = transactions
            .Where(transaction =>
                transaction.TransactionType == StatusValues.TransactionType.SessionPurchase &&
                transaction.Status == StatusValues.Transaction.Completed)
            .GroupBy(transaction => transaction.BoothId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(transaction => transaction.CompletedAt ?? transaction.CreatedAt)
                    .ThenByDescending(transaction => transaction.CreatedAt)
                    .First().Id);
        var boothIdsWithActiveTransactions = transactions
            .Where(transaction => !IsTerminalTransactionStatus(transaction.Status))
            .Select(transaction => transaction.BoothId)
            .ToHashSet();

        foreach (var transaction in transactions)
        {
            var extraPrintUnitPriceCents = TryGetExtraPrintUnitPriceCents(transaction);
            var isLatestCompletedSession =
                latestCompletedSessionIdsByBooth.TryGetValue(transaction.BoothId, out var latestTransactionId) &&
                latestTransactionId == transaction.Id;
            var hasActiveBoothTransaction = boothIdsWithActiveTransactions.Contains(transaction.BoothId);

            yield return new TransactionSummary(
                transaction.Id,
                transaction.BoothId,
                transaction.TransactionNumber,
                transaction.TransactionType,
                transaction.Status,
                transaction.PaymentMethod,
                transaction.AmountCents,
                transaction.ParentTransactionId,
                transaction.ExtraPrintCount,
                CanCreateExtraPrintAddOn(transaction, isLatestCompletedSession, hasActiveBoothTransaction, extraPrintUnitPriceCents),
                extraPrintUnitPriceCents,
                transaction.CreatedAt,
                transaction.PaidAt,
                transaction.CompletedAt);
        }
    }

    private static bool CanCreateExtraPrintAddOn(
        Transaction transaction,
        bool isLatestCompletedSession,
        bool hasActiveBoothTransaction,
        int? extraPrintUnitPriceCents)
    {
        if (!isLatestCompletedSession ||
            hasActiveBoothTransaction ||
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
            (subscription.Status is StatusValues.Subscription.Active or StatusValues.Subscription.Trial or StatusValues.Subscription.PastDue) &&
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
                subscriptions.Count(item => item.Status == StatusValues.Subscription.PastDue),
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

    private static bool IsKnownClientUserRole(string role)
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
        return status is StatusValues.Subscription.Trial or StatusValues.Subscription.Active or StatusValues.Subscription.PastDue or StatusValues.Subscription.Suspended or StatusValues.Subscription.Cancelled;
    }

    private static bool IsTerminalTransactionStatus(string status)
    {
        return status is StatusValues.Transaction.Completed or StatusValues.Transaction.Expired or StatusValues.Transaction.Cancelled;
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
public sealed record UserSummary(Guid Id, Guid? ClientAccountId, string Name, string Email, string Role, string Status, Guid? AssignedBoothId);
public sealed record LocationSummary(Guid Id, Guid ClientAccountId, string Name, string? Address, string Status);
public sealed record BoothSummary(Guid Id, Guid ClientAccountId, Guid LocationId, string Name, string Code, string Status, string CurrentState, DateTimeOffset? LastHeartbeatAt);
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
public sealed record OfferActivationSummary(Guid Id, Guid BoothId, Guid BoothOfferId, string Status);
public sealed record PaymentAssignmentSummary(Guid Id, Guid BoothId, string PaymentMethod, bool RuntimeEnabled, string Status);
public sealed record TransactionSummary(
    Guid Id,
    Guid BoothId,
    string TransactionNumber,
    string TransactionType,
    string Status,
    string PaymentMethod,
    int AmountCents,
    Guid? ParentTransactionId,
    int ExtraPrintCount,
    bool CanCreateExtraPrintAddOn,
    int? ExtraPrintUnitPriceCents,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? CompletedAt);
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
    int PastDueSubscriptions,
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
public sealed record UpdateClientRequest(string Name, string Status);
public sealed record CreateSubscriptionPlanRequest(string Name, int PricePerBoothCents, string Currency);
public sealed record CreateSubscriptionRequest(Guid ClientAccountId, Guid SubscriptionPlanId, string Status, int ActiveBoothAllowance, string? Notes);
public sealed record UpdateSubscriptionRequest(string Status, int ActiveBoothAllowance, DateOnly? EndsOn, string? Notes);
public sealed record CreateUserRequest(Guid? ClientAccountId, Guid? AssignedBoothId, string Name, string Email, string Password, string Role);
public sealed record UpdateUserRequest(Guid? AssignedBoothId, string Name, string Email, string Role, string Status);
public sealed record CreateLocationRequest(Guid ClientAccountId, string Name, string? Address);
public sealed record UpdateLocationRequest(string Name, string? Address, string Status);
public sealed record CreateBoothRequest(Guid ClientAccountId, Guid LocationId, string Name, string Code);
public sealed record UpdateBoothRequest(Guid LocationId, string Name, string Code, string Status);
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
public sealed record BoothOfferResponse(Guid Id, string Name, string Type, int PriceCents, string Currency, string IncludedPrintEntitlement, bool AllowsExtraPrintAddOn, int? ExtraPrintPriceCents);
public sealed record BoothPaymentOptionResponse(string Method, string Label, bool RuntimeEnabled);
public sealed record BoothConfigResponse(
    BoothClientResponse Client,
    BoothThemeResponse Theme,
    BoothSessionResponse Session,
    BoothStateResponse Booth,
    BoothOfferResponse? ActiveOffer,
    IReadOnlyCollection<BoothPaymentOptionResponse> PaymentOptions);
public sealed record SelectPaymentMethodRequest(string Method);
public sealed record CreateExtraPrintAddOnRequest(int CopyCount);
public sealed record AgentBoothRequest(string BoothCode, string? LumaboothSessionRef = null, string? LumaboothEventType = null);
public sealed record AgentPairResponse(Guid BoothId, string BoothName, string BoothCode);
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
