using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace YeaFlow.SapConnector.Simulation;

/// <summary>
/// In-memory stand-in for SAP Service Layer, for local demos only (<c>Connector:SimulateSap</c>, Development only). It sits
/// under the real <see cref="ServiceLayer.ServiceLayerClient"/>, so login, session handling, error classification and the
/// relay all run as they would against SAP. Nothing here touches a real SAP company.
/// <para>
/// The customer code steers the outcome so every posting state can be tried by hand:
/// <c>REJECT…</c> → SAP validation error; <c>LOST…</c> → document is created but the answer is lost (504), so YeaFlow shows
/// "outcome unknown" and reconciliation later finds it; <c>DOWN…</c> → Service Layer unavailable (503), nothing sent.
/// Anything else posts.
/// </para>
/// </summary>
public sealed partial class SimulatedServiceLayer : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, (int DocEntry, int DocNum)> _byTransactionId = new(StringComparer.Ordinal);
    private int _lastDocEntry;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = request.RequestUri!.AbsolutePath;
        var entity = path[(path.LastIndexOf('/') + 1)..];

        if (entity == "Login" && request.Method == HttpMethod.Post)
        {
            var login = Json(HttpStatusCode.OK, $$"""{"SessionId":"SIM-{{Guid.NewGuid():N}}","Version":"1000250","SessionTimeout":30}""");
            login.Headers.Add("Set-Cookie", "B1SESSION=SIM; path=/b1s/v2; HttpOnly");
            return login;
        }

        if (entity == "Logout")
        {
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (!SafeEntity().IsMatch(entity))
        {
            return Error(HttpStatusCode.NotFound, "-1", $"Simulated SAP: unknown resource '{entity}'.");
        }

        if (request.Method == HttpMethod.Get)
        {
            var txn = TxnFilter().Match(Uri.UnescapeDataString(request.RequestUri.Query));
            var rows = txn.Success && _byTransactionId.TryGetValue(txn.Groups[1].Value, out var doc)
                ? $$"""[{"DocEntry":{{doc.DocEntry}},"DocNum":{{doc.DocNum}}}]"""
                : "[]";
            return Json(HttpStatusCode.OK, $$"""{"value":{{rows}}}""");
        }

        if (request.Method != HttpMethod.Post || request.Content is null)
        {
            return Error(HttpStatusCode.MethodNotAllowed, "-1", "Simulated SAP supports posting and looking up documents only.");
        }

        var body = JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken)) as JsonObject;
        var cardCode = body?["CardCode"]?.GetValue<string>() ?? string.Empty;
        var transactionId = body?["U_YF_TxnId"]?.GetValue<string>() ?? string.Empty;

        if (cardCode.StartsWith("REJECT", StringComparison.OrdinalIgnoreCase))
        {
            return Error(HttpStatusCode.BadRequest, "-10", $"Simulated SAP: business partner {cardCode} is inactive or over its credit limit.");
        }

        if (cardCode.StartsWith("DOWN", StringComparison.OrdinalIgnoreCase))
        {
            return Error(HttpStatusCode.ServiceUnavailable, "-1", "Simulated SAP: Service Layer is unavailable.");
        }

        if (transactionId.Length > 0 && _byTransactionId.TryGetValue(transactionId, out var existing))
        {
            // Same as SAP with a unique UDF index: a second post of one transaction is refused.
            return Error(HttpStatusCode.BadRequest, "-2035", $"Simulated SAP: transaction {transactionId} already exists as document {existing.DocNum}.");
        }

        var docEntry = Interlocked.Increment(ref _lastDocEntry);
        var created = (docEntry, 100000 + docEntry);
        if (transactionId.Length > 0)
        {
            _byTransactionId[transactionId] = created;
        }

        if (cardCode.StartsWith("LOST", StringComparison.OrdinalIgnoreCase))
        {
            // The document exists in "SAP" but the connector never hears back.
            return Error(HttpStatusCode.GatewayTimeout, "-1", "Simulated SAP: gateway timeout after the document was created.");
        }

        return Json(HttpStatusCode.Created, JsonSerializer.Serialize(new { DocEntry = created.docEntry, DocNum = created.Item2, CardCode = cardCode }));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Error(HttpStatusCode status, string code, string message) =>
        Json(status, JsonSerializer.Serialize(new { error = new { code, message } }));

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,63}$")]
    private static partial Regex SafeEntity();

    [GeneratedRegex("U_YF_TxnId eq '([^']+)'")]
    private static partial Regex TxnFilter();
}
