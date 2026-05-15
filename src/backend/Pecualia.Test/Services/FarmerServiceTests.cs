using Pecualia.Api.Contracts.Auth;
using Pecualia.Api.Contracts.Farmers;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class FarmerServiceTests
{
    [Fact]
    public async Task GetManagedFarmerDetailAsync_UsesCurrentCensusTotal_ForAssociatedFarms()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var authService = new FakeAuthService();
        var service = new FarmerService(dbContext, authService, clock, censusProjectionService);

        var managerUser = ServiceTestData.CreateUser(1, UserRole.Manager, "Marta", "Gestora", email: "manager@test.local");
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        var farmerUser = ServiceTestData.CreateUser(2, UserRole.Farmer, "Paco", "Ganadero", email: "farmer@test.local");
        var farmer = ServiceTestData.CreateFarmer(farmerUser.Id, farmerUser, managerId: managerUser.Id, nifCif: "11223344B");
        var porcineFarm = ServiceTestData.CreateFarm(201, farmer.UserId, LivestockSpecies.Porcine, "Porcina Norte", "ES410010000011", authorisedCapacity: 30, porcineMothersCapacity: 10, porcineFatteningCapacity: 20);
        var ovineFarm = ServiceTestData.CreateFarm(202, farmer.UserId, LivestockSpecies.Ovine, "Ovina Sur", "ES410010000012");
        var porcineBirth = ServiceTestData.CreateBirth(2001, porcineFarm.Id, new DateOnly(2026, 04, 10), 6);
        var ovineBirth = ServiceTestData.CreateBirth(2002, ovineFarm.Id, new DateOnly(2026, 02, 20), 3);

        dbContext.Users.AddRange(managerUser, farmerUser);
        dbContext.Managers.Add(manager);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.AddRange(porcineFarm, ovineFarm);
        dbContext.AnimalBirths.AddRange(porcineBirth, ovineBirth);
        await dbContext.SaveChangesAsync();

        var detail = await service.GetManagedFarmerDetailAsync(managerUser.Id, farmerUser.Id, CancellationToken.None);

        detail.Farms.Should().HaveCount(2);
        detail.Farms.Single(entity => entity.Id == porcineFarm.Id).AnimalCount.Should().Be(6);
        detail.Farms.Single(entity => entity.Id == ovineFarm.Id).AnimalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetManagedFarmerDetailAsync_Throws_ForFarmerNotManagedByRequester()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var authService = new FakeAuthService();
        var service = new FarmerService(dbContext, authService, clock, censusProjectionService);

        var managerUser = ServiceTestData.CreateUser(11, UserRole.Manager, "Marta", "Gestora", email: "manager2@test.local");
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        var otherManagerUser = ServiceTestData.CreateUser(12, UserRole.Manager, "Luis", "Gestor", email: "other-manager@test.local");
        var otherManager = ServiceTestData.CreateManager(otherManagerUser.Id, otherManagerUser);
        var farmerUser = ServiceTestData.CreateUser(13, UserRole.Farmer, "Paco", "Ganadero", email: "farmer2@test.local");
        var farmer = ServiceTestData.CreateFarmer(farmerUser.Id, farmerUser, managerId: managerUser.Id, nifCif: "00000003A");

        dbContext.Users.AddRange(managerUser, otherManagerUser, farmerUser);
        dbContext.Managers.AddRange(manager, otherManager);
        dbContext.Farmers.Add(farmer);
        await dbContext.SaveChangesAsync();

        var action = () => service.GetManagedFarmerDetailAsync(otherManagerUser.Id, farmerUser.Id, CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Ganadero no encontrado.");
    }

    [Fact]
    public async Task CreateManagedFarmerAsync_Rejects_WhenManagerExceedsBasicPlanLimit()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        var authService = new FakeAuthService();
        var service = new FarmerService(dbContext, authService, clock, censusProjectionService);

        var managerUser = ServiceTestData.CreateUser(21, UserRole.Manager, "Marta", "Gestora", email: "limit-manager@test.local");
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        var managedFarmerUser = ServiceTestData.CreateUser(22, UserRole.Farmer, "Uno", "Actual", email: "managed@test.local");
        var managedFarmer = ServiceTestData.CreateFarmer(managedFarmerUser.Id, managedFarmerUser, managerId: managerUser.Id, nifCif: "00000004G");
        var subscription = new Pecualia.Api.Models.Entities.Subscription
        {
            Id = 1,
            UserId = managerUser.Id,
            Autorenew = false,
            InitialDate = new DateOnly(2026, 01, 01),
            ExpirationDate = new DateOnly(2026, 12, 31),
            PlanType = PlanType.Basic,
            State = SubscriptionState.Active
        };

        dbContext.Users.AddRange(managerUser, managedFarmerUser);
        dbContext.Managers.Add(manager);
        dbContext.Farmers.Add(managedFarmer);
        dbContext.Subscriptions.Add(subscription);
        await dbContext.SaveChangesAsync();

        var action = () => service.CreateManagedFarmerAsync(managerUser.Id, new CreateFarmerRequest(
            PersonType.Individual,
            "Nuevo",
            "Ganadero",
            null,
            null,
            null,
            null,
            null,
            "00000005M",
            "600000000",
            null,
            "Sevilla",
            "Sevilla",
            null), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("El plan Free permite hasta 1 ganadero vinculado. Cambia de plan para añadir más.");
    }

    private sealed class FakeAuthService : IAuthService
    {
        public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AuthResponse> RegisterManagerAsync(RegisterManagerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AuthResponse> RegisterFarmerAsync(RegisterFarmerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ActivationResponse> ActivateAccountAsync(ActivateAccountRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ActivationResponse> ResendActivationAsync(ResendActivationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<UserProfileResponse?> GetCurrentUserAsync(long userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<UserProfileResponse> UpdateCurrentUserSettingsAsync(long userId, UpdateUserSettingsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
