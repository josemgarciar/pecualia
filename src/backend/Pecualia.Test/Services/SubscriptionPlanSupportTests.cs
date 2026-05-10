using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class SubscriptionPlanSupportTests
{
    [Fact]
    public void ResolveEffectivePlanType_ReturnsBasic_WhenSubscriptionIsMissing()
    {
        var plan = SubscriptionPlanSupport.ResolveEffectivePlanType(null, new DateOnly(2026, 05, 10));

        plan.Should().Be(PlanType.Basic);
    }

    [Fact]
    public void ResolveEffectivePlanType_ReturnsBasic_WhenSubscriptionIsInactive()
    {
        var subscription = new Subscription
        {
            State = SubscriptionState.PastDue,
            PlanType = PlanType.Professional,
            ExpirationDate = new DateOnly(2026, 05, 20)
        };

        var plan = SubscriptionPlanSupport.ResolveEffectivePlanType(subscription, new DateOnly(2026, 05, 10));

        plan.Should().Be(PlanType.Basic);
    }

    [Fact]
    public void ResolveEffectivePlanType_ReturnsBasic_WhenSubscriptionIsExpired()
    {
        var subscription = new Subscription
        {
            State = SubscriptionState.Active,
            PlanType = PlanType.Enterprise,
            ExpirationDate = new DateOnly(2026, 05, 09)
        };

        var plan = SubscriptionPlanSupport.ResolveEffectivePlanType(subscription, new DateOnly(2026, 05, 10));

        plan.Should().Be(PlanType.Basic);
    }

    [Fact]
    public void ResolveEffectivePlanType_ReturnsSubscriptionPlan_WhenSubscriptionIsActiveAndCurrent()
    {
        var subscription = new Subscription
        {
            State = SubscriptionState.Active,
            PlanType = PlanType.Professional,
            ExpirationDate = new DateOnly(2026, 05, 31)
        };

        var plan = SubscriptionPlanSupport.ResolveEffectivePlanType(subscription, new DateOnly(2026, 05, 10));

        plan.Should().Be(PlanType.Professional);
    }

    [Theory]
    [InlineData(UserRole.Farmer, PlanType.Basic, 2)]
    [InlineData(UserRole.Manager, PlanType.Basic, 2)]
    [InlineData(UserRole.Manager, PlanType.Professional, 20)]
    public void GetFarmLimit_ReturnsExpectedLimit_ForRestrictedPlans(UserRole role, PlanType planType, int expectedLimit)
    {
        SubscriptionPlanSupport.GetFarmLimit(role, planType).Should().Be(expectedLimit);
    }

    [Theory]
    [InlineData(UserRole.Farmer, PlanType.Professional)]
    [InlineData(UserRole.Farmer, PlanType.Enterprise)]
    [InlineData(UserRole.Manager, PlanType.Enterprise)]
    public void GetFarmLimit_ReturnsNull_WhenPlanHasNoFarmCap(UserRole role, PlanType planType)
    {
        SubscriptionPlanSupport.GetFarmLimit(role, planType).Should().BeNull();
    }

    [Theory]
    [InlineData(PlanType.Basic, 1)]
    [InlineData(PlanType.Professional, 18)]
    public void GetManagedFarmerLimit_ReturnsExpectedLimits(PlanType planType, int expectedLimit)
    {
        SubscriptionPlanSupport.GetManagedFarmerLimit(planType).Should().Be(expectedLimit);
    }

    [Fact]
    public void GetManagedFarmerLimit_ReturnsNull_ForEnterprise()
    {
        SubscriptionPlanSupport.GetManagedFarmerLimit(PlanType.Enterprise).Should().BeNull();
    }

    [Fact]
    public void BuildFarmLimitError_UsesManagerCopy_ForManagers()
    {
        var message = SubscriptionPlanSupport.BuildFarmLimitError(UserRole.Manager, PlanType.Professional, 20);

        message.Should().Contain("Pro");
        message.Should().Contain("20 explotaciones gestionadas");
    }

    [Fact]
    public void BuildManagedFarmerLimitError_UsesSingular_WhenLimitIsOne()
    {
        var message = SubscriptionPlanSupport.BuildManagedFarmerLimitError(PlanType.Basic, 1);

        message.Should().Contain("Free");
        message.Should().Contain("1 ganadero vinculado");
    }
}
