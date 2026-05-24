namespace PhotoBIZ.WindowsAgent;

public interface ILumaBoothConnectionTester
{
    Task<LumaBoothConnectionTestResult> TestAsync(CancellationToken cancellationToken);
}

public sealed record LumaBoothConnectionTestResult(bool Success, string Message);

public sealed class LumaBoothConnectionTester(
    HttpClient httpClient,
    IAgentRuntimeOptionsProvider optionsProvider) : ILumaBoothConnectionTester
{
    public async Task<LumaBoothConnectionTestResult> TestAsync(CancellationToken cancellationToken)
    {
        var settings = await optionsProvider.LoadAsync(cancellationToken);
        if (!string.Equals(settings.LumaBooth.Mode, LumaBoothIntegrationMode.Api, StringComparison.OrdinalIgnoreCase))
        {
            return new LumaBoothConnectionTestResult(true, "Simulator mode is selected; no LumaBooth API call is required.");
        }

        if (!Uri.TryCreate(settings.LumaBooth.ApiBaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return new LumaBoothConnectionTestResult(false, "LumaBooth API URL must be an absolute HTTP or HTTPS URL.");
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.LumaBooth.StartTimeoutSeconds)));
            using var response = await httpClient.GetAsync(baseUri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            return new LumaBoothConnectionTestResult(true, $"LumaBooth API responded with HTTP {(int)response.StatusCode}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LumaBoothConnectionTestResult(false, "LumaBooth API connection timed out.");
        }
        catch (HttpRequestException ex)
        {
            return new LumaBoothConnectionTestResult(false, $"LumaBooth API is not reachable: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return new LumaBoothConnectionTestResult(false, ex.Message);
        }
    }
}
