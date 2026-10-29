namespace YeaFlow.Fiscal;

/// <summary>Reads named fields (typically UDFs) from an SAP document through Service Layer.</summary>
public interface ISapFieldReader
{
    Task<IReadOnlyDictionary<string, string?>> ReadAsync(
        Guid tenantId,
        Guid companyId,
        string sapObject,
        int docEntry,
        IReadOnlyCollection<string> fields,
        CancellationToken cancellationToken);

    Task WriteAsync(
        Guid tenantId,
        Guid companyId,
        string sapObject,
        int docEntry,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken);
}
