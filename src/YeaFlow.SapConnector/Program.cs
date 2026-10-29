using System.Globalization;
using Serilog;
using YeaFlow.SapConnector;
using YeaFlow.SapConnector.ServiceLayer;

var builder = Host.CreateApplicationBuilder(args);

// Runs as a Windows service next to SAP; console host elsewhere.
builder.Services.AddWindowsService(o => o.ServiceName = "YeaFlow SAP Connector");
builder.Services.AddSerilog((_, cfg) => cfg.ReadFrom.Configuration(builder.Configuration).WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

builder.Services
    .AddOptions<ConnectorOptions>()
    .Bind(builder.Configuration.GetSection(ConnectorOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient(ServiceLayerClientFactory.HttpClientName)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        // Sessions are carried explicitly per request; a shared cookie jar would mix sessions.
        UseCookies = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    });

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ServiceLayerClientFactory>();
builder.Services.AddHostedService<ConnectorWorker>();

await builder.Build().RunAsync();
