using Microsoft.AspNetCore.Diagnostics;
using Npgsql;

namespace Pecualia.Api.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;
        var cause = exception.GetBaseException();
        var databaseError = cause as PostgresException;

        // This diagnostic can be shared without the TXT, request body or database error detail.
        logger.LogError(
            "API failure. Reference={TraceId} Method={Method} Path={Path} ExceptionType={ExceptionType} SqlState={SqlState} Constraint={Constraint} Column={Column}",
            traceId, httpContext.Request.Method, httpContext.Request.Path,
            cause.GetType().Name, databaseError?.SqlState, databaseError?.ConstraintName, databaseError?.ColumnName);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            error = $"No se ha podido completar la operación por un error interno. Revisa si se ha registrado antes de repetirla. Referencia: {traceId}",
            traceId
        }, cancellationToken);
        return true;
    }
}
