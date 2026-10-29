using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using YeaFlow.Sap.Contracts.ServiceLayer;

namespace YeaFlow.SapConnector.ServiceLayer;

/// <summary>
/// Service Layer client for one SAP company database: login, pooled sessions, re-login on 401, ETag-aware PATCH,
/// SQLQueries and structured error mapping. All SAP mutations in YeaFlow go through this class; there is no SQL write path.
/// </summary>
public sealed class ServiceLayerClient : IDisposable
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ServiceLayerCompanyOptions _options;
    private readonly ServiceLayerSessionPool _pool;
    private readonly TimeProvider _time;
    private readonly ILogger<ServiceLayerClient> _logger;

    public ServiceLayerClient(HttpClient http, ServiceLayerCompanyOptions options, TimeProvider time, ILogger<ServiceLayerClient> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        _http = http;
        _options = options;
        _time = time;
        _logger = logger;
        _pool = new ServiceLayerSessionPool(options.MaxSessions, time);
        _http.BaseAddress ??= options.BaseUrl ?? throw new ArgumentException("BaseUrl is required.", nameof(options));
        _http.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
    }

    public string CompanyDb => _options.CompanyDb;

    public async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var (value, _) = await GetWithETagAsync<T>(path, cancellationToken);
        return value;
    }

    /// <summary>Reads an entity with its ETag so a later PATCH can detect stale updates.</summary>
    public async Task<(T Value, string? ETag)> GetWithETagAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, path), isWrite: false, cancellationToken);
        var value = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
            ?? throw new ServiceLayerException(ServiceLayerFailureKind.NotFound, $"Service Layer returned an empty body for {path}.");
        return (value, response.Headers.ETag?.Tag);
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: Json) },
            isWrite: true,
            cancellationToken);
        return await response.Content.ReadFromJsonAsync<TResponse>(Json, cancellationToken)
            ?? throw new ServiceLayerException(ServiceLayerFailureKind.OutcomeUnknown, $"Service Layer accepted POST {path} but returned no body.");
    }

    /// <summary>Calls an action that returns no content (e.g. <c>Orders(1)/Close</c>).</summary>
    public async Task PostActionAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, path), isWrite: true, cancellationToken);
    }

    /// <summary>
    /// PATCH with <c>If-Match</c>. Pass the ETag from <see cref="GetWithETagAsync{T}"/>; a changed entity raises
    /// <see cref="ServiceLayerFailureKind.ConcurrencyConflict"/> so the user can resolve it.
    /// </summary>
    public async Task PatchAsync<TRequest>(string path, TRequest body, string? etag, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Patch, path) { Content = JsonContent.Create(body, options: Json) };
                if (etag is not null)
                {
                    request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));
                }

                return request;
            },
            isWrite: true,
            cancellationToken);
    }

    /// <summary>
    /// Runs a pre-registered SQL query (<c>SQLQueries('code')/List</c>) with bound parameters. Report-only read path;
    /// the query text itself is registered by Yeasoft during onboarding, never typed by users.
    /// </summary>
    public async Task<SqlQueryResult> ExecuteSqlQueryAsync(string queryCode, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryCode);
        ArgumentNullException.ThrowIfNull(parameters);
        var paramList = string.Join(",", parameters.Select(p => $"{p.Key}='{p.Value.Replace("'", "''", StringComparison.Ordinal)}'"));
        var path = $"SQLQueries('{Uri.EscapeDataString(queryCode)}')/List";
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { ParamList = paramList }, options: Json) },
            isWrite: false,
            cancellationToken);
        var page = await response.Content.ReadFromJsonAsync<SqlQueryPage>(Json, cancellationToken);
        return new SqlQueryResult(page?.Value ?? [], page?.NextLink);
    }

    /// <summary>Logs in to verify connectivity and licences (onboarding "test connection").</summary>
    public async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        var session = await _pool.AcquireSlotAsync(cancellationToken);
        try
        {
            session ??= await LoginAsync(cancellationToken);
        }
        finally
        {
            _pool.Release(session);
        }
    }

    public void Dispose() => _pool.Dispose();

    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> createRequest, bool isWrite, CancellationToken cancellationToken)
    {
        var session = await _pool.AcquireSlotAsync(cancellationToken);
        try
        {
            session ??= await LoginAsync(cancellationToken);
            var response = await SendOnceAsync(createRequest, session, isWrite, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Session expired or was killed on the SAP side; 401 means SAP did not process the request.
                response.Dispose();
                _logger.LogInformation("Service Layer session expired for {CompanyDb}; logging in again", _options.CompanyDb);
                session = null;
                session = await LoginAsync(cancellationToken);
                response = await SendOnceAsync(createRequest, session, isWrite, cancellationToken);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    response.Dispose();
                    session = null;
                    throw new ServiceLayerException(ServiceLayerFailureKind.AuthenticationFailed, "Service Layer rejected a fresh session.", HttpStatusCode.Unauthorized);
                }
            }

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            using (response)
            {
                throw await MapErrorAsync(response, isWrite, cancellationToken);
            }
        }
        finally
        {
            _pool.Release(session);
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(Func<HttpRequestMessage> createRequest, ServiceLayerSession session, bool isWrite, CancellationToken cancellationToken)
    {
        using var request = createRequest();
        request.Headers.Add("Cookie", session.CookieHeader);
        try
        {
            return await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        }
        catch (HttpRequestException ex) when (IsConnectFailure(ex))
        {
            throw new ServiceLayerException(ServiceLayerFailureKind.TransientNotSent, "Service Layer is unreachable.", inner: ex);
        }
        catch (HttpRequestException ex)
        {
            // The connection broke mid-request: a write may have been committed.
            throw new ServiceLayerException(isWrite ? ServiceLayerFailureKind.OutcomeUnknown : ServiceLayerFailureKind.TransientNotSent, "Connection to Service Layer was interrupted.", inner: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ServiceLayerException(isWrite ? ServiceLayerFailureKind.OutcomeUnknown : ServiceLayerFailureKind.TransientNotSent, "Service Layer did not answer in time.", inner: ex);
        }
    }

    private async Task<ServiceLayerSession> LoginAsync(CancellationToken cancellationToken)
    {
        var login = new LoginRequest(_options.CompanyDb, _options.UserName, _options.Password);
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("Login", login, Json, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ServiceLayerException(ServiceLayerFailureKind.TransientNotSent, "Service Layer login failed: host unreachable.", inner: ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var error = ServiceLayerError.TryParse(body);
                var kind = (int)response.StatusCode >= 500 ? ServiceLayerFailureKind.TransientNotSent : ServiceLayerFailureKind.AuthenticationFailed;
                throw new ServiceLayerException(kind, error?.Message ?? $"Service Layer login failed ({(int)response.StatusCode}).", response.StatusCode, error);
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>(Json, cancellationToken)
                ?? throw new ServiceLayerException(ServiceLayerFailureKind.AuthenticationFailed, "Service Layer login returned no session.");
            var routeId = ReadCookie(response, "ROUTEID");
            var timeout = TimeSpan.FromMinutes(Math.Max(1, result.SessionTimeout));

            // Expire a minute early so a session is never used right at SAP's cut-off.
            var expiresAt = _time.GetUtcNow() + timeout - TimeSpan.FromMinutes(Math.Min(1, timeout.TotalMinutes / 2));
            _logger.LogInformation("Logged in to Service Layer for {CompanyDb} (version {Version})", _options.CompanyDb, result.Version);
            return new ServiceLayerSession(result.SessionId, routeId, expiresAt);
        }
    }

    private static async Task<ServiceLayerException> MapErrorAsync(HttpResponseMessage response, bool isWrite, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var error = ServiceLayerError.TryParse(body);
        var status = response.StatusCode;
        var kind = status switch
        {
            HttpStatusCode.PreconditionFailed => ServiceLayerFailureKind.ConcurrencyConflict,
            HttpStatusCode.NotFound => ServiceLayerFailureKind.NotFound,

            // A gateway in front of Service Layer could not reach it: nothing was processed.
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable => ServiceLayerFailureKind.TransientNotSent,

            // The gateway gave up waiting; SAP may still commit the write.
            HttpStatusCode.GatewayTimeout => isWrite ? ServiceLayerFailureKind.OutcomeUnknown : ServiceLayerFailureKind.TransientNotSent,

            // Service Layer reports business errors (including TransactionNotification) as 400, and some as 500 with an SAP error body.
            _ when error is not null => ServiceLayerFailureKind.Rejected,
            _ when (int)status >= 500 => isWrite ? ServiceLayerFailureKind.OutcomeUnknown : ServiceLayerFailureKind.TransientNotSent,
            _ => ServiceLayerFailureKind.Rejected,
        };

        return new ServiceLayerException(kind, error?.Message ?? $"Service Layer returned {(int)status}.", status, error);
    }

    private static bool IsConnectFailure(HttpRequestException ex) =>
        ex.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError
        || ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused or SocketError.HostNotFound or SocketError.HostUnreachable };

    private static string? ReadCookie(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        foreach (var cookie in cookies)
        {
            var first = cookie.Split(';', 2)[0];
            var parts = first.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0].Trim(), name, StringComparison.Ordinal))
            {
                return parts[1].Trim();
            }
        }

        return null;
    }

    private sealed class SqlQueryPage
    {
        [JsonPropertyName("value")]
        public List<JsonElement>? Value { get; set; }

        [JsonPropertyName("odata.nextLink")]
        public string? NextLink { get; set; }
    }
}
