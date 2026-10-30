using Microsoft.Extensions.Logging.Abstractions;
using YeaFlow.SapConnector.ServiceLayer;
using YeaFlow.SapConnector.Simulation;

namespace YeaFlow.UnitTests.Connector;

// Local demo simulator, driven through the real Service Layer client so error classification is exercised too.
public sealed class SimulatedServiceLayerTests : IDisposable
{
    private readonly SimulatedServiceLayer _sim = new();
    private readonly ServiceLayerClient _client;
    private readonly DocumentPoster _poster;

    public SimulatedServiceLayerTests()
    {
        var options = new ServiceLayerCompanyOptions { BaseUrl = new Uri("https://simulated-sap.local/b1s/v2/"), CompanyDb = "SBO_DEMO_KE", UserName = "s", Password = "s" };
        _client = new ServiceLayerClient(new HttpClient(_sim), options, TimeProvider.System, NullLogger<ServiceLayerClient>.Instance);
        _poster = new DocumentPoster(_client);
    }

    public void Dispose()
    {
        _client.Dispose();
        _sim.Dispose();
    }

    [Fact]
    public async Task Posts_AndFindsByTransactionId()
    {
        var created = await _poster.PostAsync("Orders", "TX-1", """{"CardCode":"C001"}""", CancellationToken.None);
        var found = await _poster.FindByTransactionIdAsync("Orders", "TX-1", CancellationToken.None);
        Assert.Equal(created.DocNum, found!.DocNum);
        Assert.Null(await _poster.FindByTransactionIdAsync("Orders", "TX-OTHER", CancellationToken.None));
    }

    [Theory]
    [InlineData("REJECT1", ServiceLayerFailureKind.Rejected)]
    [InlineData("DOWN1", ServiceLayerFailureKind.TransientNotSent)]
    [InlineData("LOST1", ServiceLayerFailureKind.OutcomeUnknown)]
    public async Task CustomerCode_SteersTheOutcome(string cardCode, ServiceLayerFailureKind expected)
    {
        var ex = await Assert.ThrowsAsync<ServiceLayerException>(() =>
            _poster.PostAsync("Orders", $"TX-{cardCode}", $$"""{"CardCode":"{{cardCode}}"}""", CancellationToken.None));
        Assert.Equal(expected, ex.Kind);
    }

    [Fact]
    public async Task LostAnswer_LeavesTheDocumentFindable()
    {
        await Assert.ThrowsAsync<ServiceLayerException>(() => _poster.PostAsync("Orders", "TX-LOST", """{"CardCode":"LOST9"}""", CancellationToken.None));
        Assert.NotNull(await _poster.FindByTransactionIdAsync("Orders", "TX-LOST", CancellationToken.None));
    }

    [Fact]
    public async Task SameTransactionTwice_IsRefused()
    {
        await _poster.PostAsync("Orders", "TX-DUP", """{"CardCode":"C001"}""", CancellationToken.None);
        var ex = await Assert.ThrowsAsync<ServiceLayerException>(() => _poster.PostAsync("Orders", "TX-DUP", """{"CardCode":"C001"}""", CancellationToken.None));
        Assert.Equal(ServiceLayerFailureKind.Rejected, ex.Kind);
    }
}
