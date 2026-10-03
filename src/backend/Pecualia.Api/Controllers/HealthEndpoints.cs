using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Data;
using Pecualia.Api.Services;

namespace Pecualia.Api.Controllers;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => CreateResponse("ok", StatusCodes.Status200OK));
        endpoints.MapGet("/health/ready", CheckReadinessAsync);
        endpoints.MapGet("/health", CheckReadinessAsync);
        return endpoints;
    }

    private static async Task<IResult> CheckReadinessAsync(
        DatabaseBootstrapState bootstrapState,
        PecualiaDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!bootstrapState.IsReady)
        {
            return CreateResponse("initializing", StatusCodes.Status503ServiceUnavailable);
        }

        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? CreateResponse("ok", StatusCodes.Status200OK)
            : CreateResponse("degraded", StatusCodes.Status503ServiceUnavailable);
    }

    private static IResult CreateResponse(string status, int statusCode) =>
        Results.Json(new { status, service = "pecualia-api", utc = DateTimeOffset.UtcNow }, statusCode: statusCode);
}
