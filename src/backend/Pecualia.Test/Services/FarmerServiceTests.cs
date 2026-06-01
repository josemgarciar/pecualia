using Pecualia.Api.Contracts.Auth;
using Pecualia.Api.Contracts.Farmers;
using Pecualia.Api.Configuration;
using Pecualia.Api.Infrastructure.Email;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;
using Microsoft.Extensions.Options;

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
            "00000005M",
            "600000000",
            null,
            "Sevilla",
            "Sevilla",
            null), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("El plan Free permite hasta 1 ganadero vinculado. Cambia de plan para añadir más.");
    }

    [Fact]
    public async Task GetManagedFarmersAsync_FiltersBySearchProvinceAndStatus()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);

        var managerUser = ServiceTestData.CreateUser(31, UserRole.Manager, "Marta", "Gestora", email: "manager-filter@test.local");
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        var firstUser = ServiceTestData.CreateUser(32, UserRole.Farmer, "Ana", "Campo", email: "ana-field@test.local");
        var secondUser = ServiceTestData.CreateUser(33, UserRole.Farmer, "Luis", "Monte", email: "luis-monte@test.local");
        var firstFarmer = ServiceTestData.CreateFarmer(firstUser.Id, firstUser, managerId: managerUser.Id, nifCif: "00000014D", status: FarmerStatus.PendingActivation);
        var secondFarmer = ServiceTestData.CreateFarmer(secondUser.Id, secondUser, managerId: managerUser.Id, nifCif: "00000015E", status: FarmerStatus.Active);
        secondFarmer.Province = "Cádiz";

        dbContext.Users.AddRange(managerUser, firstUser, secondUser);
        dbContext.Managers.Add(manager);
        dbContext.Farmers.AddRange(firstFarmer, secondFarmer);
        await dbContext.SaveChangesAsync();

        var results = await service.GetManagedFarmersAsync(managerUser.Id, "ana", "Sevilla", FarmerStatus.PendingActivation.ToString(), CancellationToken.None);

        results.Should().ContainSingle();
        results[0].Email.Should().Be("ana-field@test.local");
    }

    [Fact]
    public async Task CreateManagedFarmerAsync_CreatesPendingFarmerAndSendsActivation()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var emailSender = new CapturingEmailSender();
        var service = CreateService(dbContext, clock, CreateAuthService(dbContext, clock, emailSender));

        var managerUser = ServiceTestData.CreateUser(41, UserRole.Manager, "Marta", "Gestora", email: "manager-create@test.local");
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);

        dbContext.Users.Add(managerUser);
        dbContext.Managers.Add(manager);
        await dbContext.SaveChangesAsync();

        var created = await service.CreateManagedFarmerAsync(managerUser.Id, new CreateFarmerRequest(
            PersonType.Individual,
            "Nueva",
            "Titular",
            new DateOnly(1992, 2, 2),
            null,
            null,
            "new-farmer@test.local",
            "12345678Z",
            "600000001",
            "Calle Alta",
            "Sevilla",
            "Sevilla",
            "41001"), CancellationToken.None);

        created.Status.Should().Be(FarmerStatus.PendingActivation.ToString());
        created.CanResendActivation.Should().BeTrue();
        emailSender.Messages.Should().ContainSingle();
        dbContext.AccountActivationTokens.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateManagedFarmerAsync_ResendsActivationWhenPendingEmailChanges()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var emailSender = new CapturingEmailSender();
        var service = CreateService(dbContext, clock, CreateAuthService(dbContext, clock, emailSender));

        var managerUser = ServiceTestData.CreateUser(51, UserRole.Manager, "Marta", "Gestora", email: "manager-update@test.local");
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        var farmerUser = ServiceTestData.CreateUser(52, UserRole.Farmer, "Pendiente", "Correo", isActive: false, email: "old-managed@test.local");
        var farmer = ServiceTestData.CreateFarmer(farmerUser.Id, farmerUser, managerId: managerUser.Id, nifCif: "00000017G", status: FarmerStatus.PendingActivation);

        dbContext.Users.AddRange(managerUser, farmerUser);
        dbContext.Managers.Add(manager);
        dbContext.Farmers.Add(farmer);
        await dbContext.SaveChangesAsync();

        var updated = await service.UpdateManagedFarmerAsync(managerUser.Id, farmerUser.Id, new UpdateFarmerRequest(
            PersonType.Individual,
            "Pendiente",
            "Correo",
            null,
            null,
            null,
            "new-managed@test.local",
            "87654321X",
            "600000002",
            "Calle Nueva",
            "Sevilla",
            "Sevilla",
            "41001"), CancellationToken.None);

        updated.Email.Should().Be("new-managed@test.local");
        emailSender.Messages.Should().ContainSingle();
        dbContext.AccountActivationTokens.Should().ContainSingle();
    }

    [Fact]
    public async Task ResendActivationAndUnlinkManagedFarmerAsync_WorkAsExpected()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var emailSender = new CapturingEmailSender();
        var service = CreateService(dbContext, clock, CreateAuthService(dbContext, clock, emailSender));

        var managerUser = ServiceTestData.CreateUser(61, UserRole.Manager, "Marta", "Gestora", email: "manager-unlink@test.local");
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        var farmerUser = ServiceTestData.CreateUser(62, UserRole.Farmer, "Pendiente", "Vinculado", isActive: false, email: "pending-link@test.local");
        var farmer = ServiceTestData.CreateFarmer(farmerUser.Id, farmerUser, managerId: managerUser.Id, nifCif: "00000018H", status: FarmerStatus.PendingActivation);

        dbContext.Users.AddRange(managerUser, farmerUser);
        dbContext.Managers.Add(manager);
        dbContext.Farmers.Add(farmer);
        await dbContext.SaveChangesAsync();

        var resent = await service.ResendActivationAsync(managerUser.Id, farmerUser.Id, CancellationToken.None);
        await service.UnlinkManagedFarmerAsync(managerUser.Id, farmerUser.Id, CancellationToken.None);

        resent.Should().BeTrue();
        emailSender.Messages.Should().ContainSingle();
        dbContext.Farmers.Single(entity => entity.UserId == farmerUser.Id).ManagerId.Should().BeNull();
    }

    private static FarmerService CreateService(Pecualia.Api.Data.PecualiaDbContext dbContext, TestClock clock, IAuthService? authService = null)
    {
        var censusProjectionService = new FarmCensusProjectionService(dbContext, clock);
        return new FarmerService(dbContext, authService ?? new FakeAuthService(), clock, censusProjectionService);
    }

    private static AuthService CreateAuthService(Pecualia.Api.Data.PecualiaDbContext dbContext, TestClock clock, CapturingEmailSender emailSender)
    {
        return new AuthService(
            dbContext,
            new FakePasswordHasher(),
            new FakeJwtTokenService(),
            new FakeAccountActivationService(),
            emailSender,
            clock,
            Options.Create(new ActivationOptions
            {
                BaseUrl = "https://pecualia.test/activate-account",
                TokenHours = 72
            }),
            Options.Create(new PasswordResetOptions
            {
                BaseUrl = "https://pecualia.test/reset-password",
                TokenMinutes = 30
            }));
    }

    private sealed class FakeAuthService : IAuthService
    {
        public Task<AuthSessionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AuthSessionResult> RegisterManagerAsync(RegisterManagerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AuthSessionResult> RegisterFarmerAsync(RegisterFarmerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ActivationResponse> ActivateAccountAsync(ActivateAccountRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ActivationResponse> ResendActivationAsync(ResendActivationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<UserProfileResponse?> GetCurrentUserAsync(long userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<UserProfileResponse> UpdateCurrentUserSettingsAsync(long userId, UpdateUserSettingsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash::{password}";
        public bool Verify(string password, string hash) => hash == Hash(password);
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public string CreateToken(Pecualia.Api.Models.Entities.AppUser user) => $"jwt-{user.Id}";
    }

    private sealed class FakeAccountActivationService : IAccountActivationService
    {
        public (string PlainToken, string Hash) GenerateTokenPair()
        {
            return ("generated-token", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("generated-token"))));
        }
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
