using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace PhotoBIZ.WindowsAgent;

public interface IPhotoBizAgentApiClient
{
    Task PairAsync(string boothCode, CancellationToken cancellationToken);
    Task HeartbeatAsync(string boothCode, CancellationToken cancellationToken);
    Task<AgentBoothUiLaunchPayload> CreateBoothUiLaunchAsync(string boothCode, CancellationToken cancellationToken);
    Task<AgentCommandPayload?> GetNextCommandAsync(string boothCode, CancellationToken cancellationToken);
    Task MarkSessionStartedAsync(ActiveLumaBoothSession session, string lumaboothEventType, CancellationToken cancellationToken);
    Task MarkSessionCompletedAsync(ActiveLumaBoothSession session, string lumaboothEventType, CancellationToken cancellationToken);
    Task MarkSessionFailedAsync(ActiveLumaBoothSession session, string reason, string? lumaboothEventType, CancellationToken cancellationToken);
    Task MarkPrintCompletedAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken);
    Task MarkPrintFailedAsync(ActiveLumaBoothSession session, string reason, CancellationToken cancellationToken);
}

public sealed class PhotoBizAgentApiClient(
    HttpClient httpClient,
    IOptions<PhotoBizAgentOptions> options) : IPhotoBizAgentApiClient
{
    private readonly PhotoBizAgentOptions settings = options.Value;

    public async Task PairAsync(string boothCode, CancellationToken cancellationToken)
    {
        ConfigureHttpClient();
        var response = await httpClient.PostAsJsonAsync("/api/agent/pair", new { boothCode }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task HeartbeatAsync(string boothCode, CancellationToken cancellationToken)
    {
        ConfigureHttpClient();
        var response = await httpClient.PostAsJsonAsync("/api/agent/heartbeat", new { boothCode }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AgentBoothUiLaunchPayload> CreateBoothUiLaunchAsync(string boothCode, CancellationToken cancellationToken)
    {
        ConfigureHttpClient();
        var response = await httpClient.PostAsJsonAsync("/api/agent/booth-ui-launch", new { boothCode }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentBoothUiLaunchPayload>(cancellationToken)
            ?? throw new InvalidOperationException("PhotoBIZ booth UI launch response was empty.");
    }

    public async Task<AgentCommandPayload?> GetNextCommandAsync(string boothCode, CancellationToken cancellationToken)
    {
        ConfigureHttpClient();
        var response = await httpClient.GetAsync($"/api/agent/commands/next?boothCode={Uri.EscapeDataString(boothCode)}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentCommandPayload>(cancellationToken);
    }

    public Task MarkSessionStartedAsync(ActiveLumaBoothSession session, string lumaboothEventType, CancellationToken cancellationToken)
    {
        return PostSessionEventAsync(
            $"/api/agent/transactions/{session.TransactionId}/session-started",
            session,
            lumaboothEventType,
            reason: null,
            cancellationToken);
    }

    public Task MarkSessionCompletedAsync(ActiveLumaBoothSession session, string lumaboothEventType, CancellationToken cancellationToken)
    {
        return PostSessionEventAsync(
            $"/api/agent/transactions/{session.TransactionId}/session-completed",
            session,
            lumaboothEventType,
            reason: null,
            cancellationToken);
    }

    public Task MarkSessionFailedAsync(ActiveLumaBoothSession session, string reason, string? lumaboothEventType, CancellationToken cancellationToken)
    {
        return PostSessionEventAsync(
            $"/api/agent/transactions/{session.TransactionId}/session-failed",
            session,
            lumaboothEventType,
            reason,
            cancellationToken);
    }

    public Task MarkPrintCompletedAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken)
    {
        return PostSessionEventAsync(
            $"/api/agent/transactions/{session.TransactionId}/print-completed",
            session,
            "print_completed",
            reason: null,
            cancellationToken);
    }

    public Task MarkPrintFailedAsync(ActiveLumaBoothSession session, string reason, CancellationToken cancellationToken)
    {
        return PostSessionEventAsync(
            $"/api/agent/transactions/{session.TransactionId}/print-failed",
            session,
            "print_failed",
            reason,
            cancellationToken);
    }

    private async Task PostSessionEventAsync(
        string path,
        ActiveLumaBoothSession session,
        string? lumaboothEventType,
        string? reason,
        CancellationToken cancellationToken)
    {
        ConfigureHttpClient();
        var response = await httpClient.PostAsJsonAsync(path, new
        {
            boothCode = settings.BoothCode,
            lumaboothSessionRef = session.LumaboothSessionRef,
            lumaboothEventType,
            reason
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private void ConfigureHttpClient()
    {
        httpClient.BaseAddress ??= new Uri(settings.ApiBaseUrl);
        if (!httpClient.DefaultRequestHeaders.Contains("X-Agent-Credential") &&
            !string.IsNullOrWhiteSpace(settings.AgentCredential))
        {
            httpClient.DefaultRequestHeaders.Add("X-Agent-Credential", settings.AgentCredential);
        }
    }
}
