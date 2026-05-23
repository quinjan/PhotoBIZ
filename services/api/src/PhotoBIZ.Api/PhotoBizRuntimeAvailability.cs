using Microsoft.EntityFrameworkCore;
using PhotoBIZ.Api.Data;

namespace PhotoBIZ.Api;

public sealed record PhotoBizRuntimeGateResult(
    bool Succeeded,
    string Key,
    string Message)
{
    public static PhotoBizRuntimeGateResult Success()
    {
        return new PhotoBizRuntimeGateResult(true, string.Empty, string.Empty);
    }

    public static PhotoBizRuntimeGateResult Failure(string key, string message)
    {
        return new PhotoBizRuntimeGateResult(false, key, message);
    }
}

public static class PhotoBizRuntimeAvailability
{
    public static async Task<PhotoBizRuntimeGateResult> CheckBoothRuntimeAsync(
        PhotoBizDbContext dbContext,
        Booth booth,
        bool requireSubscription,
        bool requireAgent,
        CancellationToken cancellationToken)
    {
        if (booth.Status != StatusValues.Booth.Active)
        {
            return PhotoBizRuntimeGateResult.Failure("booth", "Booth is inactive.");
        }

        var clientStatus = await dbContext.ClientAccounts
            .AsNoTracking()
            .Where(item => item.Id == booth.ClientAccountId)
            .Select(item => item.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (clientStatus != StatusValues.ClientAccount.Active)
        {
            return PhotoBizRuntimeGateResult.Failure("client", "Client account is not active.");
        }

        var locationStatus = await dbContext.Locations
            .AsNoTracking()
            .Where(item => item.Id == booth.LocationId && item.ClientAccountId == booth.ClientAccountId)
            .Select(item => item.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (locationStatus != StatusValues.Booth.Active)
        {
            return PhotoBizRuntimeGateResult.Failure("location", "Booth location is inactive.");
        }

        if (requireSubscription)
        {
            var subscriptionStatus = await dbContext.ClientSubscriptions
                .AsNoTracking()
                .Where(item => item.ClientAccountId == booth.ClientAccountId)
                .OrderByDescending(item => item.StartsOn)
                .Select(item => item.Status)
                .FirstOrDefaultAsync(cancellationToken);

            if (subscriptionStatus is not StatusValues.Subscription.Trial and not StatusValues.Subscription.Active)
            {
                return PhotoBizRuntimeGateResult.Failure("subscription", "Client subscription is inactive.");
            }
        }

        if (requireAgent && PhotoBizBoothAvailability.IsAgentOffline(booth, DateTimeOffset.UtcNow))
        {
            return PhotoBizRuntimeGateResult.Failure("booth", "The booth agent is offline.");
        }

        return PhotoBizRuntimeGateResult.Success();
    }

    public static async Task EnsureBoothRuntimeAsync(
        PhotoBizDbContext dbContext,
        Booth booth,
        bool requireSubscription,
        bool requireAgent,
        CancellationToken cancellationToken)
    {
        var result = await CheckBoothRuntimeAsync(
            dbContext,
            booth,
            requireSubscription,
            requireAgent,
            cancellationToken);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Message);
        }
    }
}
