using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using PhotoBIZ.Api.Data;

namespace PhotoBIZ.Api;

public sealed class PhotoBizSecretProtector(IDataProtectionProvider dataProtectionProvider)
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("PhotoBIZ.PaymentProviderSecrets.v1");

    public string Protect(string value)
    {
        return protector.Protect(value);
    }

    public string Unprotect(string value)
    {
        return protector.Unprotect(value);
    }
}

public sealed record PayMongoCredentials(
    string SecretKey,
    string PaymentMode,
    string? BusinessAccountName);

public sealed record PayMongoQrPaymentResult(
    string PaymentIntentId,
    string? PaymentMethodId,
    string? QrImageUrl,
    string RawPayload,
    DateTimeOffset ExpiresAt);

public interface IPayMongoClient
{
    Task VerifyCredentialsAsync(PayMongoCredentials credentials, CancellationToken cancellationToken);

    Task<PayMongoQrPaymentResult> CreateQrPhPaymentAsync(
        PayMongoCredentials credentials,
        Transaction transaction,
        CancellationToken cancellationToken);
}

public sealed class DisabledPayMongoClient : IPayMongoClient
{
    public Task VerifyCredentialsAsync(PayMongoCredentials credentials, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("PayMongo client is not configured.");
    }

    public Task<PayMongoQrPaymentResult> CreateQrPhPaymentAsync(
        PayMongoCredentials credentials,
        Transaction transaction,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("PayMongo client is not configured.");
    }
}

public sealed class PayMongoClient(HttpClient httpClient) : IPayMongoClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task VerifyCredentialsAsync(PayMongoCredentials credentials, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "v1/payment_intents?limit=1");
        ApplyAuth(request, credentials.SecretKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("PayMongo rejected the supplied secret key.");
        }
    }

    public async Task<PayMongoQrPaymentResult> CreateQrPhPaymentAsync(
        PayMongoCredentials credentials,
        Transaction transaction,
        CancellationToken cancellationToken)
    {
        var intentPayload = new
        {
            data = new
            {
                attributes = new
                {
                    amount = transaction.AmountCents,
                    currency = transaction.Currency,
                    description = $"PhotoBIZ {transaction.TransactionNumber}",
                    payment_method_allowed = new[] { "qrph" },
                    metadata = new
                    {
                        photobiz_transaction_id = transaction.Id.ToString("D"),
                        photobiz_transaction_number = transaction.TransactionNumber
                    }
                }
            }
        };

        var intent = await SendPayMongoJsonAsync(
            HttpMethod.Post,
            "v1/payment_intents",
            credentials.SecretKey,
            intentPayload,
            cancellationToken);
        var intentId = GetRequiredString(intent, "data", "id");
        var clientKey = GetRequiredString(intent, "data", "attributes", "client_key");

        var methodPayload = new
        {
            data = new
            {
                attributes = new
                {
                    type = "qrph"
                }
            }
        };
        var method = await SendPayMongoJsonAsync(
            HttpMethod.Post,
            "v1/payment_methods",
            credentials.SecretKey,
            methodPayload,
            cancellationToken);
        var methodId = GetRequiredString(method, "data", "id");

        var attachPayload = new
        {
            data = new
            {
                attributes = new
                {
                    payment_method = methodId,
                    client_key = clientKey
                }
            }
        };
        var attached = await SendPayMongoJsonAsync(
            HttpMethod.Post,
            $"v1/payment_intents/{intentId}/attach",
            credentials.SecretKey,
            attachPayload,
            cancellationToken);
        var qrImageUrl =
            GetString(attached, "data", "attributes", "next_action", "code", "image_url") ??
            GetString(attached, "data", "attributes", "next_action", "qrph", "image_url");
        var expiresAt =
            GetDateTimeOffset(attached, "data", "attributes", "next_action", "code", "expires_at") ??
            GetDateTimeOffset(attached, "data", "attributes", "next_action", "qrph", "expires_at") ??
            DateTimeOffset.UtcNow.AddMinutes(30);

        return new PayMongoQrPaymentResult(
            intentId,
            methodId,
            qrImageUrl,
            attached.ToJsonString(JsonOptions),
            expiresAt);
    }

    private async Task<JsonNode> SendPayMongoJsonAsync(
        HttpMethod method,
        string path,
        string secretKey,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        ApplyAuth(request, secretKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"PayMongo request failed with status {(int)response.StatusCode}.");
        }

        return JsonNode.Parse(responseBody)
            ?? throw new InvalidOperationException("PayMongo returned an empty response.");
    }

    private static void ApplyAuth(HttpRequestMessage request, string secretKey)
    {
        var bytes = Encoding.UTF8.GetBytes($"{secretKey}:");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static string GetRequiredString(JsonNode node, params string[] path)
    {
        return GetString(node, path) ?? throw new InvalidOperationException("PayMongo response was missing a required field.");
    }

    private static string? GetString(JsonNode node, params string[] path)
    {
        var current = FollowPath(node, path);
        return current?.GetValue<string>();
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonNode node, params string[] path)
    {
        var value = GetString(node, path);
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return null;
    }

    private static JsonNode? FollowPath(JsonNode node, params string[] path)
    {
        JsonNode? current = node;
        foreach (var segment in path)
        {
            current = current?[segment];
        }

        return current;
    }
}

