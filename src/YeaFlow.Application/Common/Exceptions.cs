namespace YeaFlow.Application.Common;

/// <summary>Maps to 403. Thrown by the application layer so permissions hold no matter which endpoint or job calls in.</summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}

/// <summary>Maps to 404. Also used for records of other tenants, so their existence is not revealed.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}

/// <summary>Maps to 409: duplicate transaction ID with a different payload, or a concurrent update.</summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string code, string message) : base(message) => Code = code;

    public ConflictException(string code, string message, Exception innerException) : base(message, innerException) => Code = code;

    public string Code { get; }
}
