using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Dashboard;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(long userId, UserRole role, CancellationToken cancellationToken);
}

public sealed class DashboardService(PecualiaDbContext dbContext, IClock clock) : IDashboardService
{
    private const int ChartMonthCount = 7;
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    public async Task<DashboardSummaryResponse> GetSummaryAsync(long userId, UserRole role, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var chartStartMonth = monthStart.AddMonths(-(ChartMonthCount - 1));
        var chartPoints = BuildChartSkeleton(chartStartMonth);

        var accessibleFarmIds = await GetAccessibleFarmIdsAsync(userId, role, cancellationToken);
        var farms = await dbContext.Farms
            .AsNoTracking()
            .Where(entity => accessibleFarmIds.Contains(entity.Id))
            .ToListAsync(cancellationToken);

        var animals = await dbContext.Animals
            .AsNoTracking()
            .Where(entity => accessibleFarmIds.Contains(entity.LivestockFarmId))
            .ToListAsync(cancellationToken);

        var animalIds = animals.Select(entity => entity.Id).ToList();

        var births = await dbContext.AnimalBirths
            .AsNoTracking()
            .Where(entity => accessibleFarmIds.Contains(entity.LivestockFarmId))
            .ToListAsync(cancellationToken);

        var vaccinations = animalIds.Count == 0
            ? new List<Vaccination>()
            : await dbContext.Vaccinations
                .AsNoTracking()
                .Where(entity => animalIds.Contains(entity.AnimalId))
                .ToListAsync(cancellationToken);

        var movements = await dbContext.MovementCertificates
            .AsNoTracking()
            .Where(entity => entity.OriginLivestockId != null && accessibleFarmIds.Contains(entity.OriginLivestockId.Value))
            .ToListAsync(cancellationToken);

        var inspections = await dbContext.Inspections
            .AsNoTracking()
            .Include(entity => entity.LivestockFarm)
            .Where(entity => accessibleFarmIds.Contains(entity.LivestockFarmId))
            .ToListAsync(cancellationToken);

        foreach (var animal in animals.Where(entity => entity.RegistrationDate >= chartStartMonth && entity.RegistrationDate <= today))
        {
            Increment(chartPoints, animal.RegistrationDate!.Value, point => point.Registrations++);
        }

        foreach (var animal in animals.Where(entity => entity.DischargeDate >= chartStartMonth && entity.DischargeDate <= today))
        {
            Increment(chartPoints, animal.DischargeDate!.Value, point => point.Discharges++);
        }

        foreach (var birth in births.Where(entity => entity.BirthDate >= chartStartMonth && entity.BirthDate <= today))
        {
            Increment(chartPoints, birth.BirthDate, point => point.Births += birth.OffspringNumber);
        }

        foreach (var movement in movements.Where(entity => entity.DepartureDate >= chartStartMonth && entity.DepartureDate <= today))
        {
            Increment(chartPoints, movement.DepartureDate, point => point.Movements += movement.NumberOfAnimals);
        }

        var farmNamesById = farms.ToDictionary(entity => entity.Id, entity => entity.Name);
        var animalFarmNames = animals.ToDictionary(
            entity => entity.Id,
            entity => farmNamesById.GetValueOrDefault(entity.LivestockFarmId, "Explotación"));

        var pendingTasks = BuildPendingTasks(vaccinations, inspections, animalFarmNames, today);
        var previousMonth = monthStart.AddMonths(-1);

        return new DashboardSummaryResponse(
            role.ToString(),
            Farms: farms.Count,
            ActiveFarms: farms.Count(entity => entity.Status == FarmStatus.Active),
            TotalAnimals: animals.Count,
            ManagedFarmers: role == UserRole.Manager
                ? await dbContext.Farmers.CountAsync(entity => entity.ManagerId == userId, cancellationToken)
                : 0,
            MovementsThisMonth: movements
                .Where(entity => entity.DepartureDate >= monthStart && entity.DepartureDate <= today)
                .Sum(entity => entity.NumberOfAnimals),
            PendingActivations: role == UserRole.Manager
                ? await dbContext.Farmers.CountAsync(entity => entity.ManagerId == userId && entity.Status == FarmerStatus.PendingActivation, cancellationToken)
                : 0,
            UpcomingActions: pendingTasks.Count,
            MonthlyTrendPercentage: CalculateTrend(chartPoints, monthStart, previousMonth),
            MonthlyActivity: chartPoints
                .OrderBy(entity => entity.MonthStart)
                .Select(entity => new MonthlyActivityPointResponse(
                    entity.MonthLabel,
                    entity.Registrations,
                    entity.Discharges,
                    entity.Births,
                    entity.Movements))
                .ToList(),
            PendingTasks: pendingTasks);
    }

