using System.Globalization;
using Serilog;
using YeaFlow.SapConnector;
using YeaFlow.SapConnector.Relay;
using YeaFlow.SapConnector.ServiceLayer;
using YeaFlow.SapConnector.Simulation;

var builder = Host.CreateApplicationBuilder(args);

// Runs as a Windows service next to SAP; console host elsewhere.
builder.Services.AddWindowsService(o => o.ServiceName = "YeaFlow SAP Connector");
builder.Services.AddSerilog((_, cfg) => cfg.ReadFrom.Configuration(builder.Configuration).WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

builder.Services
    .AddOptions<ConnectorOptions>()
    .Bind(builder.Configuration.GetSection(ConnectorOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var simulateSap = builder.Configuration.GetValue<bool>($"{ConnectorOptions.SectionName}:{nameof(ConnectorOptions.SimulateSap)}");
if (simulateSap && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Connector:SimulateSap is for local demos only and cannot be used outside the Development environment.");
}

var serviceLayer = builder.Services.AddHttpClient(ServiceLayerClientFactory.HttpClientName);
if (simulateSap)
{
    // One simulator per process so posted documents can be found again by reconciliation.
    var simulator = new SimulatedServiceLayer();
    serviceLayer.ConfigurePrimaryHttpMessageHandler(() => simulator).SetHandlerLifetime(Timeout.InfiniteTimeSpan);
}
else
{
    serviceLayer.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        // Sessions are carried explicitly per request; a shared cookie jar would mix sessions.
        UseCookies = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    });
}

// Outbound only (D-007); the long-poll waits up to 20 s, so allow comfortably more.
builder.Services.AddHttpClient(RelayClient.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(60));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RelayClient>();
builder.Services.AddSingleton<CommandExecutor>();
builder.Services.AddHostedService<RelayWorker>();
builder.Services.AddSingleton<ServiceLayerClientFactory>();
builder.Services.AddHostedService<ConnectorWorker>();

await builder.Build().RunAsync();
