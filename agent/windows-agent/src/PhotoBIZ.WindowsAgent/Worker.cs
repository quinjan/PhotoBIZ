using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace PhotoBIZ.WindowsAgent;

public class Worker(
    IHttpClientFactory httpClientFactory,
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var httpClient = httpClientFactory.CreateClient("photobiz-agent");
        httpClient.BaseAddress = new Uri(settings.ApiBaseUrl);

        if (string.IsNullOrWhiteSpace(settings.BoothCode) || string.IsNullOrWhiteSpace(settings.AgentCredential))
        {
            LogAgentStatus(logger, "Agent idle because BoothCode or AgentCredential is missing.", null);
            return;
        }

        httpClient.DefaultRequestHeaders.Add("X-Agent-Credential", settings.AgentCredential);

        await PairAsync(httpClient, settings.BoothCode, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            LogHeartbeat(logger, AgentMetadata.ServiceName, TimeProvider.System.GetUtcNow(), null);
            await HeartbeatAsync(httpClient, settings.BoothCode, stoppingToken);
            await PollForCommandAsync(httpClient, settings, stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
        }
    }

    private static async Task PairAsync(HttpClient httpClient, string boothCode, CancellationToken cancellationToken)
    {
        await httpClient.PostAsJsonAsync("/api/agent/pair", new { boothCode }, cancellationToken);
    }

    private static async Task HeartbeatAsync(HttpClient httpClient, string boothCode, CancellationToken cancellationToken)
    {
        await httpClient.PostAsJsonAsync("/api/agent/heartbeat", new { boothCode }, cancellationToken);
    }

    private async Task PollForCommandAsync(HttpClient httpClient, PhotoBizAgentOptions settings, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"/api/agent/commands/next?boothCode={Uri.EscapeDataString(settings.BoothCode)}", cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return;
        }

        response.EnsureSuccessStatusCode();

        var command = await response.Content.ReadFromJsonAsync<AgentCommandPayload>(cancellationToken);

        if (command is null || command.Command != "START_SESSION")
        {
            return;
        }

        LogAgentStatus(logger, $"Simulating LumaBooth session for {command.TransactionNumber}", null);
        await httpClient.PostAsJsonAsync($"/api/agent/transactions/{command.TransactionId}/session-started", new { boothCode = settings.BoothCode }, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(settings.SimulatedSessionDurationSeconds), cancellationToken);
        await httpClient.PostAsJsonAsync($"/api/agent/transactions/{command.TransactionId}/session-completed", new { boothCode = settings.BoothCode }, cancellationToken);
    }
}

public sealed record AgentCommandPayload(Guid TransactionId, string TransactionNumber, string Command);
