using System.Net;
using System.Text;

namespace YeaFlow.UnitTests.Connector;

/// <summary>Scripted Service Layer: answers Login itself and hands other requests to a per-test responder.</summary>
internal sealed class FakeServiceLayer : HttpMessageHandler
{
    private int _sessionCounter;

    public Func<HttpRequestMessage, string?, Task<HttpResponseMessage>> Respond { get; set; } =
        (_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}"));

    public int Logins { get; private set; }

    public List<(HttpMethod Method, string Path, string? Cookie, string? Body, string? IfMatch)> Requests { get; } = [];

    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.PathAndQuery;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        if (path.EndsWith("/Login", StringComparison.Ordinal))
        {
            Logins++;
            var id = $"S{++_sessionCounter}";
            var response = Json(HttpStatusCode.OK, $$"""{"SessionId":"{{id}}","Version":"1000250","SessionTimeout":30}""");
            response.Headers.Add("Set-Cookie", $"B1SESSION={id}; path=/b1s/v2; Secure; HttpOnly");
            response.Headers.Add("Set-Cookie", "ROUTEID=.node1; path=/b1s");
            return response;
        }

        var cookie = request.Headers.TryGetValues("Cookie", out var values) ? string.Join(";", values) : null;
        Requests.Add((request.Method, path, cookie, body, request.Headers.IfMatch.FirstOrDefault()?.Tag));
        return await Respond(request, cookie);
    }
}
