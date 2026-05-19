using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhotoBIZ.WindowsAgent;

namespace PhotoBIZ.WindowsAgent.Tests;

public sealed class LumaBoothIntegrationTests
{
    [Fact]
    public async Task DslrBoothApiClientStartsSessionWithMappedModeAndPassword()
    {
        var handler = new CapturingHandler();
        var client = new DslrBoothApiClient(
            new HttpClient(handler),
            Options.Create(new PhotoBizAgentOptions
            {
                LumaBooth = new LumaBoothOptions
                {
                    ApiBaseUrl = "http://localhost:1500",
                    ApiPassword = "secret",
                    StartTimeoutSeconds = 5
                }
            }));
        var session = CreateSession("BOOMERANG");

        await client.StartSessionAsync(session, CancellationToken.None);

        Assert.NotNull(handler.RequestUri);
        Assert.Equal("/api/start?mode=boomerang&password=secret", handler.RequestUri.PathAndQuery);
    }

    [Fact]
    public async Task DslrBoothApiClientCanStartMultipleSessionsWithSameHttpClient()
    {
        var handler = new CapturingHandler();
        var client = new DslrBoothApiClient(
            new HttpClient(handler),
            Options.Create(new PhotoBizAgentOptions
            {
                LumaBooth = new LumaBoothOptions
                {
                    ApiBaseUrl = "http://localhost:1500",
                    StartTimeoutSeconds = 5
                }
            }));

        await client.StartSessionAsync(CreateSession("PRINT"), CancellationToken.None);
        await client.StartSessionAsync(CreateSession("GIF"), CancellationToken.None);

        Assert.Collection(
            handler.RequestUris,
            request => Assert.Equal("/api/start?mode=print", request.PathAndQuery),
            request => Assert.Equal("/api/start?mode=gif", request.PathAndQuery));
    }

    [Theory]
    [InlineData(1, "/api/print?count=1")]
    [InlineData(5, "/api/print?count=5")]
    public async Task DslrBoothApiClientPrintsRequestedCopyCount(int copyCount, string expectedPath)
    {
        var handler = new CapturingHandler();
        var client = new DslrBoothApiClient(
            new HttpClient(handler),
            Options.Create(new PhotoBizAgentOptions
            {
                LumaBooth = new LumaBoothOptions
                {
                    ApiBaseUrl = "http://localhost:1500",
                    StartTimeoutSeconds = 5
                }
            }));

        await client.PrintCopiesAsync(copyCount, CancellationToken.None);

        Assert.NotNull(handler.RequestUri);
        Assert.Equal(expectedPath, handler.RequestUri.PathAndQuery);
    }

    [Fact]
    public async Task DslrBoothApiClientAppendsPasswordToPrintRequest()
    {
        var handler = new CapturingHandler();
        var client = new DslrBoothApiClient(
            new HttpClient(handler),
            Options.Create(new PhotoBizAgentOptions
            {
                LumaBooth = new LumaBoothOptions
                {
                    ApiBaseUrl = "http://localhost:1500",
                    ApiPassword = "secret",
                    StartTimeoutSeconds = 5
                }
            }));

        await client.PrintCopiesAsync(2, CancellationToken.None);

        Assert.NotNull(handler.RequestUri);
        Assert.Equal("/api/print?count=2&password=secret", handler.RequestUri.PathAndQuery);
    }

    [Fact]
    public async Task TriggerHandlerReportsSessionStartAndCompletion()
    {
        var session = CreateSession("PRINT");
        var store = new InMemorySessionStore(session);
        var api = new RecordingPhotoBizAgentApiClient();
        var focus = new RecordingWindowFocusService();
        var handler = new LumaBoothTriggerHandler(
            store,
            api,
            focus,
            NullLogger<LumaBoothTriggerHandler>.Instance);

        await handler.HandleAsync(new LumaBoothTriggerEvent("session_start", "print", null, null, null), CancellationToken.None);
        await handler.HandleAsync(new LumaBoothTriggerEvent("printing", "print.jpg", "1", "DNP", null), CancellationToken.None);
        await handler.HandleAsync(new LumaBoothTriggerEvent("session_end", null, null, null, null), CancellationToken.None);

        Assert.Equal(session.TransactionId, api.StartedTransactionId);
        Assert.Equal(session.TransactionId, api.CompletedTransactionId);
        Assert.Null(await store.LoadAsync(CancellationToken.None));
        Assert.True(focus.ShowBoothUiCalled);
    }

