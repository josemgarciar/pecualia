using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Pecualia.Api.Controllers;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;

namespace Pecualia.Test.Controllers;

public sealed class ControllerResultsTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsOk_WhenActionSucceeds()
    {
        var result = await ControllerResults.ExecuteAsync(() => Task.FromResult("ok"));

        result.Should().BeOfType<Ok<string>>()
            .Which.Value.Should().Be("ok");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenDomainExceptionIsThrown()
    {
        var result = await ControllerResults.ExecuteAsync<string>(() => throw new DomainException("error de dominio"));

        result.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void GetUserId_ReturnsCustomUserIdClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(AuthClaimTypes.UserId, "42")
        ]));

        principal.GetUserId().Should().Be(42);
    }

    [Fact]
    public void GetRole_ReturnsRoleFromCustomClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(AuthClaimTypes.Role, UserRole.Manager.ToString())
        ]));

        principal.GetRole().Should().Be(UserRole.Manager);
    }

    [Fact]
    public void GetRole_FallsBackToStandardRoleClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, UserRole.Farmer.ToString())
        ]));

        principal.GetRole().Should().Be(UserRole.Farmer);
    }
}
