using System.Text.Json;
using System.Text.Json.Serialization;

namespace YeaFlow.Sap.Contracts.ServiceLayer;

/// <summary>
/// SAP error as returned by Service Layer: <c>{"error":{"code":-10,"message":"..."}}</c> on v2, where v1 wraps the message as
/// <c>{"lang":"en-us","value":"..."}</c>. Code is kept as a string because SAP returns both numbers and strings.
/// </summary>
public sealed record ServiceLayerError(string Code, string Message)
{
    public static ServiceLayerError? TryParse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(body);
            if (envelope?.Error is not { } error)
            {
                return null;
            }

            var code = error.Code.ValueKind switch
            {
                JsonValueKind.Number => error.Code.GetRawText(),
                JsonValueKind.String => error.Code.GetString() ?? string.Empty,
                _ => string.Empty,
            };

            var message = error.Message.ValueKind switch
            {
                JsonValueKind.String => error.Message.GetString(),
                JsonValueKind.Object when error.Message.TryGetProperty("value", out var value) => value.GetString(),
                _ => null,
            };

            return new ServiceLayerError(code, message ?? "SAP returned an error without a message.");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class Envelope
    {
        [JsonPropertyName("error")]
        public Body? Error { get; set; }
    }

    private sealed class Body
    {
        [JsonPropertyName("code")]
        public JsonElement Code { get; set; }

        [JsonPropertyName("message")]
        public JsonElement Message { get; set; }
    }
}
