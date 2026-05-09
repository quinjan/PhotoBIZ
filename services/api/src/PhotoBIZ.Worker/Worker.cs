namespace PhotoBIZ.Worker;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, DateTimeOffset, Exception?> LogHeartbeat =
        LoggerMessage.Define<string, DateTimeOffset>(
            LogLevel.Information,
            new EventId(1000, nameof(LogHeartbeat)),
            "{ServiceName} heartbeat at {Timestamp}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            LogHeartbeat(logger, WorkerMetadata.ServiceName, TimeProvider.System.GetUtcNow(), null);

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
