using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhotoBIZ.WindowsAgent;

namespace PhotoBIZ.WindowsAgent.Tests;

public sealed class AgentBoothRuntimeTests
{
    [Fact]
    public async Task StartBoothLaunchesKioskBeforeHeartbeat()
    {
        var launcher = new RecordingBoothUiLauncher();
        var api = new RecordingPhotoBizAgentApiClient(() => launcher.IsLaunchedProcessRunning);
        var triggerListener = new RecordingTriggerListener();
        var runtime = CreateRuntime(api, launcher, triggerListener);

        await runtime.StartAsync(CancellationToken.None);
        var heartbeat = await api.WaitForHeartbeatAsync();

        Assert.True(launcher.Launched);
        Assert.True(api.HeartbeatSawLaunchedProcess);
        Assert.True(heartbeat.KioskRunning);
        Assert.True(heartbeat.ChromeLaunched);
        Assert.True(runtime.IsRunning);

        await runtime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopBoothStopsLoopsListenerAndOnlyPhotoBizKioskProcess()
    {
        var launcher = new RecordingBoothUiLauncher();
        var api = new RecordingPhotoBizAgentApiClient(() => launcher.IsLaunchedProcessRunning);
        var triggerListener = new RecordingTriggerListener();
        var runtime = CreateRuntime(api, launcher, triggerListener);

        await runtime.StartAsync(CancellationToken.None);
        _ = await api.WaitForHeartbeatAsync();
        await runtime.StopAsync(CancellationToken.None);

        Assert.Equal(1, api.OfflineCalls);
        Assert.Equal(1, launcher.CloseCalls);
        Assert.Equal(1, triggerListener.StartCalls);
        Assert.Equal(1, triggerListener.StopCalls);
        Assert.False(runtime.IsRunning);
    }

    [Fact]
    public async Task StartBoothDoesNotHeartbeatWhenKioskLaunchFails()
    {
        var launcher = new RecordingBoothUiLauncher { ReportsRunningAfterLaunch = false };
        var api = new RecordingPhotoBizAgentApiClient(() => launcher.IsLaunchedProcessRunning);
        var triggerListener = new RecordingTriggerListener();
        var runtime = CreateRuntime(api, launcher, triggerListener);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.StartAsync(CancellationToken.None));

        Assert.Contains("Booth UI browser did not launch", exception.Message, StringComparison.Ordinal);
        Assert.Empty(api.Heartbeats);
        Assert.Equal(1, api.OfflineCalls);
        Assert.Equal(1, launcher.CloseCalls);
        Assert.Equal(0, triggerListener.StartCalls);
        Assert.False(runtime.IsRunning);
    }

    private static AgentBoothRuntime CreateRuntime(
        RecordingPhotoBizAgentApiClient api,
        RecordingBoothUiLauncher launcher,
        RecordingTriggerListener triggerListener)
    {
        return new AgentBoothRuntime(
            api,
            new RecordingLumaBoothClient(),
            new InMemorySessionStore(),
            new RecordingWindowFocusService(),
            launcher,
            triggerListener,
            Options.Create(new PhotoBizAgentOptions
            {
                BoothCode = "SMA-001",
                AgentCredential = "agent-secret",
                PollIntervalSeconds = 30,
                Display = new DisplayOptions
                {
                    BoothUiBaseUrl = "http://localhost:4201",
                    LaunchBoothUiOnStartup = true,
                    KioskMode = false
                },
                LumaBooth = new LumaBoothOptions
                {
                    Mode = LumaBoothIntegrationMode.Api,
                    TriggerListenerUrl = "http://127.0.0.1:5617/lumabooth/events"
                }
            }),
            NullLogger<AgentBoothRuntime>.Instance);
    }

    private sealed class RecordingPhotoBizAgentApiClient(Func<bool> isKioskProcessRunning) : IPhotoBizAgentApiClient
    {
        private readonly TaskCompletionSource<AgentHeartbeatPayload> heartbeatSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<AgentHeartbeatPayload> Heartbeats { get; } = [];
        public bool HeartbeatSawLaunchedProcess { get; private set; }
        public int OfflineCalls { get; private set; }

        public Task PairAsync(string boothCode, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task HeartbeatAsync(AgentHeartbeatPayload heartbeat, CancellationToken cancellationToken)
        {
            Heartbeats.Add(heartbeat);
            HeartbeatSawLaunchedProcess = isKioskProcessRunning();
            heartbeatSource.TrySetResult(heartbeat);
            return Task.CompletedTask;
        }

        public Task OfflineAsync(string boothCode, CancellationToken cancellationToken)
        {
            OfflineCalls++;
            return Task.CompletedTask;
        }

        public Task<AgentBoothUiLaunchPayload> CreateBoothUiLaunchAsync(string boothCode, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AgentBoothUiLaunchPayload(Guid.NewGuid(), boothCode, "kiosk-token"));
        }

        public Task<AgentCommandPayload?> GetNextCommandAsync(string boothCode, CancellationToken cancellationToken)
        {
            return Task.FromResult<AgentCommandPayload?>(null);
        }

        public Task MarkSessionStartedAsync(ActiveLumaBoothSession session, string lumaboothEventType, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task MarkSessionCompletedAsync(ActiveLumaBoothSession session, string lumaboothEventType, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task MarkSessionFailedAsync(ActiveLumaBoothSession session, string reason, string? lumaboothEventType, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task MarkPrintCompletedAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task MarkPrintFailedAsync(ActiveLumaBoothSession session, string reason, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task<AgentHeartbeatPayload> WaitForHeartbeatAsync()
        {
            return await heartbeatSource.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    private sealed class RecordingBoothUiLauncher : IBoothUiLauncher
    {
        public bool ReportsRunningAfterLaunch { get; init; } = true;
        public bool Launched { get; private set; }
        public int CloseCalls { get; private set; }

        public bool IsLaunchedProcessRunning => Launched && ReportsRunningAfterLaunch && CloseCalls == 0;

        public Task<BoothUiLaunchResult> LaunchAsync(AgentBoothUiLaunchPayload launch, CancellationToken cancellationToken)
        {
            Launched = true;
            return Task.FromResult(new BoothUiLaunchResult(1234, $"http://localhost:4201/{launch.KioskToken}"));
        }

        public Task CloseLaunchedAsync(CancellationToken cancellationToken)
        {
            CloseCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTriggerListener : ILumaBoothTriggerListener
    {
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public bool IsRunning { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCalls++;
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCalls++;
            IsRunning = false;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLumaBoothClient : ILumaBoothClient
    {
        public Task StartSessionAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task PrintCopiesAsync(int copyCount, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySessionStore : IActiveLumaBoothSessionStore
    {
        public Task<ActiveLumaBoothSession?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<ActiveLumaBoothSession?>(null);
        }

        public Task SaveAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingWindowFocusService : IWindowFocusService
    {
        public Task ShowLumaBoothAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ShowBoothUiAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
