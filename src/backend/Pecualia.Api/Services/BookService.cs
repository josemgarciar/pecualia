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
            .Where(entity => entity.LivestockFarmId == farm.Id)
            .OrderBy(entity => entity.BalanceDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);
        await AttachBalanceDetailsAsync(farm, balances, cancellationToken);
        await NormalizeBalanceDetailsForBookAsync(farm, balances, cancellationToken);
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
            .Include(entity => entity.OriginFarm)
            .Include(entity => entity.DestinationFarm)
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

    private async Task AttachBalanceDetailsAsync(LivestockFarm farm, IReadOnlyList<Balance> balances, CancellationToken cancellationToken)
    {
        if (balances.Count == 0)
        {
            return;
        }

        var balanceIds = balances.Select(entity => entity.Id).ToArray();
        if (farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            var porcineByBalanceId = await dbContext.BalancePorcino
                .AsNoTracking()
                .Where(entity => balanceIds.Contains(entity.BalanceId))
                .ToDictionaryAsync(entity => entity.BalanceId, cancellationToken);

            foreach (var balance in balances)
            {
                balance.Porcino = porcineByBalanceId.GetValueOrDefault(balance.Id);
            }

            return;
        }

        var ovineByBalanceId = await dbContext.BalanceOvinoCaprino
            .AsNoTracking()
            .Where(entity => balanceIds.Contains(entity.BalanceId))
            .ToDictionaryAsync(entity => entity.BalanceId, cancellationToken);

        foreach (var balance in balances)
        {
            balance.OvinoCaprino = ovineByBalanceId.GetValueOrDefault(balance.Id);
        }
    }

    private async Task NormalizeBalanceDetailsForBookAsync(LivestockFarm farm, IReadOnlyList<Balance> balances, CancellationToken cancellationToken)
    {
        if (farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            return;
        }

        var deathBalances = balances
            .Where(entity => entity.ModificationCause.Equals(AnimalDischargeCause.Muerte.ToString(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(entity => entity.BalanceDate)
            .ThenBy(entity => entity.Id)
            .ToList();

        if (deathBalances.Count == 0)
        {
            return;
        }

        var dischargedAnimals = await dbContext.Animals
            .AsNoTracking()
            .Where(entity =>
                entity.LivestockFarmId == farm.Id &&
                entity.DischargeCause == AnimalDischargeCause.Muerte &&
                entity.DischargeDate != null)
            .OrderBy(entity => entity.DischargeDate)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        var animalsByDate = dischargedAnimals
            .GroupBy(entity => entity.DischargeDate!.Value)
            .ToDictionary(entity => entity.Key, entity => new Queue<Animal>(entity));

        foreach (var balance in deathBalances)
        {
            if (!animalsByDate.TryGetValue(balance.BalanceDate, out var queue) || queue.Count == 0)
            {
                continue;
            }

            var animal = queue.Dequeue();
            balance.OvinoCaprino ??= new BalanceOvinoCaprino
            {
                BalanceId = balance.Id
            };

            var bucket = ResolveOvineBookDeathBucket(animal, balance.BalanceDate);
            balance.OvinoCaprino.NonReproductiveUnder4Months = bucket == "under4" ? balance.NumberOfAnimals : 0;
            balance.OvinoCaprino.NonReproductiveBetween4And12Months = bucket == "from4to12" ? balance.NumberOfAnimals : 0;
            balance.OvinoCaprino.ReproductiveFemales = bucket == "female" ? balance.NumberOfAnimals : 0;
            balance.OvinoCaprino.ReproductiveMales = bucket == "male" ? balance.NumberOfAnimals : 0;
        }
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
                    var storedDelta = ClonePorcineBalanceDetail(balance.Porcino);
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

                    ApplyReversePorcineDelta(balance, state, storedDelta);
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
                var storedDelta = CloneOvineBalanceDetail(balance.OvinoCaprino);
                balance.OvinoCaprino ??= new BalanceOvinoCaprino
                {
                    BalanceId = balance.Id
                };

                balance.OvinoCaprino.NonReproductiveUnder4Months = ovineState.NonReproductiveUnder4Months;
                balance.OvinoCaprino.NonReproductiveBetween4And12Months = ovineState.NonReproductiveBetween4And12Months;
                balance.OvinoCaprino.ReproductiveFemales = ovineState.ReproductiveFemales;
                balance.OvinoCaprino.ReproductiveMales = ovineState.ReproductiveMales;

                ApplyReverseOvineDelta(balance, ovineState, storedDelta);
            }
        }
    }

    private static void ApplyReverseOvineDelta(Balance balance, OvineBalanceState state, BalanceOvinoCaprino? storedDelta)
    {
        var useStoredDelta = storedDelta is not null && GetOvineDeltaTotal(storedDelta) > 0 && GetOvineDeltaTotal(storedDelta) <= balance.NumberOfAnimals;
        var deltaUnder4 = useStoredDelta
            ? storedDelta!.NonReproductiveUnder4Months
            : (IsBirthCause(balance.ModificationCause) ? balance.NumberOfAnimals : 0);
        var deltaFrom4To12 = useStoredDelta ? storedDelta!.NonReproductiveBetween4And12Months : 0;
        var deltaFemales = useStoredDelta ? storedDelta!.ReproductiveFemales : 0;
        var deltaMales = useStoredDelta ? storedDelta!.ReproductiveMales : 0;

        if (IsNegativeCause(balance.ModificationCause))
        {
            state.NonReproductiveUnder4Months += deltaUnder4;
            state.NonReproductiveBetween4And12Months += deltaFrom4To12;
            state.ReproductiveFemales += deltaFemales;
            state.ReproductiveMales += deltaMales;
            return;
        }

        if (IsAutorrepositionCause(balance.ModificationCause))
        {
            state.NonReproductiveUnder4Months -= deltaUnder4;
            state.NonReproductiveBetween4And12Months += balance.NumberOfAnimals - deltaUnder4;
            state.ReproductiveFemales -= deltaFemales;
            state.ReproductiveMales -= deltaMales;
            return;
        }

        state.NonReproductiveUnder4Months -= deltaUnder4;
        state.NonReproductiveBetween4And12Months -= deltaFrom4To12;
        state.ReproductiveFemales -= deltaFemales;
        state.ReproductiveMales -= deltaMales;
    }

    private static void ApplyReversePorcineDelta(Balance balance, PorcineBalanceState state, BalancePorcino? storedDelta)
    {
        var useStoredDelta = storedDelta is not null && GetPorcineDeltaTotal(storedDelta) > 0 && GetPorcineDeltaTotal(storedDelta) <= balance.NumberOfAnimals;
        var deltaBoars = useStoredDelta ? storedDelta!.Boars : 0;
        var deltaSowsForLive = useStoredDelta ? storedDelta!.SowsForLive : 0;
        var deltaSowsReposition = useStoredDelta ? storedDelta!.SowsReposition : 0;
        var deltaPigsReposition = useStoredDelta ? storedDelta!.PigsReposition : 0;
        var deltaPiglets = useStoredDelta ? storedDelta!.Piglets : (IsBirthCause(balance.ModificationCause) ? balance.NumberOfAnimals : 0);
        var deltaRears = useStoredDelta ? storedDelta!.Rear : 0;
        var deltaBaits = useStoredDelta ? storedDelta!.Baits : 0;

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

        if (IsAutorrepositionCause(balance.ModificationCause))
        {
            if (useStoredDelta && deltaPiglets > 0)
            {
                state.Boars -= deltaBoars;
                state.SowsForLive -= deltaSowsForLive;
                state.SowsReposition -= deltaSowsReposition;
                state.PigsReposition -= deltaPigsReposition;
                state.Piglets += deltaPiglets;
                state.Rears -= deltaRears;
                state.Baits -= deltaBaits;
                return;
            }

            state.Boars -= deltaBoars;
            state.SowsForLive -= deltaSowsForLive;
            state.SowsReposition -= deltaSowsReposition;
            state.PigsReposition -= deltaPigsReposition;
            state.Piglets -= deltaPiglets;
            state.Rears += balance.NumberOfAnimals - deltaPiglets;
            state.Baits -= deltaBaits;
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

    private static BalanceOvinoCaprino? CloneOvineBalanceDetail(BalanceOvinoCaprino? detail)
    {
        if (detail is null)
        {
            return null;
        }

        return new BalanceOvinoCaprino
        {
            BalanceId = detail.BalanceId,
            NonReproductiveUnder4Months = detail.NonReproductiveUnder4Months,
            NonReproductiveBetween4And12Months = detail.NonReproductiveBetween4And12Months,
            ReproductiveFemales = detail.ReproductiveFemales,
            ReproductiveMales = detail.ReproductiveMales,
            TransporterName = detail.TransporterName,
            TransportTicketNumber = detail.TransportTicketNumber
        };
    }

    private static BalancePorcino? ClonePorcineBalanceDetail(BalancePorcino? detail)
    {
        if (detail is null)
        {
            return null;
        }

        return new BalancePorcino
        {
            BalanceId = detail.BalanceId,
            Boars = detail.Boars,
            SowsForLive = detail.SowsForLive,
            SowsReposition = detail.SowsReposition,
            PigsReposition = detail.PigsReposition,
            Piglets = detail.Piglets,
            Rear = detail.Rear,
            Baits = detail.Baits,
            Type = detail.Type,
            Breed = detail.Breed,
            Tag = detail.Tag
        };
    }

    private static bool IsBirthCause(string cause) => cause.Equals("Nacimiento", StringComparison.OrdinalIgnoreCase);

    private static bool IsAutorrepositionCause(string cause) =>
        cause.Equals("Autorreposicion", StringComparison.OrdinalIgnoreCase) ||
        cause.Equals("Autorreposición", StringComparison.OrdinalIgnoreCase);

    private static bool IsNegativeCause(string cause) =>
        cause.Equals("Salida", StringComparison.OrdinalIgnoreCase) ||
        cause.Equals("Muerte", StringComparison.OrdinalIgnoreCase);

    private static string ResolveOvineBookDeathBucket(Animal animal, DateOnly asOfDate)
    {
        if (animal.RegistrationCause == AnimalRegistrationCause.Autorreposicion)
        {
            var normalizedSex = FarmCensusProjectionSupport.NormalizeSex(animal.Sex);
            if (normalizedSex == "female")
            {
                return "female";
            }

            if (normalizedSex == "male")
            {
                return "male";
            }

            return "from4to12";
        }

        var birthDate = FarmCensusProjectionSupport.ResolveBirthDate(animal);
        if (birthDate is not null && FarmCensusProjectionSupport.IsYoungerThanMonths(birthDate.Value, asOfDate, 4))
        {
            return "under4";
        }

        if (birthDate is not null && FarmCensusProjectionSupport.IsYoungerThanMonths(birthDate.Value, asOfDate, 12))
        {
            return "from4to12";
        }

        var normalizedSexFallback = FarmCensusProjectionSupport.NormalizeSex(animal.Sex);
        return normalizedSexFallback == "male" ? "male" : "female";
    }

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
