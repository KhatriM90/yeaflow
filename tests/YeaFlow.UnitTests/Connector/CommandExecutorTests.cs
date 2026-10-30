using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using YeaFlow.Sap.Contracts.Connector;
using YeaFlow.SapConnector.Relay;
using YeaFlow.SapConnector.ServiceLayer;

namespace YeaFlow.UnitTests.Connector;

// D-007: the connector's mapping from Service Layer results to relay outcomes. Anything that might have reached SAP is Unknown.
public sealed class CommandExecutorTests : IDisposable
{
    private readonly FakeServiceLayer _sl = new();
    private readonly ServiceLayerClient _client;
    private readonly CommandExecutor _executor;

    public CommandExecutorTests()
    {
        var options = new ServiceLayerCompanyOptions { BaseUrl = new Uri("https://sap.local/b1s/v2/"), CompanyDb = "SBO_ACME", UserName = "u", Password = "p" };
        _client = new ServiceLayerClient(new HttpClient(_sl), options, TimeProvider.System, NullLogger<ServiceLayerClient>.Instance);
        _executor = new CommandExecutor(
            db => db == "SBO_ACME" ? new DocumentPoster(_client) : throw new InvalidOperationException($"Company '{db}' is not configured."),
            NullLogger<CommandExecutor>.Instance);
    }

    public void Dispose()
    {
        _client.Dispose();
        _sl.Dispose();
    }

    private static LeasedCommand Post(string db = "SBO_ACME", string? payload = """{"CardCode":"C001"}""") =>
        new(Guid.NewGuid(), RelayCommandKinds.PostDocument, db, "Orders", "TX-1", payload);

    private static LeasedCommand Find() => new(Guid.NewGuid(), RelayCommandKinds.FindByTransactionId, "SBO_ACME", "Orders", "TX-1", null);

    private void RespondWith(HttpStatusCode status, string body) =>
        _sl.Respond = (_, _) => Task.FromResult(FakeServiceLayer.Json(status, body));

    [Fact]
    public async Task Created_IsPosted()
    {
        RespondWith(HttpStatusCode.Created, """{"DocEntry":42,"DocNum":1042}""");
        var result = await _executor.ExecuteAsync(Post(), CancellationToken.None);
        Assert.Equal(new CommandResult(RelayOutcomes.Posted, 42, 1042), result);
    }

    [Fact]
    public async Task SapValidationError_IsRejected_WithSapCode()
    {
        RespondWith(HttpStatusCode.BadRequest, """{"error":{"code":"-5002","message":"Credit limit exceeded"}}""");
        var result = await _executor.ExecuteAsync(Post(), CancellationToken.None);
        Assert.Equal(RelayOutcomes.Rejected, result.Outcome);
        Assert.Equal("-5002", result.ErrorCode);
    }

    [Fact]
    public async Task ServiceUnavailable_IsTransientNotSent()
    {
        RespondWith(HttpStatusCode.ServiceUnavailable, "{}");
        Assert.Equal(RelayOutcomes.TransientNotSent, (await _executor.ExecuteAsync(Post(), CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task GatewayTimeoutOnPost_IsUnknown()
    {
        RespondWith(HttpStatusCode.GatewayTimeout, "{}");
        Assert.Equal(RelayOutcomes.Unknown, (await _executor.ExecuteAsync(Post(), CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task UnconfiguredCompany_IsTransientNotSent_WithoutCallingSap()
    {
        var result = await _executor.ExecuteAsync(Post(db: "SBO_OTHER"), CancellationToken.None);
        Assert.Equal(RelayOutcomes.TransientNotSent, result.Outcome);
        Assert.Equal("connector.company_not_configured", result.ErrorCode);
        Assert.Empty(_sl.Requests);
    }

    [Fact]
    public async Task MissingPayload_IsRejected()
    {
        var result = await _executor.ExecuteAsync(Post(payload: null), CancellationToken.None);
        Assert.Equal(RelayOutcomes.Rejected, result.Outcome);
        Assert.Empty(_sl.Requests);
    }

    [Fact]
    public async Task UnknownKind_IsTransientNotSent()
    {
        var command = new LeasedCommand(Guid.NewGuid(), "Teleport", "SBO_ACME", "Orders", "TX-1", null);
        Assert.Equal(RelayOutcomes.TransientNotSent, (await _executor.ExecuteAsync(command, CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task Find_ReturnsFoundOrAbsent()
    {
        RespondWith(HttpStatusCode.OK, """{"value":[{"DocEntry":7,"DocNum":107}]}""");
        Assert.Equal(new CommandResult(RelayOutcomes.Found, 7, 107), await _executor.ExecuteAsync(Find(), CancellationToken.None));

        RespondWith(HttpStatusCode.OK, """{"value":[]}""");
        Assert.Equal(RelayOutcomes.Absent, (await _executor.ExecuteAsync(Find(), CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task Find_Failure_ProvesNothing()
    {
        RespondWith(HttpStatusCode.ServiceUnavailable, "{}");
        Assert.Equal(RelayOutcomes.TransientNotSent, (await _executor.ExecuteAsync(Find(), CancellationToken.None)).Outcome);
    }
}
