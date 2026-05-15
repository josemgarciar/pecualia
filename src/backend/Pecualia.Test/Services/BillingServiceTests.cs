using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;
using Pecualia.Api.Contracts.Billing;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class BillingServiceTests
{
    [Fact]
    public async Task CreateCheckoutSessionAsync_Rejects_WhenStripeIsNotConfigured()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock, secretKey: string.Empty);

        var action = () => service.CreateCheckoutSessionAsync(1, UserRole.Farmer, new CreateCheckoutSessionRequest(PlanType.Professional), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Stripe no está configurado todavía en este entorno.");
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_Rejects_WhenUserDoesNotExist()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);

        var action = () => service.CreateCheckoutSessionAsync(999, UserRole.Farmer, new CreateCheckoutSessionRequest(PlanType.Professional), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Usuario no encontrado.");
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_Rejects_WhenUserHasNoEmail()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var user = ServiceTestData.CreateUser(10, UserRole.Farmer, "Ana", "SinCorreo", email: null);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var action = () => service.CreateCheckoutSessionAsync(user.Id, UserRole.Farmer, new CreateCheckoutSessionRequest(PlanType.Professional), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("La cuenta debe tener un correo electrónico para iniciar el cobro con Stripe.");
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_Rejects_WhenFarmerRequestsNonProfessionalPlan()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var user = ServiceTestData.CreateUser(11, UserRole.Farmer, "Ana", "Titular", email: "ana@test.local");

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var action = () => service.CreateCheckoutSessionAsync(user.Id, UserRole.Farmer, new CreateCheckoutSessionRequest(PlanType.Enterprise), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Las cuentas Ganader@ solo pueden contratar el plan Pro.");
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_Rejects_WhenPlanIsBasic()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var user = ServiceTestData.CreateUser(12, UserRole.Manager, "Marta", "Gestora", email: "marta@test.local");

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var action = () => service.CreateCheckoutSessionAsync(user.Id, UserRole.Manager, new CreateCheckoutSessionRequest(PlanType.Basic), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("El plan Free no se contrata con Stripe. Para volver a Free, cancela o cambia la suscripción desde el portal de facturación.");
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_Rejects_WhenStripeSubscriptionAlreadyExists()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var user = ServiceTestData.CreateUser(13, UserRole.Manager, "Marta", "Gestora", email: "marta2@test.local");
        var subscription = CreateSubscription(user.Id, stripeSubscriptionId: "sub_123");

        dbContext.Users.Add(user);
        dbContext.Subscriptions.Add(subscription);
        await dbContext.SaveChangesAsync();

        var action = () => service.CreateCheckoutSessionAsync(user.Id, UserRole.Manager, new CreateCheckoutSessionRequest(PlanType.Professional), CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Ya existe una suscripción conectada con Stripe. Usa \"Gestionar facturación\" para cambiar o cancelar el plan.");
    }

    [Fact]
    public async Task CreatePortalSessionAsync_Rejects_WhenNoStripeCustomerExists()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);
        var subscription = CreateSubscription(20);

        dbContext.Subscriptions.Add(subscription);
        await dbContext.SaveChangesAsync();

        var action = () => service.CreatePortalSessionAsync(subscription.UserId, CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Esta cuenta todavía no tiene un cliente de Stripe asociado. Contrata un plan de pago primero.");
    }

    [Fact]
    public async Task GetCheckoutSessionStatusAsync_Rejects_WhenSessionIdIsMissing()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, clock);

        var action = () => service.GetCheckoutSessionStatusAsync(1, string.Empty, CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Debes indicar el identificador de sesión de Stripe.");
    }

    private static BillingService CreateService(Pecualia.Api.Data.PecualiaDbContext dbContext, TestClock clock, string secretKey = "sk_test_dummy")
    {
        return new BillingService(
            dbContext,
            Options.Create(new StripeOptions
            {
                SecretKey = secretKey,
                WebhookSecret = "whsec_dummy",
                ManagerProfessionalMonthlyPriceId = "price_manager_pro",
                ManagerEnterpriseMonthlyPriceId = "price_manager_enterprise",
                FarmerProfessionalMonthlyPriceId = "price_farmer_pro"
            }),
            Options.Create(new FrontendOptions
            {
                Origin = "https://pecualia.test"
            }),
            clock);
    }

    private static Subscription CreateSubscription(long userId, string? stripeCustomerId = null, string? stripeSubscriptionId = null)
    {
        return new Subscription
        {
            Id = userId,
            UserId = userId,
            Autorenew = false,
            InitialDate = new DateOnly(2026, 01, 01),
            ExpirationDate = new DateOnly(2026, 12, 31),
            PlanType = PlanType.Basic,
            State = SubscriptionState.Active,
            StripeCustomerId = stripeCustomerId,
            StripeSubscriptionId = stripeSubscriptionId
        };
    }
}
