using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Animals;
using Pecualia.Api.Contracts.Auth;
using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Contracts.Farmers;
using Pecualia.Api.Contracts.Farms;
using Pecualia.Api.Contracts.Movements;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Controllers;

public sealed class ApiIntegrationFlowTests : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiWebApplicationFactory _factory;

    public ApiIntegrationFlowTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StartupEndpoints_ReturnHealthAndSwagger()
    {
        using var client = _factory.CreateClient();

        var health = await client.GetAsync("/health");
        var swagger = await client.GetAsync("/swagger/v1/swagger.json");

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        swagger.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthEndpoints_SupportManagerProfileAndSettingsFlow()
    {
        using var client = _factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register/manager", new RegisterManagerRequest(
            "Marta",
            "Gestora",
            "manager.flow@test.local",
            "manager-flow",
            "12345678",
            "Gestoría Flow",
            "COL-100",
            "600000000",
            "Sevilla",
            "Sevilla",
            PlanType.Professional), JsonOptions);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userId = await ExtractUserIdAsync(registerResponse);
        SetTestAuth(client, userId, UserRole.Manager);

        var meResponse = await client.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateResponse = await client.PutAsJsonAsync("/api/auth/settings", new UpdateUserSettingsRequest(
            "Marta Actualizada",
            "Gestora",
            "manager.flow.updated@test.local",
            "manager-flow-updated",
            "Gestoría Flow Actualizada",
            "12345678",
            "87654321"), JsonOptions);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reminderGet = await client.GetAsync("/api/auth/task-reminder-settings");
        reminderGet.StatusCode.Should().Be(HttpStatusCode.OK);

        var reminderPut = await client.PutAsJsonAsync("/api/auth/task-reminder-settings", new UpdateTaskReminderSettingsRequest(
            true,
            "avisos@test.local",
            10), JsonOptions);
        reminderPut.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "manager.flow.updated@test.local",
            "87654321"), JsonOptions);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.SeedAsync(async dbContext =>
        {
            var user = await dbContext.Users.SingleAsync(entity => entity.Id == userId);
            user.IsActive = false;
        });

        var resendResponse = await client.PostAsJsonAsync("/api/auth/resend-activation", new ResendActivationRequest("manager.flow.updated@test.local"), JsonOptions);
        resendResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var forgotPasswordResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("manager.flow.updated@test.local"), JsonOptions);
        forgotPasswordResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ManagerFarmerAndFarmEndpoints_SupportCrudFlow()
    {
        using var client = _factory.CreateClient();
        var managerUserId = await RegisterManagerAsync(client, "manager.farm@test.local", "manager-farm");
        SetTestAuth(client, managerUserId, UserRole.Manager);

        var createFarmerResponse = await client.PostAsJsonAsync("/api/farmers", new CreateFarmerRequest(
            PersonType.Individual,
            "Lucía",
            "Ganadera Romero",
            new DateOnly(1992, 4, 10),
            null,
            null,
            "farmer.crud@test.local",
            "12345678Z",
            "600000001",
            "Calle Real, 1",
            "Sevilla",
            "Sevilla",
            "41001"), JsonOptions);
        createFarmerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var farmerId = await ExtractIdAsync(createFarmerResponse);

        var listResponse = await client.GetAsync("/api/farmers");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailResponse = await client.GetAsync($"/api/farmers/{farmerId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateFarmerResponse = await client.PutAsJsonAsync($"/api/farmers/{farmerId}", new UpdateFarmerRequest(
            PersonType.Individual,
            "Lucía",
            "Ganadera Sierra",
            new DateOnly(1992, 4, 10),
            null,
            null,
            "farmer.crud.updated@test.local",
            "12345678Z",
            "600000002",
            "Calle Real, 2",
            "Dos Hermanas",
            "Sevilla",
            "41701"), JsonOptions);
        updateFarmerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var resendActivationResponse = await client.PostAsync($"/api/farmers/{farmerId}/send-activation", content: null);
        resendActivationResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createFarmResponse = await client.PostAsJsonAsync("/api/farms", new CreateFarmRequest(
            farmerId,
            "Finca Norte",
            "ES410010000555",
            LivestockSpecies.Ovine,
            FarmRegime.Intensive,
            "Sevilla",
            "Sevilla",
            "Paraje Norte",
            "41002",
            120,
            null,
            "Reproducción",
            null,
            null,
            "Lucía Ganadera",
            "Reproducción",
            30,
            123456.7,
            765432.1), JsonOptions);
        createFarmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var farmId = await ExtractIdAsync(createFarmResponse);

        (await client.GetAsync("/api/farms")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/farms/{farmId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/farms/{farmId}/summary")).StatusCode.Should().Be(HttpStatusCode.OK);

        var unlinkResponse = await client.DeleteAsync($"/api/farmers/{farmerId}/manager-link");
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FarmAnimalMovementDashboardAndBillingEndpoints_SupportOperationalFlow()
    {
        using var client = _factory.CreateClient();
        var managerUserId = await RegisterManagerAsync(client, "manager.ops@test.local", "manager-ops");
        SetTestAuth(client, managerUserId, UserRole.Manager);

        var managedFarmerResponse = await client.PostAsJsonAsync("/api/farmers", new CreateFarmerRequest(
            PersonType.Individual,
            "Paula",
            "Titular Vega",
            new DateOnly(1988, 6, 15),
            null,
            null,
            "farmer.ops@test.local",
            "00000014Z",
            "600000003",
            "Calle Vega, 1",
            "Sevilla",
            "Sevilla",
            "41003"), JsonOptions);
        var farmerId = await ExtractIdAsync(managedFarmerResponse);

        var createOriginFarmResponse = await client.PostAsJsonAsync("/api/farms", new CreateFarmRequest(
            farmerId,
            "Explotación Origen",
            "ES410010000661",
            LivestockSpecies.Ovine,
            FarmRegime.Intensive,
            "Sevilla",
            "Sevilla",
            "Camino Uno",
            "41004",
            150,
            null,
            "Reproducción",
            null,
            null,
            "Paula Vega",
            "Reproducción",
            30,
            111111.1,
            222222.2), JsonOptions);
        var originFarmId = await ExtractIdAsync(createOriginFarmResponse);

        var createDestinationFarmResponse = await client.PostAsJsonAsync("/api/farms", new CreateFarmRequest(
            farmerId,
            "Explotación Destino",
            "ES410010000662",
            LivestockSpecies.Ovine,
            FarmRegime.Intensive,
            "Sevilla",
            "Sevilla",
            "Camino Dos",
            "41005",
            150,
            null,
            "Reproducción",
            null,
            null,
            "Paula Vega",
            "Reproducción",
            30,
            111112.1,
            222223.2), JsonOptions);
        var destinationFarmId = await ExtractIdAsync(createDestinationFarmResponse);

        var createAnimalResponse = await client.PostAsJsonAsync("/api/animals", new CreateAnimalRequest(
            originFarmId,
            "ES123456789120",
            2025,
            "Merina",
            "H",
            new DateOnly(2026, 1, 10),
            AnimalRegistrationCause.Entrada,
            "ES410010000001",
            new OvinoCaprinoAnimalRequest(LivestockSpecies.Ovine, "ARR/ARQ", "ARR", "ARQ"),
            null), JsonOptions);
        createAnimalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var animalId = await ExtractIdAsync(createAnimalResponse);

        (await client.GetAsync("/api/animals")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/animals/{animalId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updateAnimalResponse = await client.PutAsJsonAsync($"/api/animals/{animalId}", new UpdateAnimalRequest(
            "ES123456789120",
            2025,
            "Assaf",
            "H",
            new DateOnly(2026, 1, 10),
            AnimalRegistrationCause.Entrada,
            "ES410010000001",
            new OvinoCaprinoAnimalRequest(LivestockSpecies.Ovine, "ARR/ARQ", "ARR", "ARQ"),
            null), JsonOptions);
        updateAnimalResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetAsync($"/api/farms/{originFarmId}/animals")).StatusCode.Should().Be(HttpStatusCode.OK);

        var createBirthResponse = await client.PostAsJsonAsync($"/api/farms/{originFarmId}/births", new CreateFarmBirthRequest(
            new DateOnly(2026, 5, 1),
            3,
            3.2m,
            "Paridera primavera"), JsonOptions);
        createBirthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var birthId = await ExtractIdAsync(createBirthResponse);

        (await client.GetAsync($"/api/farms/{originFarmId}/births")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/farms/{originFarmId}/births/autorreposition-availability")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updateBirthResponse = await client.PutAsJsonAsync($"/api/farms/{originFarmId}/births/{birthId}", new UpdateFarmBirthRequest(
            new DateOnly(2026, 5, 2),
            4,
            3.4m,
            "Paridera ajustada"), JsonOptions);
        updateBirthResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createVaccinationResponse = await client.PostAsJsonAsync($"/api/farms/{originFarmId}/vaccinations", new CreateFarmVaccinationRequest(
            "ES123456789120",
            new DateOnly(2026, 5, 15),
            new DateOnly(2026, 6, 15),
            "Clostridiosis",
            "Primera dosis"), JsonOptions);
        createVaccinationResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var vaccinationId = await ExtractIdAsync(createVaccinationResponse);

        (await client.GetAsync($"/api/farms/{originFarmId}/vaccinations")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updateVaccinationResponse = await client.PutAsJsonAsync($"/api/farms/{originFarmId}/vaccinations/{vaccinationId}", new UpdateFarmVaccinationRequest(
            "ES123456789120",
            new DateOnly(2026, 5, 16),
            new DateOnly(2026, 6, 16),
            "Clostridiosis refuerzo",
            "Segunda dosis"), JsonOptions);
        updateVaccinationResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createIncidentResponse = await client.PostAsJsonAsync($"/api/farms/{originFarmId}/incidents", new CreateFarmIncidentRequest(
            "ES123456789120",
            new DateOnly(2026, 5, 20),
            "Cambio de crotal",
            "Sustitución por pérdida",
            "ES123456789120",
            "ES123456789129"), JsonOptions);
        createIncidentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createInspectionResponse = await client.PostAsJsonAsync($"/api/farms/{originFarmId}/inspections", new CreateFarmInspectionRequest(
            new DateOnly(2026, 5, 21),
            "CC",
            "Inspección favorable",
            "Dr. Test",
            1), JsonOptions);
        createInspectionResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetAsync($"/api/farms/{originFarmId}/incidents")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/farms/{originFarmId}/inspections")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/farms/{originFarmId}/census?year=2026")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/farms/{originFarmId}/balances?year=2026")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/farms/{originFarmId}/book/preview")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/farms/{originFarmId}/book/pdf?sectionIds=general&sectionIds=animals&sectionIds=balance&sectionIds=census&sectionIds=incidents&sectionIds=inspections")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetAsync("/api/movements/breeds/Ovine")).StatusCode.Should().Be(HttpStatusCode.OK);

        var createMovementResponse = await client.PostAsJsonAsync("/api/movements/manual", new CreateManualMovementRequest(
            destinationFarmId,
            MovementDirection.Entry,
            MovementCounterpartyType.Internal,
            originFarmId,
            null,
            null,
            "REMO-INT-E2E",
            "SER-E2E",
            new DateTime(2026, 05, 25, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 25, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            "Transportes Flow",
            "1234BCD",
            AnimalRegistrationCause.Entrada.ToString(),
            null,
            null,
            null,
            [animalId],
            [],
            null), JsonOptions);
        createMovementResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var movementId = await ExtractIdAsync(createMovementResponse);

        (await client.GetAsync($"/api/movements/{movementId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/farms/{originFarmId}/movements")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/movements/{movementId}/confirm", content: null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var previewImportResponse = await client.PostAsJsonAsync("/api/movements/imports/preview", new PreviewMovementImportRequest(
            destinationFarmId,
            MovementImportOperation.Alta,
            "ES410010009999",
            "Origen externo",
            "REMO-PREV-1",
            null,
            new DateTime(2026, 05, 26, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 26, 0, 0, 0, DateTimeKind.Utc),
            null,
            null,
            null,
            null,
            MovementImportCause.Entrada,
            null,
            null,
            null,
            "ES123456789121",
            new SharedAnimalDataRequest(
                new DateOnly(2025, 12, 1),
                2025,
                "Merina",
                "H",
                AnimalRegistrationCause.Entrada,
                new OvinoCaprinoAnimalRequest(LivestockSpecies.Ovine, "ARR/ARQ", "ARR", "ARQ"),
                null),
            null,
            null), JsonOptions);
        previewImportResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var commitImportResponse = await client.PostAsJsonAsync("/api/movements/imports/commit", new CommitMovementImportRequest(
            destinationFarmId,
            MovementImportOperation.Alta,
            "ES410010009999",
            "Origen externo",
            "REMO-COMMIT-1",
            "SER-COMMIT-1",
            new DateTime(2026, 05, 27, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 05, 27, 0, 0, 0, DateTimeKind.Utc),
            null,
            "Camión",
            "Transportes Flow",
            "4321BCD",
            MovementImportCause.Entrada,
            null,
            null,
            null,
            "ES123456789122",
            new SharedAnimalDataRequest(
                new DateOnly(2025, 12, 2),
                2025,
                "Merina",
                "H",
                AnimalRegistrationCause.Entrada,
                new OvinoCaprinoAnimalRequest(LivestockSpecies.Ovine, "ARR/ARQ", "ARR", "ARQ"),
                null),
            null,
            null), JsonOptions);
        commitImportResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetAsync("/api/dashboard/summary")).StatusCode.Should().Be(HttpStatusCode.OK);

        var portalSessionResponse = await client.PostAsync("/api/billing/portal-session", content: null);
        portalSessionResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var checkoutSessionResponse = await client.PostAsJsonAsync("/api/billing/checkout-session", new { planType = "Professional" }, JsonOptions);
        checkoutSessionResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var checkoutStatusResponse = await client.GetAsync("/api/billing/checkout-session-status/test_session");
        checkoutStatusResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var webhookResponse = await client.PostAsync("/api/billing/webhook", JsonContent.Create(new { type = "invoice.paid" }, options: JsonOptions));
        webhookResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var deleteVaccinationResponse = await client.DeleteAsync($"/api/farms/{originFarmId}/vaccinations/{vaccinationId}");
        deleteVaccinationResponse.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.BadRequest);

        var deleteBirthResponse = await client.DeleteAsync($"/api/farms/{originFarmId}/births/{birthId}");
        deleteBirthResponse.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.BadRequest);

        var dischargeResponse = await client.PostAsJsonAsync($"/api/animals/{animalId}/discharge", new DischargeAnimalRequest(
            new DateOnly(2026, 5, 30),
            AnimalDischargeCause.Salida,
            "ES410010000662"), JsonOptions);
        dischargeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<long> ExtractUserIdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("user").GetProperty("id").GetInt64();
    }

    private static async Task<long> ExtractIdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetInt64();
    }

    private static void SetTestAuth(HttpClient client, long userId, UserRole role)
    {
        client.DefaultRequestHeaders.Remove("X-Test-UserId");
        client.DefaultRequestHeaders.Remove("X-Test-Role");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", role.ToString());
    }

    private static async Task<long> RegisterManagerAsync(HttpClient client, string email, string username)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register/manager", new RegisterManagerRequest(
            "Gestor",
            "Principal",
            email,
            username,
            "12345678",
            "Gestoría Cobertura",
            $"COL-{username}",
            "600000000",
            "Sevilla",
            "Sevilla",
            PlanType.Professional), JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ExtractUserIdAsync(response);
    }
}
