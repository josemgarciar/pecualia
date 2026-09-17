using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Controllers;

public sealed class ApiErrorHandlingTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public ApiErrorHandlingTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("http://127.0.0.1:4173", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task UnexpectedError_ReturnsSafeJson_AndRespectsCorsPolicy(string origin, bool allowed)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", origin);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Farmer");
        // An invalid test identity triggers an unexpected exception inside the actual endpoint.
        client.DefaultRequestHeaders.Add("X-Test-UserId", "private-invalid-user-id");

        var response = await client.PostAsJsonAsync("/api/movements/imports/commit", new
        {
            farmId = 1,
            operation = "Baja",
            cause = "Salida",
            departureDate = "2026-09-17T10:00:00Z",
            arrivalDate = "2026-09-17T11:00:00Z",
            rawText = string.Join("\n", Enumerable.Range(0, 17).Select(index => $"ES{123456789010L + index}"))
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Headers.Contains("Access-Control-Allow-Origin").Should().Be(allowed);
        if (allowed)
        {
            response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(origin);
            response.Headers.GetValues("Access-Control-Allow-Credentials").Should().ContainSingle().Which.Should().Be("true");
        }
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("private-invalid-user-id").And.NotContain("FormatException");
        using var json = JsonDocument.Parse(body);
        var traceId = json.RootElement.GetProperty("traceId").GetString();
        traceId.Should().NotBeNullOrWhiteSpace();
        json.RootElement.GetProperty("error").GetString().Should().Contain(traceId);
    }

    [Fact]
    public async Task DomainError_RemainsBadRequest_WithCorsAndOriginalMessage()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "http://127.0.0.1:4173");
        client.DefaultRequestHeaders.Add("X-Test-Role", "Farmer");
        client.DefaultRequestHeaders.Add("X-Test-UserId", "123");

        var response = await client.GetAsync("/api/movements/1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("error").GetString().Should().Be("Movimiento no encontrado.");
    }
}
