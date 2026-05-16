using Microsoft.Extensions.Options;

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

public sealed class DslrBoothApiClient(
    HttpClient httpClient,
    IOptions<PhotoBizAgentOptions> options) : ILumaBoothClient
{
    private readonly PhotoBizAgentOptions settings = options.Value;

    public async Task StartSessionAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken)
    {
        var apiMode = LumaBoothSessionModes.ToApiMode(session.LumaboothSessionMode);
        var requestPath = $"/api/start?mode={Uri.EscapeDataString(apiMode)}";

        if (!string.IsNullOrWhiteSpace(settings.LumaBooth.ApiPassword))
        {
            requestPath += $"&password={Uri.EscapeDataString(settings.LumaBooth.ApiPassword)}";
        }

        await SendLumaBoothRequestAsync(requestPath, cancellationToken);
    }

    public async Task PrintCopiesAsync(int copyCount, CancellationToken cancellationToken)
    {
        if (copyCount is < 1 or > 5)
        {
            throw new InvalidOperationException("Extra print copy count must be between 1 and 5.");
        }

        var requestPath = $"/api/print?count={copyCount}";

        if (!string.IsNullOrWhiteSpace(settings.LumaBooth.ApiPassword))
        {
            requestPath += $"&password={Uri.EscapeDataString(settings.LumaBooth.ApiPassword)}";
        }

        await SendLumaBoothRequestAsync(requestPath, cancellationToken);
    }

    private async Task SendLumaBoothRequestAsync(string requestPath, CancellationToken cancellationToken)
    {
        httpClient.BaseAddress ??= new Uri(settings.LumaBooth.ApiBaseUrl);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.LumaBooth.StartTimeoutSeconds)));

        using var response = await httpClient.GetAsync(requestPath, timeoutCts.Token);
        response.EnsureSuccessStatusCode();
    }
}
