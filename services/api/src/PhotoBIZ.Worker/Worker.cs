using Microsoft.EntityFrameworkCore;
using PhotoBIZ.Api;
using PhotoBIZ.Api.Data;

namespace PhotoBIZ.Worker;

public class Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, DateTimeOffset, Exception?> LogHeartbeat =
        LoggerMessage.Define<string, DateTimeOffset>(
            LogLevel.Information,
            new EventId(1000, nameof(LogHeartbeat)),
            "{ServiceName} heartbeat at {Timestamp}");
    private static readonly Action<ILogger, int, Exception?> LogExpiredTransactions =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1001, nameof(LogExpiredTransactions)),
            "Expired {ExpiredTransactions} pending cash transactions");
    private static readonly Action<ILogger, int, Exception?> LogOfflineBooths =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1002, nameof(LogOfflineBooths)),
            "Marked {OfflineBooths} stale booths offline");
    private static readonly Action<ILogger, int, Exception?> LogCompletedBoothsReset =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1003, nameof(LogCompletedBoothsReset)),
            "Returned {CompletedBooths} completed booths to welcome");
    private static readonly Action<ILogger, int, Exception?> LogPrintingBoothsReset =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1005, nameof(LogPrintingBoothsReset)),
            "Returned {PrintingBooths} timed-out printing booths to welcome");
    private static readonly Action<ILogger, int, Exception?> LogExpiredPlanActivations =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1004, nameof(LogExpiredPlanActivations)),
            "Completed {ExpiredPlanActivations} expired timed package activations");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            LogHeartbeat(logger, WorkerMetadata.ServiceName, TimeProvider.System.GetUtcNow(), null);

            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoBizDbContext>();
            var workflow = scope.ServiceProvider.GetRequiredService<PhotoBizTransactionWorkflow>();
            var now = DateTimeOffset.UtcNow;
            var offlineCutoff = now.Subtract(PhotoBizBoothAvailability.AgentOfflineAfter);
            var expiredTransactions = await dbContext.Transactions
                .Where(transaction =>
                    transaction.Status == StatusValues.Transaction.PendingCash &&
                    transaction.ExpiresAt <= now)
                .ToListAsync(stoppingToken);
            if (expiredTransactions.Count > 0)
            {
                var boothIds = expiredTransactions.Select(transaction => transaction.BoothId).Distinct().ToArray();
                var booths = await dbContext.Booths
                    .Where(booth => boothIds.Contains(booth.Id))
                    .ToDictionaryAsync(booth => booth.Id, stoppingToken);

                foreach (var transaction in expiredTransactions)
                {
                    transaction.Status = StatusValues.Transaction.Expired;

                    if (booths.TryGetValue(transaction.BoothId, out var booth))
                    {
                        booth.CurrentState = StatusValues.Booth.Welcome;
                    }
                }

                await dbContext.SaveChangesAsync(stoppingToken);
                LogExpiredTransactions(logger, expiredTransactions.Count, null);
            }

            var completedBoothsReset = await workflow.ResetCompletedBoothsToWelcomeAsync(stoppingToken);
            if (completedBoothsReset > 0)
            {
                LogCompletedBoothsReset(logger, completedBoothsReset, null);
            }

            var printingBoothsReset = await workflow.ResetTimedOutPrintingBoothsToWelcomeAsync(stoppingToken);
            if (printingBoothsReset > 0)
            {
                LogPrintingBoothsReset(logger, printingBoothsReset, null);
            }

            var expiredPlanActivations = await dbContext.BoothOfferActivations
                .Include(activation => activation.BoothOffer)
                .Where(activation =>
                    activation.Status == StatusValues.OfferActivation.Active &&
                    activation.EndsAt.HasValue &&
                    activation.EndsAt <= now &&
                    activation.BoothOffer != null &&
                    activation.BoothOffer.OfferType == StatusValues.OfferType.TimeUnlimited)
                .ToListAsync(stoppingToken);

            if (expiredPlanActivations.Count > 0)
            {
                foreach (var activation in expiredPlanActivations)
                {
                    activation.Status = StatusValues.OfferActivation.Completed;
                    activation.DeactivatedAt ??= now;
                }

                await dbContext.SaveChangesAsync(stoppingToken);
                LogExpiredPlanActivations(logger, expiredPlanActivations.Count, null);
            }

            var staleIdleBooths = await dbContext.Booths
                .Where(booth =>
                    booth.CurrentState == StatusValues.Booth.Welcome &&
                    (!booth.LastHeartbeatAt.HasValue || booth.LastHeartbeatAt < offlineCutoff))
                .ToListAsync(stoppingToken);

            if (staleIdleBooths.Count > 0)
            {
                foreach (var booth in staleIdleBooths)
                {
                    booth.CurrentState = StatusValues.Booth.Offline;
                }

                await dbContext.SaveChangesAsync(stoppingToken);
                LogOfflineBooths(logger, staleIdleBooths.Count, null);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
