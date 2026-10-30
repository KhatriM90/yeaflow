using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YeaFlow.Sap.Contracts.Connector;
using YeaFlow.SapConnector.ServiceLayer;

namespace YeaFlow.SapConnector.Relay;

/// <summary>Outbound-only HTTPS client to the YeaFlow API (D-007). No inbound port is opened at the customer site.</summary>
public sealed class RelayClient
{
    public const string HttpClientName = "yeaflow-api";

    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<ConnectorOptions> _options;

    public RelayClient(IHttpClientFactory httpFactory, IOptionsMonitor<ConnectorOptions> options)
    {
        _httpFactory = httpFactory;
        _options = options;
    }

    public bool IsConfigured => _options.CurrentValue is { ApiBaseUrl: not null, Token.Length: > 0 };

    public async Task<IReadOnlyList<LeasedCommand>> LeaseAsync(int max, int waitSeconds, CancellationToken cancellationToken)
    {
        using var response = await Client().PostAsJsonAsync(RelayRoutes.Lease, new LeaseRequest(max, waitSeconds), cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LeaseResponse>(cancellationToken);
        return body?.Commands ?? [];
    }

    public async Task ReportAsync(Guid commandId, CommandResult result, CancellationToken cancellationToken)
    {
        using var response = await Client().PostAsJsonAsync(RelayRoutes.Result(commandId), result, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task HeartbeatAsync(ConnectorHeartbeat heartbeat, CancellationToken cancellationToken)
    {
        using var response = await Client().PostAsJsonAsync(RelayRoutes.Heartbeat, heartbeat, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private HttpClient Client()
    {
        var options = _options.CurrentValue;
        var client = _httpFactory.CreateClient(HttpClientName);
        client.BaseAddress = options.ApiBaseUrl ?? throw new InvalidOperationException("Connector:ApiBaseUrl is not configured.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(RelayRoutes.AuthScheme, options.Token);
        return client;
    }
}