    [Fact]
    public async Task AgentApiClientRequestsBoothUiLaunchToken()
    {
        var boothId = Guid.NewGuid();
        var handler = new CapturingHandler(
            $$"""{"boothId":"{{boothId}}","boothCode":"SMA-001","kioskToken":"kiosk-secret"}""");
        var client = new PhotoBizAgentApiClient(
            new HttpClient(handler),
            Options.Create(new PhotoBizAgentOptions
            {
                ApiBaseUrl = "http://localhost:5082",
                AgentCredential = "agent-secret"
            }));

        var launch = await client.CreateBoothUiLaunchAsync("SMA-001", CancellationToken.None);

        Assert.NotNull(handler.RequestUri);
        Assert.Equal("/api/agent/booth-ui-launch", handler.RequestUri.PathAndQuery);
        Assert.Equal("agent-secret", handler.AgentCredential);
        Assert.Equal(boothId, launch.BoothId);
        Assert.Equal("SMA-001", launch.BoothCode);
        Assert.Equal("kiosk-secret", launch.KioskToken);
    }

    [Fact]
    public void ChromeLauncherBuildsBoothUiTokenRoute()
    {
        var url = ChromeBoothUiLauncher.BuildBoothUiUrl("http://localhost:4201/", "abc 123");

        Assert.Equal("http://localhost:4201/abc%20123", url);
    }

    [Fact]
    public void ChromeLauncherBuildsIsolatedKioskArguments()
    {
        var arguments = ChromeBoothUiLauncher.BuildChromeArguments(
            "http://localhost:4201/kiosk-token",
            kioskMode: true,
            @"C:\ProgramData\PhotoBIZ\chrome-kiosk");

        Assert.Contains("--kiosk", arguments);
        Assert.Contains("--new-window", arguments);
        Assert.Contains("--no-first-run", arguments);
        Assert.Contains(@"--user-data-dir=""C:\ProgramData\PhotoBIZ\chrome-kiosk""", arguments);
        Assert.EndsWith(@"""http://localhost:4201/kiosk-token""", arguments);
    }

    [Fact]
    public void ChromeLauncherKeepsNormalBrowserArgumentsWhenKioskModeIsDisabled()
    {
        var arguments = ChromeBoothUiLauncher.BuildChromeArguments(
            "http://localhost:4201/kiosk-token",
            kioskMode: false,
            @"C:\ProgramData\PhotoBIZ\chrome-kiosk");

        Assert.Equal(@"--new-window ""http://localhost:4201/kiosk-token""", arguments);
        Assert.DoesNotContain("--kiosk", arguments);
        Assert.DoesNotContain("--user-data-dir", arguments);
    }

    private static ActiveLumaBoothSession CreateSession(string mode)
    {
        return new ActiveLumaBoothSession(
            Guid.NewGuid(),
            "TXN-TEST",
            "START_SESSION",
            mode,
            "PBZ-session",
            DateTimeOffset.UtcNow);
    }

    private sealed class CapturingHandler(string? responseBody = null) : HttpMessageHandler
    {
        private readonly List<Uri> requestUris = [];

        public Uri? RequestUri { get; private set; }
        public IReadOnlyList<Uri> RequestUris => requestUris;
        public string? AgentCredential { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            requestUris.Add(request.RequestUri!);
            AgentCredential = request.Headers.TryGetValues("X-Agent-Credential", out var values)
                ? values.SingleOrDefault()
                : null;

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (responseBody is not null)
            {
                response.Content = new StringContent(responseBody, Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }

    private sealed class InMemorySessionStore(ActiveLumaBoothSession? session) : IActiveLumaBoothSessionStore
    {
        private ActiveLumaBoothSession? current = session;

        public Task<ActiveLumaBoothSession?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(current);
        }

        public Task SaveAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken)
        {
            current = session;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            current = null;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPhotoBizAgentApiClient : IPhotoBizAgentApiClient
    {
        public Guid? StartedTransactionId { get; private set; }
        public Guid? CompletedTransactionId { get; private set; }

        public Task PairAsync(string boothCode, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task HeartbeatAsync(string boothCode, CancellationToken cancellationToken)
        {
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
            StartedTransactionId = session.TransactionId;
            return Task.CompletedTask;
        }

        public Task MarkSessionCompletedAsync(ActiveLumaBoothSession session, string lumaboothEventType, CancellationToken cancellationToken)
        {
            CompletedTransactionId = session.TransactionId;
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
    }

    private sealed class RecordingWindowFocusService : IWindowFocusService
    {
        public bool ShowBoothUiCalled { get; private set; }

        public Task ShowLumaBoothAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ShowBoothUiAsync(CancellationToken cancellationToken)
        {
            ShowBoothUiCalled = true;
            return Task.CompletedTask;
        }
    }
}
