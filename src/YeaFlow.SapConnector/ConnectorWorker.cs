using System.Reflection;
using Microsoft.Extensions.Options;
using YeaFlow.Sap.Contracts.Connector;
using YeaFlow.SapConnector.ServiceLayer;

namespace YeaFlow.SapConnector;

/// <summary>
/// Keeps Service Layer sessions healthy and produces heartbeats for the operator console.
/// Relaying posting commands from the YeaFlow API (outbound-only, authenticated) is the next connector milestone.
/// </summary>
public sealed class ConnectorWorker : BackgroundService
{
    private static readonly string Version = typeof(ConnectorWorker).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    private readonly ServiceLayerClientFactory _clients;
    private readonly IOptionsMonitor<ConnectorOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<ConnectorWorker> _logger;

    public ConnectorWorker(ServiceLayerClientFactory clients, IOptionsMonitor<ConnectorOptions> options, TimeProvider time, ILogger<ConnectorWorker> logger)
    {
        _clients = clients;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var heartbeat = await BuildHeartbeatAsync(stoppingToken);
            _logger.LogInformation(
                "Connector {ConnectorId} v{Version}: {Healthy}/{Total} companies reachable",
                heartbeat.ConnectorId,
                heartbeat.Version,
                heartbeat.Companies.Count(c => c.ServiceLayerReachable),
                heartbeat.Companies.Count);

            await Task.Delay(TimeSpan.FromSeconds(_options.CurrentValue.HeartbeatSeconds), _time, stoppingToken);
        }
    }

    private async Task<ConnectorHeartbeat> BuildHeartbeatAsync(CancellationToken cancellationToken)
    {
        var companies = new List<CompanyHealth>();
        foreach (var companyDb in _clients.CompanyDbs)
        {
            try
            {
                await _clients.For(companyDb).TestConnectionAsync(cancellationToken);
                companies.Add(new CompanyHealth(companyDb, true, 0, null));
            }
            catch (ServiceLayerException ex)
            {
                _logger.LogWarning(ex, "Service Layer check failed for {CompanyDb}", companyDb);
                companies.Add(new CompanyHealth(companyDb, false, 0, ex.Message));
            }
        }

        return new ConnectorHeartbeat(_options.CurrentValue.ConnectorId, Version, _time.GetUtcNow(), companies);
    }
}
