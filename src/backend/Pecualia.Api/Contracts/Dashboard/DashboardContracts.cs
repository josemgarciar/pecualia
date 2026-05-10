namespace Pecualia.Api.Contracts.Dashboard;

public sealed record DashboardSummaryResponse(
    string Role,
    int Farms,
    int ActiveFarms,
    int TotalAnimals,
    int ManagedFarmers,
    int MovementsThisMonth,
    int PendingActivations,
    int UpcomingActions,
    decimal? MonthlyTrendPercentage,
    IReadOnlyList<MonthlyActivityPointResponse> MonthlyActivity,
    IReadOnlyList<DashboardTaskResponse> PendingTasks);

public sealed record MonthlyActivityPointResponse(
    string MonthLabel,
    int Registrations,
    int Discharges,
    int Births,
    int Movements);

public sealed record DashboardTaskResponse(
    string Kind,
    string Title,
    string Detail,
    string Tone,
    DateOnly DueDate);
