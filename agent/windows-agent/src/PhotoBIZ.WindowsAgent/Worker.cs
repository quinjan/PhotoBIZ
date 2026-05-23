namespace PhotoBIZ.WindowsAgent;

public class Worker(
    IAgentBoothRuntime runtime,
    ILogger<Worker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> LogAgentWarning =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1002, nameof(LogAgentWarning)),
            "{Message}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await runtime.StartAsync(stoppingToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            LogAgentWarning(logger, $"Could not start PhotoBIZ booth runtime: {ex.Message}", ex);
        }
        finally
        {
            await runtime.StopAsync(CancellationToken.None);
        }
    }
}
