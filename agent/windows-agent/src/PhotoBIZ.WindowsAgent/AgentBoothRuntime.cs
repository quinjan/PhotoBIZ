namespace PhotoBIZ.WindowsAgent;

public interface IAgentBoothRuntime
{
    bool IsRunning { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public sealed class AgentBoothRuntime(
    IPhotoBizAgentApiClient photoBizApi,
    ILumaBoothClient lumaBoothClient,
    IActiveLumaBoothSessionStore activeSessionStore,
    IWindowFocusService windowFocusService,
    IBoothUiLauncher boothUiLauncher,
    ILumaBoothTriggerListener triggerListener,
    IAgentRuntimeOptionsProvider optionsProvider,
    ILogger<AgentBoothRuntime> logger) : IAgentBoothRuntime, IDisposable
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

    private readonly SemaphoreSlim stateLock = new(1, 1);
    private CancellationTokenSource? runtimeCancellation;
    private Task? runtimeLoop;
    private PhotoBizAgentOptions? runningSettings;
    private bool kioskRunning;

    public bool IsRunning => runtimeLoop is not null && !runtimeLoop.IsCompleted;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await stateLock.WaitAsync(cancellationToken);
        PhotoBizAgentOptions? settings = null;
        try
        {
            if (IsRunning)
            {
                return;
            }

            settings = await optionsProvider.LoadAsync(cancellationToken);
            ValidateStartSettings(settings);

            await photoBizApi.PairAsync(settings.BoothCode, cancellationToken);
            kioskRunning = await LaunchBoothUiAsync(settings, cancellationToken);
            if (!kioskRunning)
            {
                throw new InvalidOperationException("Booth UI browser did not launch, so heartbeat was not started.");
            }

            await triggerListener.StartAsync(cancellationToken);

            runningSettings = settings;
            runtimeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            runtimeLoop = Task.Run(() => RunRuntimeLoopAsync(settings, runtimeCancellation.Token), CancellationToken.None);
            LogAgentStatus(logger, "PhotoBIZ booth runtime started.", null);
        }
        catch
        {
            await StopStartedResourcesAsync(settings ?? new PhotoBizAgentOptions(), cancellationToken);
            throw;
        }
        finally
        {
            stateLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await stateLock.WaitAsync(cancellationToken);
        try
        {
            var settings = runningSettings ?? await optionsProvider.LoadAsync(cancellationToken);
            var loop = runtimeLoop;
            var cancellation = runtimeCancellation;

            runtimeLoop = null;
            runtimeCancellation = null;
            runningSettings = null;

            if (cancellation is not null)
            {
                await cancellation.CancelAsync();
            }

            if (loop is not null)
            {
                try
                {
                    await loop.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    LogAgentWarning(logger, $"Booth runtime loop stopped with an error: {ex.Message}", ex);
                }
            }

            cancellation?.Dispose();
            await StopStartedResourcesAsync(settings, cancellationToken);
            LogAgentStatus(logger, "PhotoBIZ booth runtime stopped.", null);
        }
        finally
        {
            stateLock.Release();
        }
    }

    private async Task RunRuntimeLoopAsync(PhotoBizAgentOptions settings, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                LogHeartbeat(logger, AgentMetadata.ServiceName, TimeProvider.System.GetUtcNow(), null);
                await photoBizApi.HeartbeatAsync(BuildHeartbeatPayload(settings), cancellationToken);
                await PollForCommandAsync(settings, cancellationToken);

                await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> LaunchBoothUiAsync(PhotoBizAgentOptions settings, CancellationToken cancellationToken)
    {
        try
        {
            var launch = await photoBizApi.CreateBoothUiLaunchAsync(settings.BoothCode, cancellationToken);
            _ = await boothUiLauncher.LaunchAsync(launch, cancellationToken);
            await windowFocusService.ShowBoothUiAsync(cancellationToken);
            return boothUiLauncher.IsLaunchedProcessRunning;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogAgentWarning(logger, $"Could not launch Booth UI browser: {ex.Message}", ex);
            return false;
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

    private AgentHeartbeatPayload BuildHeartbeatPayload(PhotoBizAgentOptions settings)
    {
        return new AgentHeartbeatPayload(
            settings.BoothCode,
            AgentMetadata.Version,
            AgentMetadata.RuntimeKind,
            kioskRunning,
            settings.LumaBooth.Mode,
            ApiReachable: true,
            ChromeLaunched: kioskRunning,
            TriggerListenerRunning: triggerListener.IsRunning,
            LumaBoothReachable: IsSimulatorMode(settings) ? true : null);
    }

    private async Task StopStartedResourcesAsync(PhotoBizAgentOptions settings, CancellationToken cancellationToken)
    {
        await triggerListener.StopAsync(cancellationToken);

        try
        {
            if (!string.IsNullOrWhiteSpace(settings.BoothCode) && !string.IsNullOrWhiteSpace(settings.AgentCredential))
            {
                await photoBizApi.OfflineAsync(settings.BoothCode, cancellationToken);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogAgentWarning(logger, $"Could not notify PhotoBIZ API that the agent stopped: {ex.Message}", ex);
        }

        await boothUiLauncher.CloseLaunchedAsync(cancellationToken);
        kioskRunning = false;
    }

    private static void ValidateStartSettings(PhotoBizAgentOptions settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BoothCode) || string.IsNullOrWhiteSpace(settings.AgentCredential))
        {
            throw new InvalidOperationException("Booth code and Agent credential are required before starting the booth runtime.");
        }

        if (string.IsNullOrWhiteSpace(settings.Display.BoothUiBaseUrl))
        {
            throw new InvalidOperationException("Booth UI base URL is required before starting the booth runtime.");
        }

        if (!settings.Display.LaunchBoothUiOnStartup)
        {
            throw new InvalidOperationException("Booth UI launch must be enabled before starting the booth runtime.");
        }
    }

    private static bool IsSimulatorMode(PhotoBizAgentOptions settings)
    {
        return !string.Equals(settings.LumaBooth.Mode, LumaBoothIntegrationMode.Api, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        runtimeCancellation?.Dispose();
        stateLock.Dispose();
    }
}
