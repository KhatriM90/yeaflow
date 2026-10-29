using System.Collections.Concurrent;

namespace YeaFlow.SapConnector.ServiceLayer;

/// <summary>A logged-in Service Layer session (B1SESSION cookie, plus ROUTEID behind a load balancer).</summary>
internal sealed class ServiceLayerSession
{
    public ServiceLayerSession(string sessionId, string? routeId, DateTimeOffset expiresAt)
    {
        SessionId = sessionId;
        RouteId = routeId;
        ExpiresAt = expiresAt;
    }

    public string SessionId { get; }

    public string? RouteId { get; }

    public DateTimeOffset ExpiresAt { get; }

    public bool IsValid(DateTimeOffset now) => now < ExpiresAt;

    public string CookieHeader => RouteId is null ? $"B1SESSION={SessionId}" : $"B1SESSION={SessionId}; ROUTEID={RouteId}";
}

/// <summary>
/// Bounded pool of sessions for one tenant company. Callers lease a session for one request at a time,
/// so the number of concurrent SAP sessions (and licences) never exceeds <see cref="ServiceLayerCompanyOptions.MaxSessions"/>.
/// </summary>
internal sealed class ServiceLayerSessionPool : IDisposable
{
    private readonly ConcurrentBag<ServiceLayerSession> _idle = [];
    private readonly SemaphoreSlim _slots;
    private readonly TimeProvider _time;

    public ServiceLayerSessionPool(int maxSessions, TimeProvider time)
    {
        _slots = new SemaphoreSlim(maxSessions, maxSessions);
        _time = time;
    }

    public int IdleCount => _idle.Count;

    /// <summary>Waits for a free slot and returns an idle valid session, or null when the caller must log in.</summary>
    public async Task<ServiceLayerSession?> AcquireSlotAsync(CancellationToken cancellationToken)
    {
        await _slots.WaitAsync(cancellationToken);
        var now = _time.GetUtcNow();
        while (_idle.TryTake(out var session))
        {
            if (session.IsValid(now))
            {
                return session;
            }
        }

        return null;
    }

    /// <summary>Releases the slot; a still-usable session goes back to the pool.</summary>
    public void Release(ServiceLayerSession? session)
    {
        if (session is not null && session.IsValid(_time.GetUtcNow()))
        {
            _idle.Add(session);
        }

        _slots.Release();
    }

    public void Dispose() => _slots.Dispose();
}
