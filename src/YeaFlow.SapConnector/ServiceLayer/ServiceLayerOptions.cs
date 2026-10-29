using System.ComponentModel.DataAnnotations;

namespace YeaFlow.SapConnector.ServiceLayer;

/// <summary>Connector configuration. Passwords come from environment variables or the secret store, never from source.</summary>
public sealed class ConnectorOptions
{
    public const string SectionName = "Connector";

    public Guid ConnectorId { get; set; }

    [Range(5, 3600)]
    public int HeartbeatSeconds { get; set; } = 30;

    public List<ServiceLayerCompanyOptions> Companies { get; set; } = [];
}

public sealed class ServiceLayerCompanyOptions
{
    /// <summary>Service Layer root, e.g. <c>https://sap-host:50000/b1s/v2/</c>. Must stay on the private network.</summary>
    [Required]
    public Uri? BaseUrl { get; set; }

    [Required]
    public string CompanyDb { get; set; } = string.Empty;

    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Upper bound on concurrent Service Layer sessions for this company. Each session can consume an SAP licence,
    /// so size it with the tenant's licence strategy (Q-004). 20 app users are expected to share far fewer sessions.
    /// </summary>
    [Range(1, 50)]
    public int MaxSessions { get; set; } = 4;

    [Range(5, 600)]
    public int RequestTimeoutSeconds { get; set; } = 60;
}