public sealed record PayMongoWebhookEvent(
    string EventId,
    string EventType,
    bool LiveMode,
    string? PaymentIntentId,
    string? ProviderObjectId,
    int? AmountCents,
    string? Currency,
    string RawPayload);

public static class PayMongoWebhookParser
{
    public static PayMongoWebhookEvent Parse(string rawPayload)
    {
        using var document = JsonDocument.Parse(rawPayload);
        var root = document.RootElement;
        var data = root.GetProperty("data");
        var eventId = data.GetProperty("id").GetString() ?? string.Empty;
        var attributes = data.GetProperty("attributes");
        var eventType = attributes.GetProperty("type").GetString() ?? string.Empty;
        var liveMode = attributes.TryGetProperty("livemode", out var liveModeElement) && liveModeElement.GetBoolean();
        var providerData = attributes.TryGetProperty("data", out var providerDataElement)
            ? providerDataElement
            : default;
        var providerObjectId = providerData.ValueKind == JsonValueKind.Object &&
            providerData.TryGetProperty("id", out var providerIdElement)
            ? providerIdElement.GetString()
            : null;
        var providerAttributes = providerData.ValueKind == JsonValueKind.Object &&
            providerData.TryGetProperty("attributes", out var providerAttributesElement)
            ? providerAttributesElement
            : default;
        var paymentIntentId = ExtractPaymentIntentId(providerAttributes);
        var amount = ExtractInt(providerAttributes, "amount");
        var currency = ExtractString(providerAttributes, "currency");

        return new PayMongoWebhookEvent(
            eventId,
            eventType,
            liveMode,
            paymentIntentId,
            providerObjectId,
            amount,
            currency,
            rawPayload);
    }

    private static string? ExtractPaymentIntentId(JsonElement attributes)
    {
        if (attributes.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (attributes.TryGetProperty("payment_intent_id", out var idElement))
        {
            return idElement.GetString();
        }

        if (attributes.TryGetProperty("payment_intent", out var intentElement) &&
            intentElement.ValueKind == JsonValueKind.Object &&
            intentElement.TryGetProperty("id", out var nestedIdElement))
        {
            return nestedIdElement.GetString();
        }

        return null;
    }

    private static int? ExtractInt(JsonElement attributes, string propertyName)
    {
        return attributes.ValueKind == JsonValueKind.Object &&
            attributes.TryGetProperty(propertyName, out var element) &&
            element.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static string? ExtractString(JsonElement attributes, string propertyName)
    {
        return attributes.ValueKind == JsonValueKind.Object &&
            attributes.TryGetProperty(propertyName, out var element)
            ? element.GetString()
            : null;
    }
}

public static class PayMongoWebhookSignature
{
    public static bool Verify(string rawPayload, string signatureHeader, string webhookSecret, string paymentMode)
    {
        var parts = signatureHeader
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Where(part => part.Length == 2)
            .ToDictionary(part => part[0], part => part[1], StringComparer.OrdinalIgnoreCase);

        if (!parts.TryGetValue("t", out var timestamp))
        {
            return false;
        }

        var signatureKey = paymentMode == StatusValues.PaymentMode.Live ? "li" : "te";
        if (!parts.TryGetValue(signatureKey, out var expectedSignature) || string.IsNullOrWhiteSpace(expectedSignature))
        {
            return false;
        }

        var signedPayload = $"{timestamp}.{rawPayload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var actual = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();

        var actualBytes = Encoding.UTF8.GetBytes(actual);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature.ToLowerInvariant());
        return actualBytes.Length == expectedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}
