using YeaFlow.Domain.Common;

namespace YeaFlow.Domain.Tenancy;

public enum SapDatabaseType
{
    SqlServer = 1,
    Hana = 2,
}

/// <summary>One SAP Business One company database belonging to a tenant, reached through a connector.</summary>
public sealed class Company : ITenantScoped
{
    private Company()
    {
        Name = string.Empty;
        SapCompanyDb = string.Empty;
        LocalCurrency = string.Empty;
    }

    public Company(Guid id, Guid tenantId, string name, string sapCompanyDb, SapDatabaseType databaseType, string localCurrency = "KES")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sapCompanyDb);
        Id = id;
        TenantId = tenantId;
        Name = name.Trim();
        SapCompanyDb = sapCompanyDb.Trim();
        DatabaseType = databaseType;
        LocalCurrency = localCurrency;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    /// <summary>SAP company database name used in the Service Layer login (CompanyDB).</summary>
    public string SapCompanyDb { get; private set; }

    public SapDatabaseType DatabaseType { get; private set; }

    public string LocalCurrency { get; private set; }

    public Guid? ConnectorId { get; private set; }

    public bool IsActive { get; private set; }

    public void AssignConnector(Guid connectorId) => ConnectorId = connectorId;
}
