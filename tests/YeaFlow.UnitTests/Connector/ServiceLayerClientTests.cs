using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using YeaFlow.SapConnector.ServiceLayer;

namespace YeaFlow.UnitTests.Connector;

public sealed class ServiceLayerClientTests : IDisposable
{
    private readonly FakeServiceLayer _sl = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero));
    private readonly ServiceLayerClient _client;

    public ServiceLayerClientTests()
    {
        var options = new ServiceLayerCompanyOptions
        {
            BaseUrl = new Uri("https://sap.local:50000/b1s/v2/"),
            CompanyDb = "SBODEMOKE",
            UserName = "yf_integration",
            Password = "not-a-real-secret",
            MaxSessions = 2,
        };
        _client = new ServiceLayerClient(new HttpClient(_sl), options, _time, NullLogger<ServiceLayerClient>.Instance);
    }

    public void Dispose()
    {
        _client.Dispose();
        _sl.Dispose();
    }

    [Fact]
    public async Task Get_LogsInOnce_AndReusesPooledSession()
    {
        _sl.Respond = (_, _) => Task.FromResult(FakeServiceLayer.Json(HttpStatusCode.OK, """{"CardCode":"C001"}"""));

        await _client.GetAsync<JsonObject>("BusinessPartners('C001')", CancellationToken.None);
        await _client.GetAsync<JsonObject>("BusinessPartners('C001')", CancellationToken.None);

        Assert.Equal(1, _sl.Logins);
        Assert.All(_sl.Requests, r => Assert.Equal("B1SESSION=S1; ROUTEID=.node1", r.Cookie));
    }

    [Fact]
    public async Task ExpiredSession_ReLogsInOn401_AndRetriesOnce()
    {
        _sl.Respond = (_, cookie) => Task.FromResult(cookie!.Contains("S1", StringComparison.Ordinal)
            ? FakeServiceLayer.Json(HttpStatusCode.Unauthorized, """{"error":{"code":301,"message":"Invalid session."}}""")
            : FakeServiceLayer.Json(HttpStatusCode.OK, """{"DocEntry":1}"""));

        var result = await _client.GetAsync<JsonObject>("Orders(1)", CancellationToken.None);

        Assert.Equal(1, result["DocEntry"]!.GetValue<int>());
        Assert.Equal(2, _sl.Logins);
    }

    [Fact]
    public async Task SessionPastTimeout_IsNotReused()
    {
        await _client.GetAsync<JsonObject>("Items('A')", CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(31));
        await _client.GetAsync<JsonObject>("Items('A')", CancellationToken.None);
        Assert.Equal(2, _sl.Logins);
    }

    [Fact]
    public async Task SapBusinessError_IsRejected_WithCodeAndMessage()
    {
        _sl.Respond = (_, _) => Task.FromResult(FakeServiceLayer.Json(HttpStatusCode.BadRequest,
            """{"error":{"code":-5002,"message":"Customer credit limit exceeded"}}"""));

        var ex = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            _client.PostAsync<object, JsonObject>("Orders", new { CardCode = "C001" }, CancellationToken.None));

        Assert.Equal(ServiceLayerFailureKind.Rejected, ex.Kind);
        Assert.Equal("-5002", ex.SapError!.Code);
        Assert.Equal("Customer credit limit exceeded", ex.SapError.Message);
    }

    [Fact]
    public async Task V1ErrorFormat_IsParsed()
    {
        _sl.Respond = (_, _) => Task.FromResult(FakeServiceLayer.Json(HttpStatusCode.BadRequest,
            """{"error":{"code":"-1029","message":{"lang":"en-us","value":"Field cannot be updated"}}}"""));

        var ex = await Assert.ThrowsAsync<ServiceLayerException>(() => _client.GetAsync<JsonObject>("Orders(1)", CancellationToken.None));
        Assert.Equal("Field cannot be updated", ex.SapError!.Message);
    }

    [Fact]
    public async Task TimeoutOnWrite_IsOutcomeUnknown()
    {
        _sl.Respond = (_, _) => throw new TaskCanceledException("timeout");

        var ex = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            _client.PostAsync<object, JsonObject>("Orders", new { CardCode = "C001" }, CancellationToken.None));

        Assert.Equal(ServiceLayerFailureKind.OutcomeUnknown, ex.Kind);
    }

    [Fact]
    public async Task TimeoutOnRead_IsTransient()
    {
        _sl.Respond = (_, _) => throw new TaskCanceledException("timeout");
        var ex = await Assert.ThrowsAsync<ServiceLayerException>(() => _client.GetAsync<JsonObject>("Orders(1)", CancellationToken.None));
        Assert.Equal(ServiceLayerFailureKind.TransientNotSent, ex.Kind);
    }

    [Fact]
    public async Task ConnectionRefused_IsTransientNotSent()
    {
        _sl.Respond = (_, _) => throw new HttpRequestException(HttpRequestError.ConnectionError, "refused");
        var ex = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            _client.PostAsync<object, JsonObject>("Orders", new { CardCode = "C001" }, CancellationToken.None));
        Assert.Equal(ServiceLayerFailureKind.TransientNotSent, ex.Kind);
    }

    [Fact]
    public async Task Patch_SendsIfMatch_AndMaps412ToConcurrencyConflict()
    {
        _sl.Respond = (_, _) => Task.FromResult(FakeServiceLayer.Json(HttpStatusCode.PreconditionFailed, """{"error":{"code":-2039,"message":"Data changed"}}"""));

        var ex = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            _client.PatchAsync("BusinessPartners('C001')", new { CardName = "New" }, "W/\"abc\"", CancellationToken.None));

        Assert.Equal(ServiceLayerFailureKind.ConcurrencyConflict, ex.Kind);
        Assert.Equal("\"abc\"", _sl.Requests.Single().IfMatch);
    }

    [Fact]
    public async Task GatewayTimeoutOnWrite_IsOutcomeUnknown()
    {
        _sl.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.GatewayTimeout));
        var ex = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            _client.PostAsync<object, JsonObject>("Orders", new { CardCode = "C001" }, CancellationToken.None));
        Assert.Equal(ServiceLayerFailureKind.OutcomeUnknown, ex.Kind);
    }

    [Fact]
    public async Task SqlQuery_BindsEscapedParameters()
    {
        _sl.Respond = (_, _) => Task.FromResult(FakeServiceLayer.Json(HttpStatusCode.OK, """{"value":[{"CardCode":"C001"}]}"""));

        var result = await _client.ExecuteSqlQueryAsync("YF_SALES_BY_CUSTOMER", new Dictionary<string, string> { ["card"] = "O'Brien" }, CancellationToken.None);

        Assert.Single(result.Rows);
        var request = _sl.Requests.Single();
        Assert.EndsWith("SQLQueries('YF_SALES_BY_CUSTOMER')/List", request.Path, StringComparison.Ordinal);
        Assert.Equal("card='O''Brien'", JsonNode.Parse(request.Body!)!["ParamList"]!.GetValue<string>());
    }
}
