using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace YeaFlow.SapConnector.ServiceLayer;

/// <summary>One <see cref="ServiceLayerClient"/> (and therefore one session pool) per configured company database.</summary>
public sealed class ServiceLayerClientFactory : IDisposable
{
    public const string HttpClientName = "service-layer";

    private readonly ConcurrentDictionary<string, ServiceLayerClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<ConnectorOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILoggerFactory _loggers;

    public ServiceLayerClientFactory(IHttpClientFactory httpFactory, IOptionsMonitor<ConnectorOptions> options, TimeProvider time, ILoggerFactory loggers)
    {
        _httpFactory = httpFactory;
        _options = options;
        _time = time;
        _loggers = loggers;
    }

    public IEnumerable<string> CompanyDbs => _options.CurrentValue.Companies.Select(c => c.CompanyDb);

    public ServiceLayerClient For(string companyDb) => _clients.GetOrAdd(companyDb, db =>
    {
        var company = _options.CurrentValue.Companies.Find(c => string.Equals(c.CompanyDb, db, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Company database '{db}' is not configured on this connector.");
        return new ServiceLayerClient(_httpFactory.CreateClient(HttpClientName), company, _time, _loggers.CreateLogger<ServiceLayerClient>());
    });

    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
    }
}
