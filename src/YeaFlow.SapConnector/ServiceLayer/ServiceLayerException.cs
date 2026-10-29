using System.Net;
using YeaFlow.Sap.Contracts.ServiceLayer;

namespace YeaFlow.SapConnector.ServiceLayer;

/// <summary>How the caller must treat a failed Service Layer call. Drives the posting state machine.</summary>
public enum ServiceLayerFailureKind
{
    /// <summary>SAP processed and rejected the request (validation, credit limit, period, TransactionNotification). Do not retry.</summary>
    Rejected = 1,

    /// <summary>The request provably did not reach SAP. Safe to retry with backoff.</summary>
    TransientNotSent = 2,

    /// <summary>The request may have been processed; reconcile before any retry.</summary>
    OutcomeUnknown = 3,

    /// <summary>The ETag no longer matches; re-read and let the user resolve.</summary>
    ConcurrencyConflict = 4,

    /// <summary>Login failed (bad credentials, licence, company DB).</summary>
    AuthenticationFailed = 5,

    NotFound = 6,
}

public sealed class ServiceLayerException : Exception
{
    public ServiceLayerException(ServiceLayerFailureKind kind, string message, HttpStatusCode? statusCode = null, ServiceLayerError? sapError = null, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        StatusCode = statusCode;
        SapError = sapError;
    }

    public ServiceLayerFailureKind Kind { get; }

    public HttpStatusCode? StatusCode { get; }

    /// <summary>SAP error code and message, safe to show to the user.</summary>
    public ServiceLayerError? SapError { get; }
}
