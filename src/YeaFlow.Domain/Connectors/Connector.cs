using System.Security.Cryptography;
using System.Text;
using YeaFlow.Domain.Common;

namespace YeaFlow.Domain.Connectors;

/// <summary>
/// A YeaFlow connector installed next to a tenant's SAP server (decision D-007). It only makes outbound HTTPS calls to
/// the API, authenticating with a secret whose SHA-256 hash is stored here; the secret itself is shown once at registration.
/// </summary>
public sealed class Connector : ITenantScoped
{
    private Connector()
    {
        Name = string.Empty;
        SecretHash = string.Empty;
    }

    private Connector(Guid id, Guid tenantId, string name, string secretHash, DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        SecretHash = secretHash;
        CreatedAt = createdAt;
        IsEnabled = true;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public string SecretHash { get; private set; }

    public bool IsEnabled { get; private set; }

    public string? Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastSeenAt { get; private set; }

    /// <summary>Registers a connector and returns it with its one-time secret.</summary>
    public static (Connector Connector, string Secret) Register(Guid tenantId, string name, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (new Connector(Guid.NewGuid(), tenantId, name.Trim(), Hash(secret), now), secret);
    }

    public bool Verify(string secret) =>
        IsEnabled && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(secret)), Encoding.ASCII.GetBytes(SecretHash));

    public void RecordContact(string? version, DateTimeOffset now)
    {
        Version = version is { Length: > 50 } ? version[..50] : version;
        LastSeenAt = now;
    }

    public void Disable() => IsEnabled = false;

    public bool IsOnline(DateTimeOffset now, TimeSpan window) => LastSeenAt is { } seen && now - seen <= window;

    private static string Hash(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
