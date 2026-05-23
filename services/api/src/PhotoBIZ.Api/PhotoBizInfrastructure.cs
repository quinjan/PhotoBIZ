using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhotoBIZ.Api.Data;

namespace PhotoBIZ.Api;

public sealed class PhotoBizTokenHasher
{
    public string Hash(string value)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(bytes);
    }

    public string GenerateOpaqueToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    }

    public bool Verify(string candidate, string? expectedHash)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        var actual = Hash(candidate);
        var actualBytes = System.Text.Encoding.UTF8.GetBytes(actual);
        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expectedHash);

        return actualBytes.Length == expectedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}

public sealed class PhotoBizAuditService(PhotoBizDbContext dbContext)
{
    public async Task WriteAsync(
        PhotoBizCurrentUser? currentUser,
        string action,
        string entityType,
        Guid? entityId,
        object metadata,
        CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            ClientAccountId = currentUser?.ClientAccountId,
            UserId = currentUser?.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Metadata = JsonSerializer.Serialize(metadata),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record PhotoBizCurrentUser(
    Guid UserId,
    string Role,
    Guid? ClientAccountId,
    Guid? AssignedBoothId)
{
    public bool IsApplicationOwner => Role == StatusValues.User.ApplicationOwner;
    public bool IsClientOwner => Role == StatusValues.User.ClientOwner;
    public bool IsClientAdmin => Role == StatusValues.User.ClientAdmin;
    public bool IsCashier => Role == StatusValues.User.Cashier;
    public bool IsClientScopedAdmin => IsApplicationOwner || IsClientOwner || IsClientAdmin;
}

public static class PhotoBizUserContext
{
    public static PhotoBizCurrentUser GetRequiredCurrentUser(this ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user id is missing.");
        var role = principal.FindFirstValue(ClaimTypes.Role)
            ?? throw new InvalidOperationException("Authenticated role is missing.");

        return new PhotoBizCurrentUser(
            Guid.Parse(userId, CultureInfo.InvariantCulture),
            role,
            ParseGuid(principal.FindFirstValue("client_account_id")),
            ParseGuid(principal.FindFirstValue("assigned_booth_id")));
    }

    public static Guid? ParseGuid(string? value)
    {
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}

public static class PhotoBizAuthenticationGuards
{
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            await RejectPrincipalAsync(context);
            return;
        }

        var dbContext = context.HttpContext.RequestServices.GetRequiredService<PhotoBizDbContext>();
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(item => item.ClientAccount)
            .SingleOrDefaultAsync(item => item.Id == userId, context.HttpContext.RequestAborted);

        if (!CanMaintainAuthenticatedSession(user))
        {
            await RejectPrincipalAsync(context);
        }
    }

    public static bool CanAuthenticate(ApplicationUser user)
    {
        return CanMaintainAuthenticatedSession(user);
    }

    public static bool CanMaintainAuthenticatedSession(ApplicationUser? user)
    {
        if (user is null || user.Status != StatusValues.User.Active)
        {
            return false;
        }

        if (user.Role == StatusValues.User.ApplicationOwner)
        {
            return true;
        }

        return user.ClientAccount?.Status is StatusValues.ClientAccount.Active or StatusValues.ClientAccount.Suspended;
    }

    private static async Task RejectPrincipalAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}

public static class PhotoBizBootstrapper
{
    public static async Task BootstrapDevelopmentAdminAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("PhotoBizBootstrapper");
        var bootstrapSection = app.Configuration.GetSection("BootstrapAdmin");

        if (!bootstrapSection.Exists())
        {
            return;
        }

        var email = bootstrapSection["Email"];
        var password = bootstrapSection["Password"];
        var name = bootstrapSection["Name"] ?? "PhotoBIZ Owner";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var existingAdmin = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Role == StatusValues.User.ApplicationOwner, CancellationToken.None);

        if (existingAdmin)
        {
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            Name = name.Trim(),
            Role = StatusValues.User.ApplicationOwner,
            Status = StatusValues.User.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Seeded bootstrap application owner {Email}", email);
    }
}
