using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

internal static class SubscriptionPlanSupport
{
    internal static PlanType ResolveEffectivePlanType(Subscription? subscription, DateOnly today)
    {
        if (subscription is null ||
            subscription.State != SubscriptionState.Active ||
            subscription.ExpirationDate < today)
        {
            return PlanType.Basic;
        }

        return subscription.PlanType;
    }

    internal static int? GetFarmLimit(UserRole role, PlanType planType) => role switch
    {
        UserRole.Farmer when planType == PlanType.Basic => 2,
        UserRole.Manager when planType == PlanType.Basic => 2,
        UserRole.Manager when planType == PlanType.Professional => 20,
        _ => null
    };

    internal static int? GetManagedFarmerLimit(PlanType planType) => planType switch
    {
        PlanType.Basic => 1,
        PlanType.Professional => 18,
        _ => null
    };

    internal static string BuildFarmLimitError(UserRole role, PlanType planType, int limit) => role switch
    {
        UserRole.Manager => $"El plan {GetPlanLabel(planType)} permite hasta {limit} explotaciones gestionadas. Cambia de plan para crear más.",
        _ => $"El plan {GetPlanLabel(planType)} permite hasta {limit} explotaciones. Cambia de plan para crear más."
    };

    internal static string BuildManagedFarmerLimitError(PlanType planType, int limit)
    {
        var label = limit == 1 ? "ganadero vinculado" : "ganaderos vinculados";
        return $"El plan {GetPlanLabel(planType)} permite hasta {limit} {label}. Cambia de plan para añadir más.";
    }

    private static string GetPlanLabel(PlanType planType) => planType switch
    {
        PlanType.Basic => "Free",
        PlanType.Professional => "Pro",
        PlanType.Enterprise => "Max",
        _ => planType.ToString()
    };
}
