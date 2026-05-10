using System.Security.Claims;
using Pecualia.Api.Contracts.Animals;
using Pecualia.Api.Contracts.Books;
using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Contracts.Farms;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Services;

namespace Pecualia.Api.Controllers;

public static class FarmController
{
    public static IEndpointRouteBuilder MapFarmController(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/farms").RequireAuthorization(AuthorizationPolicies.FarmerOrManager);

        group.MapGet("/", async (ClaimsPrincipal user, IFarmService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetAccessibleFarmsAsync(user.GetUserId(), user.GetRole(), cancellationToken)));

        group.MapPost("/", async (ClaimsPrincipal user, CreateFarmRequest request, IFarmService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CreateFarmAsync(user.GetUserId(), user.GetRole(), request, cancellationToken)));

        group.MapPut("/{farmId:long}", async (ClaimsPrincipal user, long farmId, UpdateFarmRequest request, IFarmService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.UpdateFarmAsync(user.GetUserId(), user.GetRole(), farmId, request, cancellationToken)));

        group.MapGet("/{farmId:long}/summary", async (ClaimsPrincipal user, long farmId, IFarmService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetSummaryAsync(user.GetUserId(), user.GetRole(), farmId, cancellationToken)));

        group.MapGet("/{farmId:long}/animals", async (
            ClaimsPrincipal user,
            long farmId,
            IAnimalService service,
            long? movementId,
            string? search,
            string? species,
            string? sex,
            string? status,
            int? page,
            int? pageSize,
            CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetFarmAnimalsPageAsync(
                user.GetUserId(),
                user.GetRole(),
                farmId,
                movementId,
                search,
                species,
                sex,
                status,
                page ?? 1,
                pageSize ?? 25,
                cancellationToken)));

        group.MapPost("/{farmId:long}/animals/autorreposition", async (
            ClaimsPrincipal user,
            long farmId,
            CreateAnimalsAutorrepositionRequest request,
            IAnimalService service,
            CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CreateAutorrepositionAnimalsAsync(
                user.GetUserId(),
                user.GetRole(),
                farmId,
                request,
                cancellationToken)));

        group.MapGet("/{farmId:long}/births", async (ClaimsPrincipal user, long farmId, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetBirthsAsync(user.GetUserId(), user.GetRole(), farmId, cancellationToken)));

        group.MapGet("/{farmId:long}/births/autorreposition-availability", async (ClaimsPrincipal user, long farmId, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetAutorrepositionAvailabilityAsync(user.GetUserId(), user.GetRole(), farmId, cancellationToken)));

        group.MapPost("/{farmId:long}/births", async (ClaimsPrincipal user, long farmId, CreateFarmBirthRequest request, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CreateBirthAsync(user.GetUserId(), user.GetRole(), farmId, request, cancellationToken)));

        group.MapPut("/{farmId:long}/births/{birthId:long}", async (ClaimsPrincipal user, long farmId, long birthId, UpdateFarmBirthRequest request, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.UpdateBirthAsync(user.GetUserId(), user.GetRole(), farmId, birthId, request, cancellationToken)));

        group.MapDelete("/{farmId:long}/births/{birthId:long}", async (ClaimsPrincipal user, long farmId, long birthId, IFarmOperationService service, CancellationToken cancellationToken) =>
        {
            try
            {
                await service.DeleteBirthAsync(user.GetUserId(), user.GetRole(), farmId, birthId, cancellationToken);
                return Results.NoContent();
            }
            catch (DomainException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        group.MapGet("/{farmId:long}/deaths", async (ClaimsPrincipal user, long farmId, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetDeathsAsync(user.GetUserId(), user.GetRole(), farmId, cancellationToken)));

        group.MapPost("/{farmId:long}/deaths", async (ClaimsPrincipal user, long farmId, CreateFarmDeathRequest request, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CreateDeathAsync(user.GetUserId(), user.GetRole(), farmId, request, cancellationToken)));

        group.MapGet("/{farmId:long}/vaccinations", async (ClaimsPrincipal user, long farmId, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetVaccinationsAsync(user.GetUserId(), user.GetRole(), farmId, cancellationToken)));

        group.MapPost("/{farmId:long}/vaccinations", async (ClaimsPrincipal user, long farmId, CreateFarmVaccinationRequest request, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CreateVaccinationAsync(user.GetUserId(), user.GetRole(), farmId, request, cancellationToken)));

        group.MapPut("/{farmId:long}/vaccinations/{vaccinationId:long}", async (ClaimsPrincipal user, long farmId, long vaccinationId, UpdateFarmVaccinationRequest request, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.UpdateVaccinationAsync(user.GetUserId(), user.GetRole(), farmId, vaccinationId, request, cancellationToken)));

        group.MapDelete("/{farmId:long}/vaccinations/{vaccinationId:long}", async (ClaimsPrincipal user, long farmId, long vaccinationId, IFarmOperationService service, CancellationToken cancellationToken) =>
        {
            try
            {
                await service.DeleteVaccinationAsync(user.GetUserId(), user.GetRole(), farmId, vaccinationId, cancellationToken);
                return Results.NoContent();
            }
            catch (DomainException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        group.MapGet("/{farmId:long}/census", async (ClaimsPrincipal user, long farmId, int? year, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetCensusAsync(user.GetUserId(), user.GetRole(), farmId, year, cancellationToken)));

        group.MapPut("/{farmId:long}/census", async (ClaimsPrincipal user, long farmId, int year, UpdateFarmCensusRequest request, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.UpdateCensusAsync(user.GetUserId(), user.GetRole(), farmId, year, request, cancellationToken)));

        group.MapGet("/{farmId:long}/balances", async (ClaimsPrincipal user, long farmId, int? year, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetBalanceAsync(user.GetUserId(), user.GetRole(), farmId, year, cancellationToken)));

        group.MapGet("/{farmId:long}/incidents", async (ClaimsPrincipal user, long farmId, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetIncidentsAsync(user.GetUserId(), user.GetRole(), farmId, cancellationToken)));

        group.MapPost("/{farmId:long}/incidents", async (ClaimsPrincipal user, long farmId, CreateFarmIncidentRequest request, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CreateIncidentAsync(user.GetUserId(), user.GetRole(), farmId, request, cancellationToken)));

        group.MapGet("/{farmId:long}/inspections", async (ClaimsPrincipal user, long farmId, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetInspectionsAsync(user.GetUserId(), user.GetRole(), farmId, cancellationToken)));

        group.MapPost("/{farmId:long}/inspections", async (ClaimsPrincipal user, long farmId, CreateFarmInspectionRequest request, IFarmOperationService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.CreateInspectionAsync(user.GetUserId(), user.GetRole(), farmId, request, cancellationToken)));

        group.MapGet("/{farmId:long}/book/preview", async (ClaimsPrincipal user, long farmId, IBookService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetPreviewAsync(user.GetUserId(), user.GetRole(), farmId, cancellationToken)));

        group.MapGet("/{farmId:long}/book/pdf", async (
            ClaimsPrincipal user,
            long farmId,
            string[]? sectionIds,
            IBookService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var file = await service.GeneratePdfAsync(user.GetUserId(), user.GetRole(), farmId, sectionIds, cancellationToken);
                return Results.File(file.Content, file.ContentType, file.FileName);
            }
            catch (DomainException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        });

        group.MapGet("/{farmId:long}", async (ClaimsPrincipal user, long farmId, IFarmService service, CancellationToken cancellationToken) =>
            await ControllerResults.ExecuteAsync(() => service.GetDetailAsync(user.GetUserId(), user.GetRole(), farmId, cancellationToken)));

        return endpoints;
    }
}
