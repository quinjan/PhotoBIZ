using Microsoft.Extensions.Options;

namespace PhotoBIZ.WindowsAgent;

public class Worker(
    IPhotoBizAgentApiClient photoBizApi,
    ILumaBoothClient lumaBoothClient,
    IActiveLumaBoothSessionStore activeSessionStore,
    IWindowFocusService windowFocusService,
    IBoothUiLauncher boothUiLauncher,
    IOptions<PhotoBizAgentOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, DateTimeOffset, Exception?> LogHeartbeat =
        LoggerMessage.Define<string, DateTimeOffset>(
            LogLevel.Information,
            new EventId(1000, nameof(LogHeartbeat)),
            "{ServiceName} heartbeat at {Timestamp}");
    private static readonly Action<ILogger, string, Exception?> LogAgentStatus =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1001, nameof(LogAgentStatus)),
            "{Message}");
    private static readonly Action<ILogger, string, Exception?> LogAgentWarning =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1002, nameof(LogAgentWarning)),
            "{Message}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.BoothCode) || string.IsNullOrWhiteSpace(settings.AgentCredential))
        {
            LogAgentStatus(logger, "Agent idle because BoothCode or AgentCredential is missing.", null);
            return;
        }

        await photoBizApi.PairAsync(settings.BoothCode, stoppingToken);
        await LaunchBoothUiAsync(settings, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            LogHeartbeat(logger, AgentMetadata.ServiceName, TimeProvider.System.GetUtcNow(), null);
            await photoBizApi.HeartbeatAsync(settings.BoothCode, stoppingToken);
            await PollForCommandAsync(settings, stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task LaunchBoothUiAsync(PhotoBizAgentOptions settings, CancellationToken cancellationToken)
    {
        if (!settings.Display.LaunchBoothUiOnStartup)
        {
            return;
        }

        try
        {
            var launch = await photoBizApi.CreateBoothUiLaunchAsync(settings.BoothCode, cancellationToken);
            await boothUiLauncher.LaunchAsync(launch, cancellationToken);
            await windowFocusService.ShowBoothUiAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogAgentWarning(logger, $"Could not launch Booth UI browser: {ex.Message}", ex);
        }
    }

    private async Task PollForCommandAsync(PhotoBizAgentOptions settings, CancellationToken cancellationToken)
    {
        var command = await photoBizApi.GetNextCommandAsync(settings.BoothCode, cancellationToken);

        if (command is null)
        {
            return;
        }

        var activeSession = new ActiveLumaBoothSession(
            command.TransactionId,
            command.TransactionNumber,
            command.Command,
            LumaBoothSessionModes.Normalize(command.LumaboothSessionMode),
            $"PBZ-{command.TransactionId:N}",
            TimeProvider.System.GetUtcNow());

        if (command.Command == "PRINT_COPIES")
        {
            await PrintCopiesAsync(command, activeSession, cancellationToken);
            return;
        }

        if (command.Command != "START_SESSION")
        {
            LogAgentWarning(logger, $"Unsupported PhotoBIZ command {command.Command} for {command.TransactionNumber}", null);
            return;
        }

        try
        {
            await activeSessionStore.SaveAsync(activeSession, cancellationToken);
            LogAgentStatus(logger, $"Starting LumaBooth session for {command.TransactionNumber}", null);
            await lumaBoothClient.StartSessionAsync(activeSession, cancellationToken);
            await windowFocusService.ShowLumaBoothAsync(cancellationToken);

            if (IsSimulatorMode(settings))
            {
                await photoBizApi.MarkSessionStartedAsync(activeSession, "simulator_session_start", cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(settings.SimulatedSessionDurationSeconds), cancellationToken);
                await photoBizApi.MarkSessionCompletedAsync(activeSession, "simulator_session_end", cancellationToken);
                await activeSessionStore.ClearAsync(cancellationToken);
                await windowFocusService.ShowBoothUiAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogAgentWarning(logger, $"LumaBooth session failed for {command.TransactionNumber}: {ex.Message}", ex);
            await photoBizApi.MarkSessionFailedAsync(activeSession, ex.Message, "agent_start_failed", cancellationToken);
            await activeSessionStore.ClearAsync(cancellationToken);
        }
    }

    private async Task PrintCopiesAsync(
        AgentCommandPayload command,
        ActiveLumaBoothSession activeSession,
        CancellationToken cancellationToken)
    {
        try
        {
            LogAgentStatus(logger, $"Printing {command.ExtraPrintCount} extra copies for {command.TransactionNumber}", null);
            await lumaBoothClient.PrintCopiesAsync(command.ExtraPrintCount, cancellationToken);
            await photoBizApi.MarkPrintCompletedAsync(activeSession, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogAgentWarning(logger, $"Extra print command failed for {command.TransactionNumber}: {ex.Message}", ex);
            await photoBizApi.MarkPrintFailedAsync(activeSession, ex.Message, cancellationToken);
        }
    }

    private static bool IsSimulatorMode(PhotoBizAgentOptions settings)
    {
        return !string.Equals(settings.LumaBooth.Mode, LumaBoothIntegrationMode.Api, StringComparison.OrdinalIgnoreCase);
    }
}
