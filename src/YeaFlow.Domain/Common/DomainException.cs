namespace YeaFlow.Domain.Common;

/// <summary>A business rule was violated. Mapped to an RFC 7807 problem response by the API.</summary>
public class DomainException : Exception
{
    public DomainException(string code, string message) : base(message) => Code = code;

    public DomainException(string code, string message, Exception innerException) : base(message, innerException) => Code = code;

    /// <summary>Stable machine-readable code, e.g. <c>seats.limit_reached</c>.</summary>
    public string Code { get; }
}
