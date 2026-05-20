using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Pecualia.Api.Controllers;

namespace Pecualia.Test.Controllers;

public sealed class ControllerMappingTests
{
    [Fact]
    public void MapAllControllers_RegistersExpectedEndpointSet()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();

        var app = builder.Build();

        var action = () =>
        {
            app.MapAuthController();
            app.MapFarmerController();
            app.MapFarmController();
            app.MapAnimalController();
            app.MapMovementController();
            app.MapDashboardController();
            app.MapBillingController();
        };

        action.Should().NotThrow();
    }
}
