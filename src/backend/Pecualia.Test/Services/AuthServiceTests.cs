using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;
using Pecualia.Api.Contracts.Auth;
using Pecualia.Api.Infrastructure.Email;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task RegisterManagerAsync_CreatesManagerWithBasicSubscription()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var emailSender = new CapturingEmailSender();
        var service = CreateService(dbContext, clock, emailSender);

        var response = await service.RegisterManagerAsync(new RegisterManagerRequest(
            "Marta",
            "Gestora",
            "manager@test.local",
            "marta-manager",
            "12345678",
            "Gestoría Sierra",
            "COL-001",
            "600000000",
            "Sevilla",
            "Sevilla",
            PlanType.Professional), CancellationToken.None);

        response.Token.Should().Be("jwt-1");
        response.User.Role.Should().Be(UserRole.Manager.ToString());
        response.User.OrganizationName.Should().Be("Gestoría Sierra");

        var manager = dbContext.Managers.Single();
        manager.OrganizationName.Should().Be("Gestoría Sierra");

        var subscription = dbContext.Subscriptions.Single();
        subscription.PlanType.Should().Be(PlanType.Basic);
        subscription.State.Should().Be(SubscriptionState.Active);
    }

    [Fact]
    public async Task RegisterFarmerAsync_Rejects_WhenManagerBasicPlanIsAtCapacity()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var emailSender = new CapturingEmailSender();
        var service = CreateService(dbContext, clock, emailSender);

        var managerUser = ServiceTestData.CreateUser(10, UserRole.Manager, "Marta", "Gestora", email: "manager@test.local");
        managerUser.Username = "manager";
        managerUser.PasswordHash = "hash::secret";
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        manager.InvitationCode = "INVITE01";
        var subscription = new Subscription
        {
            Id = 1,
            UserId = managerUser.Id,
            Autorenew = false,
            InitialDate = new DateOnly(2026, 01, 01),
            ExpirationDate = new DateOnly(2026, 12, 31),
            PlanType = PlanType.Basic,
            State = SubscriptionState.Active
        };
        var existingFarmerUser = ServiceTestData.CreateUser(11, UserRole.Farmer, "Paco", "Actual", email: "existing@test.local");
        var existingFarmer = ServiceTestData.CreateFarmer(existingFarmerUser.Id, existingFarmerUser, managerId: managerUser.Id, nifCif: "00000006Y");

        dbContext.Users.AddRange(managerUser, existingFarmerUser);
        dbContext.Managers.Add(manager);
        dbContext.Subscriptions.Add(subscription);
        dbContext.Farmers.Add(existingFarmer);
        await dbContext.SaveChangesAsync();

        var action = () => service.RegisterFarmerAsync(new RegisterFarmerRequest(
            "Nuevo",
            "Ganadero",
            "nuevo@test.local",
            "nuevo-ganadero",
            "12345678",
            "12345678Z",
            "600000000",
            null,
            "Sevilla",
            "Sevilla",
            null,
            PersonType.Individual,
            null,
            "INVITE01",
            null), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("El plan Free permite hasta 1 ganadero vinculado. Cambia de plan para añadir más.");
    }

    [Fact]
    public async Task RegisterFarmerAsync_CreatesActiveFarmerLinkedToManager()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock, new CapturingEmailSender());

        var managerUser = ServiceTestData.CreateUser(15, UserRole.Manager, "Marta", "Gestora", email: "manager-ok@test.local");
        managerUser.Username = "manager-ok";
        managerUser.PasswordHash = "hash::secret";
        var manager = ServiceTestData.CreateManager(managerUser.Id, managerUser);
        manager.InvitationCode = "INVITE99";

        dbContext.Users.Add(managerUser);
        dbContext.Managers.Add(manager);
        await dbContext.SaveChangesAsync();

        var response = await service.RegisterFarmerAsync(new RegisterFarmerRequest(
            "Nuevo",
            "Ganadero",
            "nuevo-ok@test.local",
            "nuevo-ok",
            "12345678",
            "12345678Z",
            "600000000",
            "Calle Test",
            "Sevilla",
            "Sevilla",
            "41001",
            PersonType.Individual,
            new DateOnly(1990, 1, 1),
            "INVITE99",
            null), CancellationToken.None);

        response.Token.Should().StartWith("jwt-");
        dbContext.Farmers.Should().ContainSingle(entity => entity.ManagerId == managerUser.Id && entity.Status == FarmerStatus.Active);
    }

    [Fact]
    public async Task LoginAsync_ReturnsToken_WhenCredentialsAreValid()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var emailSender = new CapturingEmailSender();
        var service = CreateService(dbContext, clock, emailSender);

        var user = ServiceTestData.CreateUser(20, UserRole.Farmer, "Ana", "Titular", email: "ana@test.local");
        user.Username = "ana";
        user.PasswordHash = "hash::12345678";
        user.IsActive = true;
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await service.LoginAsync(new LoginRequest("ana@test.local", "12345678"), CancellationToken.None);

        response.Token.Should().Be("jwt-20");
        response.User.Email.Should().Be("ana@test.local");
    }

    [Fact]
    public async Task LoginAsync_Rejects_WhenCredentialsAreInvalid()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock, new CapturingEmailSender());

        var user = ServiceTestData.CreateUser(21, UserRole.Farmer, "Ana", "Titular", email: "ana2@test.local");
        user.Username = "ana2";
        user.PasswordHash = "hash::12345678";
        user.IsActive = true;
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var action = () => service.LoginAsync(new LoginRequest("ana2@test.local", "bad-password"), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Credenciales inválidas.");
    }

    [Fact]
    public async Task LoginAsync_Rejects_WhenUserIsInactive()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock, new CapturingEmailSender());

        var user = ServiceTestData.CreateUser(22, UserRole.Farmer, "Ana", "Titular", isActive: false, email: "ana3@test.local");
        user.Username = "ana3";
        user.PasswordHash = "hash::12345678";
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var action = () => service.LoginAsync(new LoginRequest("ana3", "12345678"), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("La cuenta aún no está activa. Revisa el correo de activación.");
    }

    [Fact]
    public async Task ActivateAccountAsync_ActivatesPendingUser_AndConsumesToken()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock, new CapturingEmailSender());

        const string plainToken = "activation-token";
        var user = ServiceTestData.CreateUser(30, UserRole.Farmer, "Paco", "Pendiente", isActive: false, email: "pending@test.local");
        user.Username = null;
        user.PasswordHash = null;
        user.EmailVerifiedAt = null;
        var farmer = ServiceTestData.CreateFarmer(user.Id, user, nifCif: "00000008Z", status: FarmerStatus.PendingActivation);
        var token = new AccountActivationToken
        {
            Id = 1,
            UserId = user.Id,
            User = user,
            TokenHash = ComputeTokenHash(plainToken),
            CreatedAt = clock.UtcNow,
            ExpiresAt = clock.UtcNow.AddHours(24)
        };

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.AccountActivationTokens.Add(token);
        await dbContext.SaveChangesAsync();

        var response = await service.ActivateAccountAsync(new ActivateAccountRequest(plainToken, "paco-activo", "12345678"), CancellationToken.None);

        response.Message.Should().Be("Cuenta activada correctamente. Ya puedes iniciar sesión.");
        user.IsActive.Should().BeTrue();
        user.Username.Should().Be("paco-activo");
        user.PasswordHash.Should().Be("hash::12345678");
        farmer.Status.Should().Be(FarmerStatus.Active);
        token.UsedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ReturnsGenericMessage_AndCreatesToken_ForActiveUser()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var emailSender = new CapturingEmailSender();
        var service = CreateService(dbContext, clock, emailSender);

        var user = ServiceTestData.CreateUser(40, UserRole.Farmer, "Rosa", "Activa", email: "rosa@test.local");
        user.PasswordHash = "hash::12345678";
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await service.ForgotPasswordAsync(new ForgotPasswordRequest("rosa@test.local"), CancellationToken.None);

        response.Message.Should().Be("Si existe una cuenta con ese correo, recibirás un enlace para restablecer tu contraseña.");
        dbContext.PasswordResetTokens.Should().ContainSingle();
        emailSender.Messages.Should().ContainSingle();
        emailSender.Messages[0].To.Should().Be("rosa@test.local");
    }

    [Fact]
    public async Task ForgotPasswordAsync_ReturnsGenericMessage_WithoutCreatingToken_ForUnknownUser()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var emailSender = new CapturingEmailSender();
        var service = CreateService(dbContext, clock, emailSender);

        var response = await service.ForgotPasswordAsync(new ForgotPasswordRequest("missing@test.local"), CancellationToken.None);

        response.Message.Should().Be("Si existe una cuenta con ese correo, recibirás un enlace para restablecer tu contraseña.");
        dbContext.PasswordResetTokens.Should().BeEmpty();
        emailSender.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ResetPasswordAsync_UpdatesHash_AndConsumesToken()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock, new CapturingEmailSender());

        const string plainToken = "reset-token";
        var user = ServiceTestData.CreateUser(50, UserRole.Farmer, "Luis", "Activo", email: "luis@test.local");
        user.PasswordHash = "hash::oldpass";
        var token = new PasswordResetToken
        {
            Id = 1,
            UserId = user.Id,
            User = user,
            TokenHash = ComputeTokenHash(plainToken),
            CreatedAt = clock.UtcNow,
            ExpiresAt = clock.UtcNow.AddMinutes(30)
        };

        dbContext.Users.Add(user);
        dbContext.PasswordResetTokens.Add(token);
        await dbContext.SaveChangesAsync();

        var response = await service.ResetPasswordAsync(new ResetPasswordRequest(plainToken, "new-password"), CancellationToken.None);

        response.Message.Should().Be("Tu contraseña se ha restablecido correctamente. Ya puedes iniciar sesión.");
        user.PasswordHash.Should().Be("hash::new-password");
        token.UsedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task ResetPasswordAsync_Rejects_WhenTokenIsInvalid()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock, new CapturingEmailSender());

        var action = () => service.ResetPasswordAsync(new ResetPasswordRequest("missing-token", "new-password"), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("El enlace de recuperación no es válido o ha caducado.");
    }

    [Fact]
    public async Task ResendActivationAsync_ReturnsActivationUrl_ForInactiveFarmer()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var emailSender = new CapturingEmailSender();
        var service = CreateService(dbContext, clock, emailSender);
        var user = ServiceTestData.CreateUser(60, UserRole.Farmer, "Ina", "Activa", isActive: false, email: "inactive@test.local");
        var farmer = ServiceTestData.CreateFarmer(user.Id, user, nifCif: "00000010H", status: FarmerStatus.PendingActivation);

        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        await dbContext.SaveChangesAsync();

        var response = await service.ResendActivationAsync(new ResendActivationRequest(user.Email!), CancellationToken.None);

        response.Message.Should().Be("Se ha reenviado la invitación.");
        response.ActivationUrl.Should().NotBeNull();
        dbContext.AccountActivationTokens.Should().ContainSingle();
        emailSender.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsManagerProfileWithSubscription()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock, new CapturingEmailSender());
        var user = ServiceTestData.CreateUser(70, UserRole.Manager, "Mar", "Perfil", email: "perfil@test.local");
        user.Username = "perfil";
        var manager = ServiceTestData.CreateManager(user.Id, user);
        var subscription = new Subscription
        {
            Id = 2,
            UserId = user.Id,
            Autorenew = true,
            InitialDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2026, 12, 31),
            PlanType = PlanType.Professional,
            State = SubscriptionState.Active
        };

        dbContext.Users.Add(user);
        dbContext.Managers.Add(manager);
        dbContext.Subscriptions.Add(subscription);
        await dbContext.SaveChangesAsync();

        var profile = await service.GetCurrentUserAsync(user.Id, CancellationToken.None);

        profile.Should().NotBeNull();
        profile!.OrganizationName.Should().Be(manager.OrganizationName);
        profile.PlanType.Should().Be(PlanType.Professional.ToString());
        profile.SubscriptionAutorenew.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateCurrentUserSettingsAsync_UpdatesManagerProfileAndPassword()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock, new CapturingEmailSender());
        var user = ServiceTestData.CreateUser(71, UserRole.Manager, "Mar", "Vieja", email: "old@test.local");
        user.Username = "old-user";
        user.PasswordHash = "hash::old-password";
        var manager = ServiceTestData.CreateManager(user.Id, user);

        dbContext.Users.Add(user);
        dbContext.Managers.Add(manager);
        await dbContext.SaveChangesAsync();

        var profile = await service.UpdateCurrentUserSettingsAsync(user.Id, new UpdateUserSettingsRequest(
            "María",
            "Nueva",
            "new@test.local",
            "new-user",
            "Gestoría Nueva",
            "old-password",
            "new-password"), CancellationToken.None);

        profile.Email.Should().Be("new@test.local");
        profile.Username.Should().Be("new-user");
        manager.OrganizationName.Should().Be("Gestoría Nueva");
        user.PasswordHash.Should().Be("hash::new-password");
    }

    [Fact]
    public async Task UpdateCurrentUserSettingsAsync_Rejects_WhenCurrentPasswordIsMissing()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock, new CapturingEmailSender());
        var user = ServiceTestData.CreateUser(72, UserRole.Farmer, "Paco", "Actual", email: "paco-current@test.local");
        user.Username = "paco-current";
        user.PasswordHash = "hash::old-password";

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var action = () => service.UpdateCurrentUserSettingsAsync(user.Id, new UpdateUserSettingsRequest(
            "Paco",
            "Actualizado",
            "paco-current@test.local",
            "paco-current",
            null,
            null,
            "new-password"), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Debes indicar la contraseña actual para establecer una nueva.");
    }

    private static AuthService CreateService(Pecualia.Api.Data.PecualiaDbContext dbContext, TestClock clock, CapturingEmailSender emailSender)
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

    private static string ComputeTokenHash(string plainToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainToken)));
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash::{password}";

        public bool Verify(string password, string hash) => hash == Hash(password);
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public string CreateToken(AppUser user) => $"jwt-{user.Id}";
    }

    private sealed class FakeAccountActivationService : IAccountActivationService
    {
        public (string PlainToken, string Hash) GenerateTokenPair()
        {
            const string plainToken = "generated-token";
            return (plainToken, ComputeTokenHash(plainToken));
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
