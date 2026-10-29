using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using YeaFlow.SapConnector.ServiceLayer;

namespace YeaFlow.UnitTests.Connector;

public sealed class DocumentPosterTests : IDisposable
{
    private readonly FakeServiceLayer _sl = new();
    private readonly ServiceLayerClient _client;
    private readonly DocumentPoster _poster;

    public DocumentPosterTests()
    {
        var options = new ServiceLayerCompanyOptions { BaseUrl = new Uri("https://sap.local/b1s/v2/"), CompanyDb = "DB", UserName = "u", Password = "p" };
        _client = new ServiceLayerClient(new HttpClient(_sl), options, TimeProvider.System, NullLogger<ServiceLayerClient>.Instance);
        _poster = new DocumentPoster(_client);
    }

    public void Dispose()
    {
        _client.Dispose();
        _sl.Dispose();
    }

    [Fact]
    public async Task Post_StampsTransactionIdUdf_AndReturnsReference()
    {
        _sl.Respond = (_, _) => Task.FromResult(FakeServiceLayer.Json(HttpStatusCode.Created, """{"DocEntry":42,"DocNum":1042}"""));

        var result = await _poster.PostAsync("Orders", "TX-9", """{"CardCode":"C001"}""", CancellationToken.None);

        Assert.Equal(42, result.DocEntry);
        Assert.Equal(1042, result.DocNum);
        var sent = JsonNode.Parse(_sl.Requests.Single().Body!)!;
        Assert.Equal("TX-9", sent["U_YF_TxnId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Find_ReturnsNull_WhenAbsent()
    {
        _sl.Respond = (_, _) => Task.FromResult(FakeServiceLayer.Json(HttpStatusCode.OK, """{"value":[]}"""));
        Assert.Null(await _poster.FindByTransactionIdAsync("Orders", "TX-9", CancellationToken.None));
        Assert.Contains("U_YF_TxnId%20eq%20%27TX-9%27", _sl.Requests.Single().Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Find_Throws_WhenTransactionIdIsNotUnique()
    {
        _sl.Respond = (_, _) => Task.FromResult(FakeServiceLayer.Json(HttpStatusCode.OK, """{"value":[{"DocEntry":1,"DocNum":1},{"DocEntry":2,"DocNum":2}]}"""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _poster.FindByTransactionIdAsync("Orders", "TX-9", CancellationToken.None));
    }

    [Fact]
    public async Task RejectsUnsafeEntitySet()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _poster.PostAsync("Orders?$x", "TX", "{}", CancellationToken.None));
    }
}
