using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Books;
using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;
using QuestPDF.Fluent;

namespace Pecualia.Api.Services;

public interface IBookService
{
    Task<FarmBookPreviewResponse> GetPreviewAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken);

    Task<FarmBookPdfFile> GeneratePdfAsync(
        long userId,
        UserRole role,
        long farmId,
        IReadOnlyCollection<string>? sectionIds,
        CancellationToken cancellationToken);
}

public sealed record FarmBookPdfFile(string FileName, byte[] Content, string ContentType);

public sealed class BookService(PecualiaDbContext dbContext, IFarmCensusProjectionService censusProjectionService) : IBookService
{
    public async Task<FarmBookPreviewResponse> GetPreviewAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        var aggregate = await LoadAggregateAsync(userId, role, farmId, cancellationToken);

        return new FarmBookPreviewResponse(
            aggregate.Farm.Id,
            aggregate.Farm.Name,
            aggregate.Farm.RegaCode,
            aggregate.Farm.LivestockSpecies.ToString(),
            BookDocumentSupport.IsOvineOrCaprine(aggregate.Farm) ? "official-ovino-caprino" : "official-porcino",
            new FarmBookPreviewSummaryResponse(
                BookDocumentSupport.BuildFarmerName(aggregate.Farm.Farmer),
                aggregate.Farm.Farmer.NifCif,
                BookDocumentSupport.EmptyToNull(aggregate.Farm.Town),
                BookDocumentSupport.EmptyToNull(aggregate.Farm.Province),
                aggregate.Animals.Count,
                aggregate.Balances.Count,
                aggregate.Censuses.Count,
                aggregate.Incidents.Count,
                aggregate.Inspections.Count),
            BookDocumentComposer.BuildSections(aggregate));
    }

    public async Task<FarmBookPdfFile> GeneratePdfAsync(
        long userId,
        UserRole role,
        long farmId,
        IReadOnlyCollection<string>? sectionIds,
        CancellationToken cancellationToken)
    {
        var aggregate = await LoadAggregateAsync(userId, role, farmId, cancellationToken);
        var includedSections = BookDocumentComposer.ResolveIncludedSections(sectionIds);
        var content = Document.Create(container => BookDocumentComposer.ComposeDocument(container, aggregate, includedSections)).GeneratePdf();
        var fileName = $"libro-registro-{aggregate.Farm.RegaCode.ToLowerInvariant()}.pdf";
        return new FarmBookPdfFile(fileName, content, "application/pdf");
    }

    private async Task<BookAggregate> LoadAggregateAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        var farm = await BuildAccessibleFarmQuery(userId, role)
            .AsNoTracking()
            .Include(entity => entity.Farmer)
            .ThenInclude(entity => entity.User)
            .SingleOrDefaultAsync(entity => entity.Id == farmId, cancellationToken);

        if (farm is null)
        {
            throw new DomainException("Explotación no encontrada.");
        }

        var animals = await dbContext.Animals
            .AsNoTracking()
            .Include(entity => entity.OvinoCaprino)
            .Include(entity => entity.Porcino)
            .Where(entity => entity.LivestockFarmId == farm.Id)
            .OrderBy(entity => entity.RegistrationDate)
            .ThenBy(entity => entity.Identification)
            .ToListAsync(cancellationToken);

        var balances = await dbContext.Balances
            .AsNoTracking()
            .Include(entity => entity.OvinoCaprino)
            .Include(entity => entity.Porcino)
            .Where(entity => entity.LivestockFarmId == farm.Id)
            .OrderBy(entity => entity.BalanceDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);
        await EnrichBalancesForBookAsync(farm, balances, cancellationToken);

        var censuses = await censusProjectionService.BuildBookCensusesAsync(farm, cancellationToken);

        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .Include(entity => entity.Animal)
            .Where(entity => entity.LivestockFarmId == farm.Id)
            .OrderBy(entity => entity.IncidentDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        var inspections = await dbContext.Inspections
            .AsNoTracking()
            .Where(entity => entity.LivestockFarmId == farm.Id)
            .OrderBy(entity => entity.InspectionDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        var movements = await dbContext.MovementCertificates
            .AsNoTracking()
            .Where(entity => entity.OriginLivestockId == farm.Id || entity.DestinationLivestockId == farm.Id)
            .OrderBy(entity => entity.DepartureDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        var animalIds = animals.Select(animal => animal.Id).ToArray();
        var guideSeriesByAnimalId = await dbContext.MovementCertificateAnimals
            .AsNoTracking()
            .Where(entity => animalIds.Contains(entity.AnimalId))
            .Select(entity => new
            {
                entity.AnimalId,
                entity.MovementCertificate.Serie,
                entity.MovementCertificate.DepartureDate,
                IsEntry = entity.MovementCertificate.DestinationLivestockId == farm.Id,
                IsExit = entity.MovementCertificate.OriginLivestockId == farm.Id
            })
            .ToListAsync(cancellationToken);

        var guideSeriesLookup = guideSeriesByAnimalId
            .GroupBy(entity => entity.AnimalId)
            .ToDictionary(
                entity => entity.Key,
                entity => new BookAnimalGuideSeries(
                    entity
                        .Where(item => item.IsEntry && !string.IsNullOrWhiteSpace(item.Serie))
                        .OrderByDescending(item => item.DepartureDate)
                        .Select(item => item.Serie)
                        .FirstOrDefault(),
                    entity
                        .Where(item => item.IsExit && !string.IsNullOrWhiteSpace(item.Serie))
                        .OrderByDescending(item => item.DepartureDate)
                        .Select(item => item.Serie)
                        .FirstOrDefault()));

        return new BookAggregate(farm, animals, balances, censuses, incidents, inspections, movements, guideSeriesLookup);
    }

    private async Task EnrichBalancesForBookAsync(LivestockFarm farm, IReadOnlyList<Balance> balances, CancellationToken cancellationToken)
    {
        var groups = balances
            .GroupBy(entity => entity.BalanceDate)
            .OrderBy(entity => entity.Key);

        foreach (var group in groups)
        {
            var snapshot = await censusProjectionService.BuildSnapshotAsync(farm, group.Key, cancellationToken);
            if (farm.LivestockSpecies == LivestockSpecies.Porcine)
            {
                var state = new PorcineBalanceState(
                    snapshot.Boars,
                    snapshot.SowsForLive,
                    snapshot.SowsReposition,
                    snapshot.MalesReposition,
                    snapshot.Piglets,
                    snapshot.Rears,
                    snapshot.Baits);

                foreach (var balance in group.OrderByDescending(entity => entity.Id))
                {
                    balance.Porcino ??= new BalancePorcino
                    {
                        BalanceId = balance.Id
                    };

                    balance.Porcino.Boars = state.Boars;
                    balance.Porcino.SowsForLive = state.SowsForLive;
                    balance.Porcino.SowsReposition = state.SowsReposition;
                    balance.Porcino.PigsReposition = state.PigsReposition;
                    balance.Porcino.Piglets = state.Piglets;
                    balance.Porcino.Rear = state.Rears;
                    balance.Porcino.Baits = state.Baits;

                    ApplyReversePorcineDelta(balance, state);
                }

                continue;
            }

            var ovineState = new OvineBalanceState(
                snapshot.NonReproductiveUnder4Months,
                snapshot.NonReproductiveBetween4And12Months,
                snapshot.ReproductiveFemales,
                snapshot.ReproductiveMales);

            foreach (var balance in group.OrderByDescending(entity => entity.Id))
            {
                balance.OvinoCaprino ??= new BalanceOvinoCaprino
                {
                    BalanceId = balance.Id
                };

                balance.OvinoCaprino.NonReproductiveUnder4Months = ovineState.NonReproductiveUnder4Months;
                balance.OvinoCaprino.NonReproductiveBetween4And12Months = ovineState.NonReproductiveBetween4And12Months;
                balance.OvinoCaprino.ReproductiveFemales = ovineState.ReproductiveFemales;
                balance.OvinoCaprino.ReproductiveMales = ovineState.ReproductiveMales;

                ApplyReverseOvineDelta(balance, ovineState);
            }
        }
    }

    private static void ApplyReverseOvineDelta(Balance balance, OvineBalanceState state)
    {
        var detail = balance.OvinoCaprino;
        var useStoredDelta = detail is not null && GetOvineDeltaTotal(detail) > 0 && GetOvineDeltaTotal(detail) <= balance.NumberOfAnimals;
        var deltaUnder4 = useStoredDelta
            ? detail!.NonReproductiveUnder4Months
            : (IsBirthCause(balance.ModificationCause) ? balance.NumberOfAnimals : 0);
        var deltaFrom4To12 = useStoredDelta ? detail!.NonReproductiveBetween4And12Months : 0;
        var deltaFemales = useStoredDelta ? detail!.ReproductiveFemales : 0;
        var deltaMales = useStoredDelta ? detail!.ReproductiveMales : 0;

        if (IsNegativeCause(balance.ModificationCause))
        {
            state.NonReproductiveUnder4Months += deltaUnder4;
            state.NonReproductiveBetween4And12Months += deltaFrom4To12;
            state.ReproductiveFemales += deltaFemales;
            state.ReproductiveMales += deltaMales;
            return;
        }

        state.NonReproductiveUnder4Months -= deltaUnder4;
        state.NonReproductiveBetween4And12Months -= deltaFrom4To12;
        state.ReproductiveFemales -= deltaFemales;
        state.ReproductiveMales -= deltaMales;
    }

    private static void ApplyReversePorcineDelta(Balance balance, PorcineBalanceState state)
    {
        var detail = balance.Porcino;
        var useStoredDelta = detail is not null && GetPorcineDeltaTotal(detail) > 0 && GetPorcineDeltaTotal(detail) <= balance.NumberOfAnimals;
        var deltaBoars = useStoredDelta ? detail!.Boars : 0;
        var deltaSowsForLive = useStoredDelta ? detail!.SowsForLive : 0;
        var deltaSowsReposition = useStoredDelta ? detail!.SowsReposition : 0;
        var deltaPigsReposition = useStoredDelta ? detail!.PigsReposition : 0;
        var deltaPiglets = useStoredDelta ? detail!.Piglets : (IsBirthCause(balance.ModificationCause) ? balance.NumberOfAnimals : 0);
        var deltaRears = useStoredDelta ? detail!.Rear : 0;
        var deltaBaits = useStoredDelta ? detail!.Baits : 0;

        if (IsNegativeCause(balance.ModificationCause))
        {
            state.Boars += deltaBoars;
            state.SowsForLive += deltaSowsForLive;
            state.SowsReposition += deltaSowsReposition;
            state.PigsReposition += deltaPigsReposition;
            state.Piglets += deltaPiglets;
            state.Rears += deltaRears;
            state.Baits += deltaBaits;
            return;
        }

        state.Boars -= deltaBoars;
        state.SowsForLive -= deltaSowsForLive;
        state.SowsReposition -= deltaSowsReposition;
        state.PigsReposition -= deltaPigsReposition;
        state.Piglets -= deltaPiglets;
        state.Rears -= deltaRears;
        state.Baits -= deltaBaits;
    }

    private static bool IsBirthCause(string cause) => cause.Equals("Nacimiento", StringComparison.OrdinalIgnoreCase);

    private static bool IsNegativeCause(string cause) =>
        cause.Equals("Salida", StringComparison.OrdinalIgnoreCase) ||
        cause.Equals("Muerte", StringComparison.OrdinalIgnoreCase);

    private sealed class OvineBalanceState(
        int nonReproductiveUnder4Months,
        int nonReproductiveBetween4And12Months,
        int reproductiveFemales,
        int reproductiveMales)
    {
        public int NonReproductiveUnder4Months { get; set; } = nonReproductiveUnder4Months;
        public int NonReproductiveBetween4And12Months { get; set; } = nonReproductiveBetween4And12Months;
        public int ReproductiveFemales { get; set; } = reproductiveFemales;
        public int ReproductiveMales { get; set; } = reproductiveMales;
    }

    private static int GetOvineDeltaTotal(BalanceOvinoCaprino detail) =>
        detail.NonReproductiveUnder4Months +
        detail.NonReproductiveBetween4And12Months +
        detail.ReproductiveFemales +
        detail.ReproductiveMales;

    private static int GetPorcineDeltaTotal(BalancePorcino detail) =>
        detail.Boars +
        detail.SowsForLive +
        detail.SowsReposition +
        detail.PigsReposition +
        detail.Piglets +
        detail.Rear +
        detail.Baits;

    private sealed class PorcineBalanceState(
        int boars,
        int sowsForLive,
        int sowsReposition,
        int pigsReposition,
        int piglets,
        int rears,
        int baits)
    {
        public int Boars { get; set; } = boars;
        public int SowsForLive { get; set; } = sowsForLive;
        public int SowsReposition { get; set; } = sowsReposition;
        public int PigsReposition { get; set; } = pigsReposition;
        public int Piglets { get; set; } = piglets;
        public int Rears { get; set; } = rears;
        public int Baits { get; set; } = baits;
    }

    private IQueryable<LivestockFarm> BuildAccessibleFarmQuery(long userId, UserRole role)
    {
        return role == UserRole.Manager
            ? dbContext.Farms.Where(entity => entity.Farmer.ManagerId == userId)
            : dbContext.Farms.Where(entity => entity.FarmerId == userId);
    }
}
