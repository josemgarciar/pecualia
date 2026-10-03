using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Models.Enums;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Controllers;

public sealed class TenantAuthorizationIntegrationTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public TenantAuthorizationIntegrationTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData(UserRole.Farmer)]
    [InlineData(UserRole.Manager)]
    public async Task UnrelatedUser_CannotReadOrDeleteAnotherFarmOrAnimal(UserRole role)
    {
        var ownerId = 88000 + (long)role * 10;
        var farmId = ownerId + 1;
        var animalId = ownerId + 2;
        await _factory.SeedAsync(db =>
        {
            var owner = ServiceTestData.CreateUser(ownerId, UserRole.Farmer, "Owner", "Test");
            db.Users.Add(owner);
            db.Farmers.Add(ServiceTestData.CreateFarmer(ownerId, owner));
            db.Farms.Add(ServiceTestData.CreateFarm(farmId, ownerId, LivestockSpecies.Ovine, "Private farm", $"ES060000{farmId:D6}"));
            db.Animals.Add(ServiceTestData.CreateAnimal(animalId, farmId, $"ES060000{animalId:D6}", new DateOnly(2025, 1, 1)));
            return Task.CompletedTask;
        });
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "999999");
        client.DefaultRequestHeaders.Add("X-Test-Role", role.ToString());

        foreach (var path in new[] { $"/api/farms/{farmId}", $"/api/animals/{animalId}", $"/api/farms/{farmId}/book/preview" })
        {
            (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        await client.DeleteAsync($"/api/animals/{animalId}");
        await client.DeleteAsync($"/api/farms/{farmId}");

        await _factory.SeedAsync(db =>
        {
            db.Farms.Any(farm => farm.Id == farmId).Should().BeTrue();
            db.Animals.Any(animal => animal.Id == animalId).Should().BeTrue();
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(AuthorizationPolicies.ManagerOnly)]
    [InlineData(AuthorizationPolicies.FarmerOrManager)]
    public async Task RoleClaimWithoutAuthenticatedIdentity_IsRejected(string policy)
    {
        using var scope = _factory.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(AuthClaimTypes.Role, UserRole.Manager.ToString())]));

        (await authorization.AuthorizeAsync(anonymous, null, policy)).Succeeded.Should().BeFalse();
    }
}
