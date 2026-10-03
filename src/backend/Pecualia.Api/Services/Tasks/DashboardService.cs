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
    internal const int MovementConfirmationGraceDays = 10;
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    public async Task<DashboardSummaryResponse> GetSummaryAsync(long userId, UserRole role, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow.UtcDateTime;
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
            .Include(entity => entity.PorcineTransitionDecision)
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

        var pendingMovementConfirmations = await dbContext.MovementCertificates
            .AsNoTracking()
            .Where(entity =>
                entity.DestinationLivestockId != null &&
                accessibleFarmIds.Contains(entity.DestinationLivestockId.Value) &&
                entity.Status == MovementStatus.Pending &&
                entity.ArrivalDate != null &&
                entity.ArrivalDate.Value <= now &&
                entity.ArrivalDate.Value.AddDays(MovementConfirmationGraceDays) >= now)
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

        foreach (var movement in movements.Where(entity => ToDateOnly(entity.DepartureDate) >= chartStartMonth && ToDateOnly(entity.DepartureDate) <= today))
        {
            Increment(chartPoints, ToDateOnly(movement.DepartureDate), point => point.Movements += movement.NumberOfAnimals);
        }

        var farmNamesById = farms.ToDictionary(entity => entity.Id, entity => entity.Name);
        var farmSpeciesById = farms.ToDictionary(entity => entity.Id, entity => entity.LivestockSpecies);
        var animalFarmNames = animals.ToDictionary(
            entity => entity.Id,
            entity => farmNamesById.GetValueOrDefault(entity.LivestockFarmId, "Explotación"));
        var pendingPorcineTransitionBirths = births
            .Where(entity =>
                entity.PorcineTransitionDecision is null &&
                farmSpeciesById.GetValueOrDefault(entity.LivestockFarmId) == LivestockSpecies.Porcine &&
                PorcineTransitionSupport.GetDecisionDate(entity.BirthDate) <= today)
            .ToList();
        var pendingPorcineTransitionBirthIds = pendingPorcineTransitionBirths.Select(entity => entity.Id).ToArray();
        var consumedPendingBirthCounts = pendingPorcineTransitionBirths.Count == 0
            ? new Dictionary<long, int>()
            : await dbContext.Animals
                .AsNoTracking()
                .Where(entity =>
                    entity.SourceBirthId != null &&
                    pendingPorcineTransitionBirthIds.Contains(entity.SourceBirthId.Value) &&
                    entity.RegistrationDate != null &&
                    entity.RegistrationDate <= today)
                .GroupBy(entity => entity.SourceBirthId!.Value)
                .Select(entity => new { BirthId = entity.Key, Count = entity.Count() })
                .ToDictionaryAsync(entity => entity.BirthId, entity => entity.Count, cancellationToken);

        var pendingTasks = BuildPendingTasks(
            vaccinations,
            inspections,
            pendingMovementConfirmations,
            pendingPorcineTransitionBirths,
            consumedPendingBirthCounts,
            animalFarmNames,
            farmNamesById,
            today);
        var previousMonth = monthStart.AddMonths(-1);

        return new DashboardSummaryResponse(
            role.ToString(),
            Farms: farms.Count,
            ActiveFarms: farms.Count,
            TotalAnimals: animals.Count,
            ManagedFarmers: role == UserRole.Manager
                ? await dbContext.Farmers.CountAsync(entity => entity.ManagerId == userId, cancellationToken)
                : 0,
            MovementsThisMonth: movements
                .Where(entity => ToDateOnly(entity.DepartureDate) >= monthStart && ToDateOnly(entity.DepartureDate) <= today)
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

    private static DateOnly ToDateOnly(DateTime value)
    {
        return DateOnly.FromDateTime(value.Date);
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

    internal static List<DashboardTaskResponse> BuildPendingTasks(
        IReadOnlyCollection<Vaccination> vaccinations,
        IReadOnlyCollection<Inspection> inspections,
        IReadOnlyCollection<MovementCertificate> pendingMovementConfirmations,
        IReadOnlyCollection<AnimalBirth> pendingPorcineTransitionBirths,
        IReadOnlyDictionary<long, int> consumedPendingBirthCounts,
        IReadOnlyDictionary<long, string> animalFarmNames,
        IReadOnlyDictionary<long, string> farmNamesById,
        DateOnly today)
    {
        var tasks = new List<DashboardTaskResponse>();
        var nextTwoWeeks = today.AddDays(14);

        tasks.AddRange(vaccinations
            .Where(entity => entity.NextDose is not null && entity.NextDose.Value <= nextTwoWeeks)
            .OrderBy(entity => entity.NextDose)
            .Take(3)
            .Select(entity => new DashboardTaskResponse(
                "Vaccination",
                $"Vacunación {entity.VaccinationType.ToLowerInvariant()} pendiente",
                $"{animalFarmNames.GetValueOrDefault(entity.AnimalId, "Explotación")} · {FormatDueText(entity.NextDose!.Value, today)}",
                entity.NextDose <= today ? "danger" : "warning",
                entity.NextDose!.Value)));

        tasks.AddRange(inspections
            .Where(entity => entity.InspectionDate >= today && entity.InspectionDate <= today.AddDays(21))
            .OrderBy(entity => entity.InspectionDate)
            .Take(3)
            .Select(entity => new DashboardTaskResponse(
                "Inspection",
                string.IsNullOrWhiteSpace(entity.Reason) ? "Inspección programada" : entity.Reason!,
                $"{entity.LivestockFarm.Name} · {FormatDueText(entity.InspectionDate, today)}",
                "info",
                entity.InspectionDate)));

        tasks.AddRange(BuildPendingMovementConfirmationTasks(pendingMovementConfirmations, farmNamesById, today));
        tasks.AddRange(BuildPendingPorcineTransitionTasks(pendingPorcineTransitionBirths, consumedPendingBirthCounts, farmNamesById, today));

        return tasks
            .OrderBy(entity => entity.DueDate)
            .ThenBy(entity => entity.Title)
            .Take(5)
            .ToList();
    }

    internal static IReadOnlyList<DashboardTaskResponse> BuildPendingMovementConfirmationTasks(
        IReadOnlyCollection<MovementCertificate> pendingMovementConfirmations,
        IReadOnlyDictionary<long, string> farmNamesById,
        DateOnly today)
    {
        return pendingMovementConfirmations
            .OrderBy(entity => entity.ArrivalDate)
            .Take(5)
            .Select(entity =>
            {
                var confirmationDeadline = DateOnly.FromDateTime(entity.ArrivalDate!.Value.AddDays(MovementConfirmationGraceDays));
                var farmName = farmNamesById.GetValueOrDefault(entity.DestinationLivestockId!.Value, "Explotación");
                var movementCode = string.IsNullOrWhiteSpace(entity.CodRemo)
                    ? (string.IsNullOrWhiteSpace(entity.Serie) ? $"Guía #{entity.Id}" : $"Serie {entity.Serie}")
                    : $"Guía {entity.CodRemo}";

                return new DashboardTaskResponse(
                    "MovementConfirmation",
                    "Confirmar guía pendiente",
                    $"{farmName} · {movementCode} · {FormatDueText(confirmationDeadline, today)}",
                    confirmationDeadline <= today ? "danger" : "warning",
                    confirmationDeadline);
            })
            .ToList();
    }

    internal static IReadOnlyList<DashboardTaskResponse> BuildPendingPorcineTransitionTasks(
        IReadOnlyCollection<AnimalBirth> pendingPorcineTransitionBirths,
        IReadOnlyDictionary<long, int> consumedPendingBirthCounts,
        IReadOnlyDictionary<long, string> farmNamesById,
        DateOnly today)
    {
        return pendingPorcineTransitionBirths
            .Select(entity => new
            {
                Birth = entity,
                Pending = Math.Max(0, entity.OffspringNumber - consumedPendingBirthCounts.GetValueOrDefault(entity.Id))
            })
            .Where(entity => entity.Pending > 0)
            .OrderBy(entity => entity.Birth.BirthDate)
            .Take(5)
            .Select(entity =>
            {
                var finalDate = PorcineTransitionSupport.GetFinalTransitionDate(entity.Birth.BirthDate);
                return new DashboardTaskResponse(
                    "PorcineTransition",
                    "Reclasificación porcina pendiente",
                    $"{farmNamesById.GetValueOrDefault(entity.Birth.LivestockFarmId, "Explotación")} · {entity.Pending} animales · {FormatDueText(finalDate, today)}",
                    finalDate <= today ? "danger" : "warning",
                    PorcineTransitionSupport.GetDecisionDate(entity.Birth.BirthDate));
            })
            .ToList();
    }

    internal static bool IsPendingMovementConfirmationVisible(MovementCertificate movement, DateTime now)
    {
        return movement.Status == MovementStatus.Pending &&
            movement.ArrivalDate is not null &&
            movement.ArrivalDate.Value <= now &&
            movement.ArrivalDate.Value.AddDays(MovementConfirmationGraceDays) >= now;
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

    internal static string FormatDueText(DateOnly dueDate, DateOnly today)
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
