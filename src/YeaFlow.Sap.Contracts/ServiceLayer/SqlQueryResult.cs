using System.Text.Json;

namespace YeaFlow.Sap.Contracts.ServiceLayer;

/// <summary>Rows returned by <c>POST SQLQueries('code')/List</c>, plus the next-link for paging.</summary>
public sealed record SqlQueryResult(IReadOnlyList<JsonElement> Rows, string? NextLink);