    private async Task<List<long>> GetAccessibleFarmIdsAsync(long userId, UserRole role, CancellationToken cancellationToken)
    {
        if (role == UserRole.Manager)
        {
            return await dbContext.Farms
                .AsNoTracking()
                .Where(entity => entity.Farmer.ManagerId == userId)
                .Select(entity => entity.Id)
                .ToListAsync(cancellationToken);
        }

        return await dbContext.Farms
            .AsNoTracking()
            .Where(entity => entity.FarmerId == userId)
            .Select(entity => entity.Id)
            .ToListAsync(cancellationToken);
    }

    private static List<DashboardTaskResponse> BuildPendingTasks(
        IReadOnlyCollection<Vaccination> vaccinations,
        IReadOnlyCollection<Inspection> inspections,
        IReadOnlyDictionary<long, string> animalFarmNames,
        DateOnly today)
    {
        var tasks = new List<DashboardTaskResponse>();
        var nextTwoWeeks = today.AddDays(14);

        tasks.AddRange(vaccinations
            .Where(entity => entity.NextDose is not null && entity.NextDose.Value <= nextTwoWeeks)
            .OrderBy(entity => entity.NextDose)
            .Take(3)
            .Select(entity => new DashboardTaskResponse(
                $"Vacunación {entity.VaccinationType.ToLowerInvariant()} pendiente",
                $"{animalFarmNames.GetValueOrDefault(entity.AnimalId, "Explotación")} · {FormatDueText(entity.NextDose!.Value, today)}",
                entity.NextDose <= today ? "danger" : "warning",
                entity.NextDose!.Value)));

        tasks.AddRange(inspections
            .Where(entity => entity.InspectionDate >= today && entity.InspectionDate <= today.AddDays(21))
            .OrderBy(entity => entity.InspectionDate)
            .Take(3)
            .Select(entity => new DashboardTaskResponse(
                string.IsNullOrWhiteSpace(entity.Reason) ? "Inspección programada" : entity.Reason!,
                $"{entity.LivestockFarm.Name} · {FormatDueText(entity.InspectionDate, today)}",
                "info",
                entity.InspectionDate)));

        return tasks
            .OrderBy(entity => entity.DueDate)
            .ThenBy(entity => entity.Title)
            .Take(5)
            .ToList();
    }

    private static decimal? CalculateTrend(IReadOnlyCollection<MonthlyChartPoint> points, DateOnly currentMonthStart, DateOnly previousMonthStart)
    {
        var current = points.Single(entity => entity.MonthStart == currentMonthStart).Total;
        var previous = points.Single(entity => entity.MonthStart == previousMonthStart).Total;

        if (previous == 0)
        {
            return current == 0 ? 0m : null;
        }

        return Math.Round(((decimal)(current - previous) / previous) * 100m, 1, MidpointRounding.AwayFromZero);
    }

    private static List<MonthlyChartPoint> BuildChartSkeleton(DateOnly startMonth)
    {
        var points = new List<MonthlyChartPoint>(ChartMonthCount);
        for (var index = 0; index < ChartMonthCount; index++)
        {
            var monthStart = startMonth.AddMonths(index);
            points.Add(new MonthlyChartPoint(monthStart, BuildMonthLabel(monthStart)));
        }

        return points;
    }

    private static void Increment(List<MonthlyChartPoint> points, DateOnly date, Action<MonthlyChartPoint> apply)
    {
        var monthStart = new DateOnly(date.Year, date.Month, 1);
        var point = points.SingleOrDefault(entity => entity.MonthStart == monthStart);
        if (point is not null)
        {
            apply(point);
        }
    }

    private static string BuildMonthLabel(DateOnly date)
    {
        var label = SpanishCulture.DateTimeFormat.GetAbbreviatedMonthName(date.Month).TrimEnd('.');
        return char.ToUpperInvariant(label[0]) + label[1..];
    }

    private static string FormatDueText(DateOnly dueDate, DateOnly today)
    {
        var delta = dueDate.DayNumber - today.DayNumber;
        return delta switch
        {
            < 0 => $"Atrasada {Math.Abs(delta)} d",
            0 => "Hoy",
            1 => "Mañana",
            _ => $"En {delta} días"
        };
    }

    private sealed class MonthlyChartPoint(DateOnly monthStart, string monthLabel)
    {
        public DateOnly MonthStart { get; } = monthStart;

        public string MonthLabel { get; } = monthLabel;

        public int Registrations { get; set; }

        public int Discharges { get; set; }

        public int Births { get; set; }

        public int Movements { get; set; }

        public int Total => Registrations + Discharges + Births + Movements;
    }
}
