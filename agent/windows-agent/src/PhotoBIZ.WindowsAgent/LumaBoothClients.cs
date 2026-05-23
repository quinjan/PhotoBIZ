namespace PhotoBIZ.WindowsAgent;

public interface ILumaBoothClient
{
    Task StartSessionAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken);
    Task PrintCopiesAsync(int copyCount, CancellationToken cancellationToken);
}

public sealed class SimulatorLumaBoothClient : ILumaBoothClient
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

public sealed class ConfiguredLumaBoothClient(
    IAgentRuntimeOptionsProvider optionsProvider,
    SimulatorLumaBoothClient simulatorClient,
    DslrBoothApiClient apiClient) : ILumaBoothClient
{
    public async Task StartSessionAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken)
    {
        await (await ResolveClientAsync(cancellationToken)).StartSessionAsync(session, cancellationToken);
    }

    public async Task PrintCopiesAsync(int copyCount, CancellationToken cancellationToken)
    {
        await (await ResolveClientAsync(cancellationToken)).PrintCopiesAsync(copyCount, cancellationToken);
    }

    private async Task<ILumaBoothClient> ResolveClientAsync(CancellationToken cancellationToken)
    {
        var settings = await optionsProvider.LoadAsync(cancellationToken);
        return string.Equals(settings.LumaBooth.Mode, LumaBoothIntegrationMode.Api, StringComparison.OrdinalIgnoreCase)
            ? apiClient
            : simulatorClient;
    }
}

public sealed class DslrBoothApiClient(
    HttpClient httpClient,
    IAgentRuntimeOptionsProvider optionsProvider) : ILumaBoothClient
{
    public async Task StartSessionAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken)
    {
        var settings = await optionsProvider.LoadAsync(cancellationToken);
        var apiMode = LumaBoothSessionModes.ToApiMode(session.LumaboothSessionMode);
        var requestPath = $"/api/start?mode={Uri.EscapeDataString(apiMode)}";

        if (!string.IsNullOrWhiteSpace(settings.LumaBooth.ApiPassword))
        {
            requestPath += $"&password={Uri.EscapeDataString(settings.LumaBooth.ApiPassword)}";
        }

        await SendLumaBoothRequestAsync(settings, requestPath, cancellationToken);
    }

    public async Task PrintCopiesAsync(int copyCount, CancellationToken cancellationToken)
    {
        if (copyCount is < 1 or > 5)
        {
            throw new InvalidOperationException("Extra print copy count must be between 1 and 5.");
        }

        var settings = await optionsProvider.LoadAsync(cancellationToken);
        var requestPath = $"/api/print?count={copyCount}";

        if (!string.IsNullOrWhiteSpace(settings.LumaBooth.ApiPassword))
        {
            requestPath += $"&password={Uri.EscapeDataString(settings.LumaBooth.ApiPassword)}";
        }

        await SendLumaBoothRequestAsync(settings, requestPath, cancellationToken);
    }

    private async Task SendLumaBoothRequestAsync(
        PhotoBizAgentOptions settings,
        string requestPath,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.LumaBooth.StartTimeoutSeconds)));

        using var response = await httpClient.GetAsync(new Uri(new Uri(settings.LumaBooth.ApiBaseUrl), requestPath), timeoutCts.Token);
        response.EnsureSuccessStatusCode();
    }
}
