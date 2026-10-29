using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using YeaFlow.Application.Common;
using YeaFlow.Domain.Common;
using YeaFlow.Fiscal;

namespace YeaFlow.Api.Infrastructure;

/// <summary>Maps application and domain exceptions to RFC 7807 problem details. SAP messages pass through; stack traces never do.</summary>
public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problems;

    public ProblemDetailsExceptionHandler(IProblemDetailsService problems) => _problems = problems;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        (int status, string title, string? code, IDictionary<string, string[]>? errors) = exception switch
        {
            ValidationException v => (StatusCodes.Status400BadRequest, "Validation failed", "validation", v.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", "forbidden", null),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found", "not_found", null),
            ConflictException c => (StatusCodes.Status409Conflict, "Conflict", c.Code, null),
            FiscalisationRequiredException => (StatusCodes.Status409Conflict, "Fiscalisation pending", "fiscal.not_signed", null),
            DomainException d => (StatusCodes.Status422UnprocessableEntity, "Business rule", d.Code, null),
            _ => (0, string.Empty, null, null),
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title, Detail = exception.Message },
        };
        if (code is not null)
        {
            context.ProblemDetails.Extensions["code"] = code;
        }

        if (errors is not null)
        {
            context.ProblemDetails.Extensions["errors"] = errors;
        }

        return await _problems.TryWriteAsync(context);
    }
}
