namespace YeaFlow.Domain.Common;

/// <summary>
/// Marks an entity that belongs to exactly one tenant. The infrastructure layer applies a query filter
/// and PostgreSQL row-level security to every table whose entity implements this interface.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; }
}

/// <summary>An entity that additionally belongs to one SAP company database of its tenant.</summary>
public interface ICompanyScoped : ITenantScoped
{
    Guid CompanyId { get; }
}
