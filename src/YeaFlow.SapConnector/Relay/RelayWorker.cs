using Microsoft.Extensions.Options;
using YeaFlow.Sap.Contracts.Connector;
using YeaFlow.SapConnector.ServiceLayer;

namespace YeaFlow.SapConnector.Relay;

/// <summary>
/// Long-polls the API for commands, runs them through Service Layer and reports the outcome. If a result cannot be
/// delivered, the API treats the command as outcome unknown and reconciles by transaction ID, so nothing is posted twice.
/// </summary>
public sealed class RelayWorker : BackgroundService
{
    private static readonly TimeSpan[] ReportBackoff = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10)];

    private readonly RelayClient _relay;
    private readonly CommandExecutor _executor;
    private readonly IOptionsMonitor<ConnectorOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<RelayWorker> _logger;

    public RelayWorker(RelayClient relay, CommandExecutor executor, IOptionsMonitor<ConnectorOptions> options, TimeProvider time, ILogger<RelayWorker> logger)
    {
        _relay = relay;
        _executor = executor;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_relay.IsConfigured)
        {
            _logger.LogWarning("Connector:ApiBaseUrl or Connector:Token is not set; the relay is idle");
            return;
        }

        var failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var commands = await _relay.LeaseAsync(_options.CurrentValue.MaxConcurrentCommands, 20, stoppingToken);
                failures = 0;
                await Task.WhenAll(commands.Select(c => RunAsync(c, stoppingToken)));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !stoppingToken.IsCancellationRequested)
            {
                failures++;
                var delay = TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, failures)));
                _logger.LogWarning(ex, "Cannot reach the YeaFlow API; retrying in {Delay}", delay);
                await Task.Delay(delay, _time, stoppingToken);
            }
        }
    }

    internal async Task RunAsync(LeasedCommand command, CancellationToken cancellationToken)
    {
        var result = await _executor.ExecuteAsync(command, cancellationToken);
        _logger.LogInformation("{Kind} {SapObject} {TransactionId}: {Outcome}", command.Kind, command.SapObject, command.TransactionId, result.Outcome);

        foreach (var delay in ReportBackoff.Prepend(TimeSpan.Zero))
        {
            await Task.Delay(delay, _time, cancellationToken);
            try
            {
                await _relay.ReportAsync(command.Id, result, cancellationToken);
                return;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Could not report result for command {CommandId}", command.Id);
            }
        }

        _logger.LogError("Gave up reporting command {CommandId}; the API will reconcile it by transaction ID", command.Id);
    }
}
