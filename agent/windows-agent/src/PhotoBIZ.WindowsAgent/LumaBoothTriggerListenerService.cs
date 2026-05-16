using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace PhotoBIZ.WindowsAgent;

public sealed class LumaBoothTriggerListenerService(
    LumaBoothTriggerHandler triggerHandler,
    IOptions<PhotoBizAgentOptions> options,
    ILogger<LumaBoothTriggerListenerService> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> LogInvalidUrl =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2100, nameof(LogInvalidUrl)),
            "LumaBooth trigger listener URL is invalid: {TriggerListenerUrl}");
    private static readonly Action<ILogger, string, Exception?> LogListenerStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2101, nameof(LogListenerStarted)),
            "LumaBooth trigger listener started at {TriggerListenerUrl}.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!IsApiMode(settings))
        {
            return;
        }

        if (!Uri.TryCreate(settings.LumaBooth.TriggerListenerUrl, UriKind.Absolute, out var listenerUri))
        {
            LogInvalidUrl(logger, settings.LumaBooth.TriggerListenerUrl, null);
            return;
        }

        var listenerAddress = IPAddress.TryParse(listenerUri.Host, out var parsedAddress)
            ? parsedAddress
            : IPAddress.Loopback;
        var listener = new TcpListener(listenerAddress, listenerUri.Port);
        listener.Start();
        LogListenerStarted(logger, settings.LumaBooth.TriggerListenerUrl, null);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(stoppingToken);
                await HandleClientAsync(client, listenerUri.AbsolutePath, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, string expectedPath, CancellationToken cancellationToken)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken);

        if (requestLine is null)
        {
            await WriteResponseAsync(stream, HttpStatusCode.BadRequest, cancellationToken);
            return;
        }

        var requestParts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (requestParts.Length < 2 || !Uri.TryCreate(requestParts[1], UriKind.RelativeOrAbsolute, out var requestUri))
        {
            await WriteResponseAsync(stream, HttpStatusCode.BadRequest, cancellationToken);
            return;
        }

        var path = requestUri.IsAbsoluteUri ? requestUri.AbsolutePath : requestParts[1].Split('?', 2)[0];
        if (!string.Equals(path, expectedPath, StringComparison.OrdinalIgnoreCase))
        {
            await WriteResponseAsync(stream, HttpStatusCode.NotFound, cancellationToken);
            return;
        }

        var query = requestUri.IsAbsoluteUri
            ? requestUri.Query
            : requestParts[1].Contains('?', StringComparison.Ordinal) ? requestParts[1][requestParts[1].IndexOf('?')..] : string.Empty;
        var values = ParseQuery(query);

        if (!values.TryGetValue("event_type", out var eventType) || string.IsNullOrWhiteSpace(eventType))
        {
            await WriteResponseAsync(stream, HttpStatusCode.BadRequest, cancellationToken);
            return;
        }

        await triggerHandler.HandleAsync(new LumaBoothTriggerEvent(
            eventType,
            values.GetValueOrDefault("param1"),
            values.GetValueOrDefault("param2"),
            values.GetValueOrDefault("param3"),
            values.GetValueOrDefault("param4")), cancellationToken);
        await WriteResponseAsync(stream, HttpStatusCode.OK, cancellationToken);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        return query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                part => Uri.UnescapeDataString(part[0]),
                part => part.Length > 1 ? Uri.UnescapeDataString(part[1].Replace("+", " ", StringComparison.Ordinal)) : string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }

    private static async Task WriteResponseAsync(Stream stream, HttpStatusCode statusCode, CancellationToken cancellationToken)
    {
        var response = $"HTTP/1.1 {(int)statusCode} {statusCode}\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK";
        var bytes = System.Text.Encoding.ASCII.GetBytes(response);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static bool IsApiMode(PhotoBizAgentOptions settings)
    {
        return string.Equals(settings.LumaBooth.Mode, LumaBoothIntegrationMode.Api, StringComparison.OrdinalIgnoreCase);
    }
}
