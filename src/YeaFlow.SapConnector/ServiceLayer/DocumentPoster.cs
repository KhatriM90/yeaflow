using System.Text.Json;
using System.Text.Json.Nodes;
using YeaFlow.Sap.Contracts.Documents;

namespace YeaFlow.SapConnector.ServiceLayer;

/// <summary>
/// Executes posting intents against Service Layer for the connector: stamps the transaction ID UDF on the document,
/// posts it, and looks documents up by that UDF to resolve outcome-unknown results.
/// </summary>
public sealed class DocumentPoster
{
    private readonly ServiceLayerClient _client;

    public DocumentPoster(ServiceLayerClient client) => _client = client;

    /// <summary>
    /// Posts a document payload to <paramref name="sapObject"/>. Throws <see cref="ServiceLayerException"/> classified for
    /// the posting state machine. VERIFY per approval case whether Service Layer returns a draft (scope section 4).
    /// </summary>
    public async Task<SapDocumentRef> PostAsync(string sapObject, string transactionId, string payloadJson, CancellationToken cancellationToken)
    {
        EnsureEntitySet(sapObject);
        var body = JsonNode.Parse(payloadJson)?.AsObject()
            ?? throw new ArgumentException("Payload must be a JSON object.", nameof(payloadJson));
        body[SapUdf.TransactionId] = transactionId;

        var created = await _client.PostAsync<JsonObject, JsonObject>(sapObject, body, cancellationToken);
        return ToRef(sapObject, created)
            ?? throw new ServiceLayerException(ServiceLayerFailureKind.OutcomeUnknown, $"Service Layer created {sapObject} but returned no DocEntry.");
    }

    /// <summary>Finds the document carrying our transaction ID, or null when SAP has none.</summary>
    public async Task<SapDocumentRef?> FindByTransactionIdAsync(string sapObject, string transactionId, CancellationToken cancellationToken)
    {
        EnsureEntitySet(sapObject);
        var filter = Uri.EscapeDataString($"{SapUdf.TransactionId} eq '{transactionId.Replace("'", "''", StringComparison.Ordinal)}'");
        var page = await _client.GetAsync<JsonObject>($"{sapObject}?$select=DocEntry,DocNum&$filter={filter}&$top=2", cancellationToken);
        var rows = page["value"]?.AsArray() ?? [];
        return rows.Count switch
        {
            0 => null,
            1 => ToRef(sapObject, rows[0]!.AsObject()),
            _ => throw new InvalidOperationException($"More than one {sapObject} carries transaction {transactionId}; investigate before continuing."),
        };
    }

    private static SapDocumentRef? ToRef(string sapObject, JsonObject document) =>
        document["DocEntry"]?.GetValueKind() == JsonValueKind.Number && document["DocNum"]?.GetValueKind() == JsonValueKind.Number
            ? new SapDocumentRef(sapObject, document["DocEntry"]!.GetValue<int>(), document["DocNum"]!.GetValue<int>())
            : null;

    private static void EnsureEntitySet(string sapObject)
    {
        if (string.IsNullOrWhiteSpace(sapObject) || !sapObject.All(char.IsAsciiLetter))
        {
            throw new ArgumentException($"'{sapObject}' is not a Service Layer entity set.", nameof(sapObject));
        }
    }
}
