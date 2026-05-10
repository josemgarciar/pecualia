using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Movements;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

public interface IMovementService
{
    IReadOnlyList<MovementBreedOptionResponse> GetBreedOptions(LivestockSpecies species);

    Task<IReadOnlyList<FarmMovementListItemResponse>> GetFarmMovementsAsync(
        long userId,
        UserRole role,
        long farmId,
        CancellationToken cancellationToken);

    Task<MovementDetailResponse> GetMovementAsync(long userId, UserRole role, long movementId, CancellationToken cancellationToken);

    Task<ConfirmMovementResponse> ConfirmMovementAsync(long userId, UserRole role, long movementId, CancellationToken cancellationToken);

    Task<MovementDetailResponse> CreateManualMovementAsync(
        long userId,
        UserRole role,
        CreateManualMovementRequest request,
        CancellationToken cancellationToken);

    Task<MovementImportPreviewResponse> PreviewImportAsync(
        long userId,
        UserRole role,
        PreviewMovementImportRequest request,
        CancellationToken cancellationToken);

    Task<MovementImportCommitResponse> CommitImportAsync(
        long userId,
        UserRole role,
        CommitMovementImportRequest request,
        CancellationToken cancellationToken);
}

public sealed class MovementService(PecualiaDbContext dbContext) : IMovementService
{
    private static readonly Regex SpanishOfficialIdentificationRegex = new("^ES\\d{12}$", RegexOptions.Compiled);
    private static readonly Regex OvineOrCaprineLegacyIdentificationRegex = new("^ES\\d{12}-[A-Z0-9]{3,}$", RegexOptions.Compiled);
    private static readonly Regex PorcineAlternativeIdentificationRegex = new("^GT\\d+$", RegexOptions.Compiled);
    private static readonly Regex SpanishOfficialIdentificationFinderRegex = new("ES[\\s._-]*(?:\\d[\\s._-]*){12}(?:-[A-Z0-9]{3,})?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PorcineAlternativeIdentificationFinderRegex = new("\\bGT[\\s._-]*\\d+\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IReadOnlyList<MovementBreedOptionResponse> GetBreedOptions(LivestockSpecies species)
    {
        return BookDocumentSupport.GetBreedCodes(species)
            .Select(entity => new MovementBreedOptionResponse(entity.Key, entity.Value))
            .ToList();
    }

    public async Task<IReadOnlyList<FarmMovementListItemResponse>> GetFarmMovementsAsync(
        long userId,
        UserRole role,
        long farmId,
        CancellationToken cancellationToken)
    {
        await EnsureAccessibleFarmAsync(userId, role, farmId, cancellationToken);

        var movements = await BuildAccessibleMovementQuery(userId, role)
            .AsNoTracking()
            .Include(entity => entity.OriginFarm)
            .Include(entity => entity.DestinationFarm)
            .Where(entity => entity.OriginLivestockId == farmId || entity.DestinationLivestockId == farmId)
            .OrderByDescending(entity => entity.DepartureDate)
            .ThenByDescending(entity => entity.Id)
            .ToListAsync(cancellationToken);

        return movements
            .Select(entity => MapFarmMovementListItem(entity, farmId))
            .ToList();
    }

    public async Task<MovementDetailResponse> GetMovementAsync(long userId, UserRole role, long movementId, CancellationToken cancellationToken)
    {
        var movement = await BuildAccessibleMovementQuery(userId, role)
            .AsNoTracking()
            .Include(entity => entity.OriginFarm)
            .Include(entity => entity.DestinationFarm)
            .Include(entity => entity.Animals)
            .ThenInclude(entity => entity.Animal)
            .SingleOrDefaultAsync(entity => entity.Id == movementId, cancellationToken);

        if (movement is null)
        {
            throw new DomainException("Movimiento no encontrado.");
        }

        return MapMovementDetail(movement);
    }

    public async Task<ConfirmMovementResponse> ConfirmMovementAsync(long userId, UserRole role, long movementId, CancellationToken cancellationToken)
    {
        var movement = await BuildAccessibleMovementQuery(userId, role)
            .SingleOrDefaultAsync(entity => entity.Id == movementId, cancellationToken);

        if (movement is null)
        {
            throw new DomainException("Movimiento no encontrado.");
        }

        if (movement.Status == MovementStatus.Confirmed)
        {
            throw new DomainException("La guía ya está confirmada.");
        }

        movement.Status = MovementStatus.Confirmed;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ConfirmMovementResponse(
            movement.Id,
            BuildMovementStatus(movement));
    }

    public async Task<MovementDetailResponse> CreateManualMovementAsync(
        long userId,
        UserRole role,
        CreateManualMovementRequest request,
        CancellationToken cancellationToken)
    {
        ValidateMovementTimeline(request.DepartureDate, request.ArrivalDate, request.SolicitationDate);

        var context = await PrepareContextAsync(
            userId,
            role,
            request.FarmId,
            request.Direction,
            request.CounterpartyType,
            request.CounterpartyFarmId,
            request.CounterpartyExternalCode,
            request.CounterpartyExternalName,
            request.CodRemo,
            request.Cause,
            cancellationToken);

        if (context.Direction == MovementDirection.Entry && context.CounterpartyType == MovementCounterpartyType.External)
        {
            var parsedRows = ParseLines(request.Identifications);
            if (parsedRows.Count == 0)
            {
                throw new DomainException("Debes indicar al menos una identificación para registrar una entrada externa.");
            }

            return await CreateExternalEntryMovementFromLinesAsync(
                context,
                request.Serie,
                request.DepartureDate,
                request.ArrivalDate,
                request.SolicitationDate,
                request.MeansOfTransport,
                request.TransportName,
                request.VehicleRegistrationNumber,
                request.HealthDocumentNumber,
                request.SharedAnimalData,
                parsedRows,
                cancellationToken);
        }

        var animalIds = request.AnimalIds?
            .Where(entity => entity > 0)
            .Distinct()
            .ToList() ?? [];

        if (animalIds.Count == 0)
        {
            throw new DomainException("Debes seleccionar al menos un animal para registrar el movimiento.");
        }

        return await CreateMovementFromExistingAnimalsAsync(
            context,
            request.Serie,
            request.DepartureDate,
            request.ArrivalDate,
            request.SolicitationDate,
            request.MeansOfTransport,
            request.TransportName,
            request.VehicleRegistrationNumber,
            request.HealthDocumentNumber,
            animalIds,
            cancellationToken);
    }

    public async Task<MovementImportPreviewResponse> PreviewImportAsync(
        long userId,
        UserRole role,
        PreviewMovementImportRequest request,
        CancellationToken cancellationToken)
    {
        ValidateMovementTimeline(request.DepartureDate, request.ArrivalDate, request.SolicitationDate);

        var context = await PrepareImportContextAsync(
            userId,
            role,
            request.FarmId,
            request.Operation,
            request.CounterpartyExternalCode,
            request.CounterpartyExternalName,
            request.CodRemo,
            request.Cause,
            cancellationToken);

        if (request.UnidentifiedAnimalCount is > 0)
        {
            ValidateUnidentifiedAnimalRequest(context, request.UnidentifiedAnimalCount.Value);
            var summary = new MovementImportPreviewSummaryResponse(0, 0, 0, 0, 0, 0, 0, 0);
            return new MovementImportPreviewResponse(context.Species.ToString(), false, [], summary);
        }

        if (string.IsNullOrWhiteSpace(request.RawText))
        {
            throw new DomainException("Debes subir o pegar un TXT con al menos una identificación.");
        }

        return await BuildPreviewAsync(context, request.RawText, cancellationToken);
    }

    public async Task<MovementImportCommitResponse> CommitImportAsync(
        long userId,
        UserRole role,
        CommitMovementImportRequest request,
        CancellationToken cancellationToken)
    {
        ValidateMovementTimeline(request.DepartureDate, request.ArrivalDate, request.SolicitationDate);

        var context = await PrepareImportContextAsync(
            userId,
            role,
            request.FarmId,
            request.Operation,
            request.CounterpartyExternalCode,
            request.CounterpartyExternalName,
            request.CodRemo,
            request.Cause,
            cancellationToken);

        if (request.UnidentifiedAnimalCount is > 0)
        {
            ValidateUnidentifiedAnimalRequest(context, request.UnidentifiedAnimalCount.Value);
            return await CommitUnidentifiedAnimalMovementAsync(
                context,
                request.Serie,
                request.DepartureDate,
                request.ArrivalDate,
                request.SolicitationDate,
                request.MeansOfTransport,
                request.TransportName,
                request.VehicleRegistrationNumber,
                request.HealthDocumentNumber,
                request.UnidentifiedAnimalCount.Value,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.RawText))
        {
            throw new DomainException("Debes subir o pegar un TXT con al menos una identificación.");
        }

        var preview = await BuildPreviewAsync(context, request.RawText, cancellationToken);
        var processableRows = preview.Rows
            .Where(entity => context.Direction == MovementDirection.Entry
                ? entity.Status == "not_found"
                : entity.Status == "valid")
            .ToList();

        if (processableRows.Count == 0)
        {
            throw new DomainException("El archivo no contiene identificaciones válidas para registrar el movimiento.");
        }

        if (preview.RequiresSharedAnimalData && preview.Rows.Any(entity => entity.Status == "not_found"))
        {
            ValidateSharedAnimalData(context, request.SharedAnimalData);
        }

        MovementDetailResponse movement;
        if (context.Direction == MovementDirection.Entry && context.CounterpartyType == MovementCounterpartyType.External)
        {
            movement = await CreateExternalEntryMovementFromRowsAsync(
            context,
            request.Serie,
            request.DepartureDate,
                request.ArrivalDate,
                request.SolicitationDate,
                request.MeansOfTransport,
                request.TransportName,
                request.VehicleRegistrationNumber,
                request.HealthDocumentNumber,
                request.SharedAnimalData,
                processableRows,
                cancellationToken);
        }
        else
        {
            var animalIds = processableRows
                .Where(entity => entity.AnimalId is not null)
                .Select(entity => entity.AnimalId!.Value)
                .Distinct()
                .ToList();

            movement = await CreateMovementFromExistingAnimalsAsync(
                context,
                request.Serie,
                request.DepartureDate,
                request.ArrivalDate,
                request.SolicitationDate,
                request.MeansOfTransport,
                request.TransportName,
                request.VehicleRegistrationNumber,
                request.HealthDocumentNumber,
                animalIds,
                cancellationToken);
        }

        var rejectedRows = preview.Rows.Count(entity => entity.Status is "duplicate" or "invalid_format" or "conflict" or "existing");

        return new MovementImportCommitResponse(
            movement.Id,
            movement.CodRemo,
            processableRows.Count,
            rejectedRows,
            preview.RequiresSharedAnimalData && preview.Rows.Any(entity => entity.Status == "not_found"),
            preview.Summary);
    }

    private async Task<MovementImportCommitResponse> CommitUnidentifiedAnimalMovementAsync(
        MovementContext context,
        string? serie,
        DateTime departureDate,
        DateTime? arrivalDate,
        DateTime? solicitationDate,
        string? meansOfTransport,
        string? transportName,
        string? vehicleRegistrationNumber,
        string? healthDocumentNumber,
        int unidentifiedAnimalCount,
        CancellationToken cancellationToken)
    {
        var movement = BuildMovementCertificate(
            context,
            serie,
            departureDate,
            arrivalDate,
            solicitationDate,
            meansOfTransport,
            transportName,
            vehicleRegistrationNumber,
            unidentifiedAnimalCount);

        dbContext.MovementCertificates.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);

        var summary = new MovementImportPreviewSummaryResponse(0, 0, 0, 0, 0, 0, 0, 0);

        return new MovementImportCommitResponse(
            movement.Id,
            movement.CodRemo,
            unidentifiedAnimalCount,
            0,
            false,
            summary);
    }

    private static void ValidateUnidentifiedAnimalRequest(MovementContext context, int count)
    {
        if (context.Species is not (LivestockSpecies.Ovine or LivestockSpecies.Caprine))
        {
            throw new DomainException("Solo se pueden registrar movimientos sin identificación para ganado ovino o caprino.");
        }

        if (count <= 0 || count > 10000)
        {
            throw new DomainException("El número de animales sin identificar debe estar entre 1 y 10.000.");
        }
    }

    private async Task<MovementImportPreviewResponse> BuildPreviewAsync(
        MovementContext context,
        string rawText,
        CancellationToken cancellationToken)
    {
        var parsedLines = ParseLines(rawText);
        if (parsedLines.Count == 0)
        {
            throw new DomainException("Debes subir o pegar un TXT con al menos una identificación.");
        }

        var deduplicated = new Dictionary<string, ParsedIdentificationLine>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<MovementImportPreviewRowResponse>(parsedLines.Count);

        foreach (var line in parsedLines)
        {
            var identification = NormalizeIdentification(line.Value);
            if (!IsIdentificationValid(context.Species, identification))
            {
                rows.Add(new MovementImportPreviewRowResponse(
                    line.LineNumber,
                    identification,
                    "invalid_format",
                    "Excluido",
                    BuildIdentificationFormatMessage(context.Species),
                    null,
                    null));
                continue;
            }

            if (deduplicated.TryGetValue(identification, out var firstOccurrence))
            {
                rows.Add(new MovementImportPreviewRowResponse(
                    line.LineNumber,
                    identification,
                    "duplicate",
                    "Excluido",
                    $"Identificación duplicada. Primera aparición en la línea {firstOccurrence.LineNumber}.",
                    null,
                    null));
                continue;
            }

            deduplicated[identification] = new ParsedIdentificationLine(line.LineNumber, identification);
        }

        var existingAnimals = await LoadAnimalsByIdentificationAsync(deduplicated.Keys.ToList(), cancellationToken);
        foreach (var parsed in deduplicated.Values.OrderBy(entity => entity.LineNumber))
        {
            existingAnimals.TryGetValue(parsed.Value, out var animal);
            rows.Add(BuildPreviewRow(context, parsed, animal));
        }

        rows = rows
            .OrderBy(entity => entity.LineNumber)
            .ToList();

        var summary = new MovementImportPreviewSummaryResponse(
            parsedLines.Count,
            deduplicated.Count,
            rows.Count(entity => entity.Status == "valid"),
            rows.Count(entity => entity.Status == "duplicate"),
            rows.Count(entity => entity.Status == "invalid_format"),
            rows.Count(entity => entity.Status == "existing"),
            rows.Count(entity => entity.Status == "not_found"),
            rows.Count(entity => entity.Status == "conflict"));

        return new MovementImportPreviewResponse(
            context.Species.ToString(),
            context.IsBulkImport && context.Direction == MovementDirection.Entry && rows.Any(entity => entity.Status == "not_found"),
            rows,
            summary);
    }

    private async Task<MovementDetailResponse> CreateMovementFromExistingAnimalsAsync(
        MovementContext context,
        string? serie,
        DateTime departureDate,
        DateTime arrivalDate,
        DateTime? solicitationDate,
        string? meansOfTransport,
        string? transportName,
        string? vehicleRegistrationNumber,
        string? healthDocumentNumber,
        IReadOnlyCollection<long> animalIds,
        CancellationToken cancellationToken)
    {
        var departureDay = ToDateOnly(departureDate);
        var arrivalDay = ToDateOnly(arrivalDate);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var animals = await dbContext.Animals
            .Include(entity => entity.LivestockFarm)
            .Include(entity => entity.Porcino)
            .Include(entity => entity.OvinoCaprino)
            .Where(entity => animalIds.Contains(entity.Id))
            .ToListAsync(cancellationToken);

        if (animals.Count != animalIds.Count)
        {
            throw new DomainException("Al menos uno de los animales seleccionados no existe.");
        }

        ValidateExistingAnimalsForMovement(context, animals);

        var movement = BuildMovementCertificate(
            context,
            serie,
            departureDate,
            arrivalDate,
            solicitationDate,
            meansOfTransport,
            transportName,
            vehicleRegistrationNumber,
            animalIds.Count);

        dbContext.MovementCertificates.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var animal in animals)
        {
            ApplyMovementToExistingAnimal(context, animal, departureDay, arrivalDay, healthDocumentNumber);
        }

        dbContext.MovementCertificateAnimals.AddRange(animals.Select(entity => new MovementCertificateAnimal
        {
            MovementCertificateId = movement.Id,
            AnimalId = entity.Id
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordMovementSnapshotsAsync(context, animals, departureDay, arrivalDay, healthDocumentNumber, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetMovementAsyncForCommittedTransaction(movement.Id, cancellationToken);
    }

    private async Task<MovementDetailResponse> CreateExternalEntryMovementFromLinesAsync(
        MovementContext context,
        string? serie,
        DateTime departureDate,
        DateTime arrivalDate,
        DateTime? solicitationDate,
        string? meansOfTransport,
        string? transportName,
        string? vehicleRegistrationNumber,
        string? healthDocumentNumber,
        SharedAnimalDataRequest? sharedAnimalData,
        IReadOnlyList<ParsedIdentificationLine> parsedLines,
        CancellationToken cancellationToken)
    {
        var rawText = string.Join(Environment.NewLine, parsedLines.Select(entity => entity.Value));
        var preview = await BuildPreviewAsync(context, rawText, cancellationToken);
        var processableRows = preview.Rows
            .Where(entity => entity.Status is "existing" or "not_found")
            .ToList();

        if (processableRows.Count == 0)
        {
            throw new DomainException("No hay animales válidos para registrar en esta entrada.");
        }

        return await CreateExternalEntryMovementFromRowsAsync(
            context,
            serie,
            departureDate,
            arrivalDate,
            solicitationDate,
            meansOfTransport,
            transportName,
            vehicleRegistrationNumber,
            healthDocumentNumber,
            sharedAnimalData,
            processableRows,
            cancellationToken);
    }

    private async Task<MovementDetailResponse> CreateExternalEntryMovementFromRowsAsync(
        MovementContext context,
        string? serie,
        DateTime departureDate,
        DateTime arrivalDate,
        DateTime? solicitationDate,
        string? meansOfTransport,
        string? transportName,
        string? vehicleRegistrationNumber,
        string? healthDocumentNumber,
        SharedAnimalDataRequest? sharedAnimalData,
        IReadOnlyList<MovementImportPreviewRowResponse> rows,
        CancellationToken cancellationToken)
    {
        var departureDay = ToDateOnly(departureDate);
        var arrivalDay = ToDateOnly(arrivalDate);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var identifications = rows.Select(entity => entity.Identification).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var animalLookup = await LoadTrackedAnimalsByIdentificationAsync(identifications, cancellationToken);
        var revalidatedRows = rows
            .Select(entity => new RevalidatedImportRow(entity, animalLookup.GetValueOrDefault(entity.Identification)))
            .ToList();
        var hasNewAnimals = revalidatedRows.Any(entity => entity.Animal is null);

        if (hasNewAnimals)
        {
            ValidateSharedAnimalData(context, sharedAnimalData);
        }

        foreach (var row in revalidatedRows)
        {
            if (row.Animal is not null)
            {
                if (context.IsBulkImport)
                {
                    throw new DomainException($"El animal {row.Row.Identification} ya existe en Pecualia.");
                }

                EnsureExternalEntryAnimalCanBeProcessed(context, row.Animal, row.Row.Identification);
            }
        }

        var movement = BuildMovementCertificate(
            context,
            serie,
            departureDate,
            arrivalDate,
            solicitationDate,
            meansOfTransport,
            transportName,
            vehicleRegistrationNumber,
            rows.Count);

        dbContext.MovementCertificates.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);

        var affectedAnimals = new List<Animal>(rows.Count);

        var existingAnimalsToReactivate = revalidatedRows
            .Where(entity => entity.Animal is not null)
            .Select(entity => entity.Animal!)
            .ToList();

        foreach (var animal in existingAnimalsToReactivate)
        {
            ApplyExternalEntryToExistingAnimal(context, animal, departureDay, arrivalDay, healthDocumentNumber);
            affectedAnimals.Add(animal);
        }

        var newAnimals = revalidatedRows
            .Where(entity => entity.Animal is null)
            .Select(entity => BuildNewAnimalForExternalEntry(
                context,
                entity.Row.Identification,
                sharedAnimalData!,
                departureDay,
                arrivalDay,
                healthDocumentNumber))
            .ToList();

        if (newAnimals.Count > 0)
        {
            dbContext.Animals.AddRange(newAnimals);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (context.Species == LivestockSpecies.Porcine)
            {
                dbContext.PorcinoAnimals.AddRange(newAnimals.Select(entity => new PorcinoAnimal
                {
                    AnimalId = entity.Id,
                    AnimalType = sharedAnimalData!.Porcino!.AnimalType.Trim(),
                    IdentificationDate = sharedAnimalData.Porcino.IdentificationDate,
                    PigRegistrationNumber = NormalizeNullable(sharedAnimalData.Porcino.PigRegistrationNumber),
                    Tag = NormalizeNullable(sharedAnimalData.Porcino.Tag)
                }));
            }
            else
            {
                dbContext.OvinoCaprinoAnimals.AddRange(newAnimals.Select(entity => new OvinoCaprinoAnimal
                {
                    AnimalId = entity.Id,
                    SpeciesType = context.Species,
                    Genotyping = NormalizeNullable(sharedAnimalData!.OvinoCaprino?.Genotyping),
                    DominantAllele = NormalizeNullable(sharedAnimalData.OvinoCaprino?.DominantAllele),
                    LowAllele = NormalizeNullable(sharedAnimalData.OvinoCaprino?.LowAllele)
                }));
            }

            affectedAnimals.AddRange(newAnimals);
        }

        dbContext.MovementCertificateAnimals.AddRange(affectedAnimals.Select(entity => new MovementCertificateAnimal
        {
            MovementCertificateId = movement.Id,
            AnimalId = entity.Id
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordMovementSnapshotsAsync(context, affectedAnimals, departureDay, arrivalDay, healthDocumentNumber, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetMovementAsyncForCommittedTransaction(movement.Id, cancellationToken);
    }

    private async Task RecordMovementSnapshotsAsync(
        MovementContext context,
        IReadOnlyCollection<Animal> movedAnimals,
        DateOnly departureDate,
        DateOnly? arrivalDate,
        string? healthDocumentNumber,
        CancellationToken cancellationToken)
    {
        var movementDate = arrivalDate ?? departureDate;
        var snapshots = BuildSnapshotRequests(context, movedAnimals, movementDate, healthDocumentNumber);

        foreach (var snapshot in snapshots)
        {
            var balance = new Balance
            {
                LivestockFarmId = snapshot.FarmId,
                BalanceDate = snapshot.Date,
                DestinationLivestockCode = snapshot.DestinationCode,
                HealthDocumentNumber = NormalizeNullable(healthDocumentNumber),
                ModificationCause = snapshot.Cause,
                NumberOfAnimals = movedAnimals.Count,
                OriginLivestockCode = snapshot.OriginCode
            };

            dbContext.Balances.Add(balance);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (snapshot.Species == LivestockSpecies.Porcine)
            {
                dbContext.BalancePorcino.Add(BuildPorcineBalance(balance.Id, movedAnimals, snapshot.Date));
            }
            else
            {
                dbContext.BalanceOvinoCaprino.Add(BuildOvineOrCaprineBalance(balance.Id, movedAnimals, snapshot.Date));
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var activeAnimals = await dbContext.Animals
                .AsNoTracking()
                .Include(entity => entity.Porcino)
                .Include(entity => entity.OvinoCaprino)
                .Where(entity => entity.LivestockFarmId == snapshot.FarmId && entity.DischargeDate == null)
                .ToListAsync(cancellationToken);

            var census = new Census
            {
                LivestockFarmId = snapshot.FarmId,
                CensusDate = snapshot.Date
            };

            dbContext.Census.Add(census);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (snapshot.Species == LivestockSpecies.Porcine)
            {
                dbContext.CensusPorcino.Add(BuildPorcineCensus(census.Id, activeAnimals, snapshot.Date));
            }
            else
            {
                dbContext.CensusOvinoCaprino.Add(BuildOvineOrCaprineCensus(census.Id, activeAnimals, snapshot.Date));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static IReadOnlyList<SnapshotRequest> BuildSnapshotRequests(
        MovementContext context,
        IReadOnlyCollection<Animal> movedAnimals,
        DateOnly movementDate,
        string? healthDocumentNumber)
    {
        var requests = new List<SnapshotRequest>();
        if (context.Direction == MovementDirection.Exit)
        {
            requests.Add(new SnapshotRequest(
                context.CurrentFarm.Id,
                context.Species,
                movementDate,
                context.Cause,
                context.CurrentFarm.RegaCode,
                context.CounterpartyCode));

            if (context.CounterpartyFarm is not null)
            {
                requests.Add(new SnapshotRequest(
                    context.CounterpartyFarm.Id,
                    context.Species,
                    movementDate,
                    context.Cause,
                    context.CurrentFarm.RegaCode,
                    context.CounterpartyFarm.RegaCode));
            }

            return requests;
        }

        requests.Add(new SnapshotRequest(
            context.CurrentFarm.Id,
            context.Species,
            movementDate,
            context.Cause,
            context.CounterpartyCode,
            context.CurrentFarm.RegaCode));

        if (context.CounterpartyFarm is not null)
        {
            requests.Add(new SnapshotRequest(
                context.CounterpartyFarm.Id,
                context.Species,
                movementDate,
                context.Cause,
                context.CounterpartyFarm.RegaCode,
                context.CurrentFarm.RegaCode));
        }

        return requests;
    }

    private void ValidateExistingAnimalsForMovement(MovementContext context, IReadOnlyCollection<Animal> animals)
    {
        foreach (var animal in animals)
        {
            if (animal.LivestockFarm.LivestockSpecies != context.Species)
            {
                throw new DomainException($"El animal {animal.Identification} no pertenece a la misma especie que la guía.");
            }

            if (context.Direction == MovementDirection.Exit)
            {
                if (animal.LivestockFarmId != context.CurrentFarm.Id || animal.DischargeDate is not null)
                {
                    throw new DomainException($"El animal {animal.Identification} no está activo en la explotación de salida.");
                }

                continue;
            }

            if (context.CounterpartyType == MovementCounterpartyType.Internal)
            {
                if (animal.LivestockFarmId != context.CounterpartyFarm!.Id || animal.DischargeDate is not null)
                {
                    throw new DomainException($"El animal {animal.Identification} no está activo en la explotación origen.");
                }

                continue;
            }

            EnsureExternalEntryAnimalCanBeProcessed(context, animal, animal.Identification);
        }
    }

    private static void EnsureExternalEntryAnimalCanBeProcessed(MovementContext context, Animal animal, string identification)
    {
        if (animal.DischargeDate is null && animal.LivestockFarmId == context.CurrentFarm.Id)
        {
            throw new DomainException($"El animal {identification} ya está activo en la explotación de destino.");
        }

        if (animal.DischargeDate is null && animal.LivestockFarmId != context.CurrentFarm.Id)
        {
            throw new DomainException($"El animal {identification} ya está activo en otra explotación interna.");
        }

        if (animal.LivestockFarm.LivestockSpecies != context.Species)
        {
            throw new DomainException($"El animal {identification} está registrado con una especie distinta.");
        }
    }

    private static void ApplyMovementToExistingAnimal(
        MovementContext context,
        Animal animal,
        DateOnly departureDate,
        DateOnly? arrivalDate,
        string? healthDocumentNumber)
    {
        if (context.Direction == MovementDirection.Exit)
        {
            if (context.CounterpartyType == MovementCounterpartyType.External)
            {
                animal.DischargeDate = departureDate;
                animal.DischargeCause = ParseDischargeCause(context.Cause);
                animal.DestinationCode = context.CounterpartyCode;
            }
            else
            {
                animal.LivestockFarmId = context.CounterpartyFarm!.Id;
                animal.RegistrationDate = arrivalDate ?? departureDate;
                animal.RegistrationCause = ParseRegistrationCause(context.Cause);
                animal.OriginCode = context.CurrentFarm.RegaCode;
                animal.HealthDocumentNumber = NormalizeNullable(healthDocumentNumber) ?? animal.HealthDocumentNumber;
                animal.DischargeDate = null;
                animal.DischargeCause = null;
                animal.DestinationCode = null;
            }

            return;
        }

        animal.LivestockFarmId = context.CurrentFarm.Id;
        animal.RegistrationDate = arrivalDate ?? departureDate;
        animal.RegistrationCause = ParseRegistrationCause(context.Cause);
        animal.OriginCode = context.CounterpartyCode;
        animal.HealthDocumentNumber = NormalizeNullable(healthDocumentNumber) ?? animal.HealthDocumentNumber;
        animal.DischargeDate = null;
        animal.DischargeCause = null;
        animal.DestinationCode = null;
    }

    private static void ApplyExternalEntryToExistingAnimal(
        MovementContext context,
        Animal animal,
        DateOnly departureDate,
        DateOnly? arrivalDate,
        string? healthDocumentNumber)
    {
        animal.LivestockFarmId = context.CurrentFarm.Id;
        animal.RegistrationDate = arrivalDate ?? departureDate;
        animal.RegistrationCause = ParseRegistrationCause(context.Cause);
        animal.OriginCode = context.CounterpartyCode;
        animal.HealthDocumentNumber = NormalizeNullable(healthDocumentNumber) ?? animal.HealthDocumentNumber;
        animal.DischargeDate = null;
        animal.DischargeCause = null;
        animal.DestinationCode = null;
    }

    private static Animal BuildNewAnimalForExternalEntry(
        MovementContext context,
        string identification,
        SharedAnimalDataRequest sharedAnimalData,
        DateOnly departureDate,
        DateOnly? arrivalDate,
        string? healthDocumentNumber)
    {
        return new Animal
        {
            LivestockFarmId = context.CurrentFarm.Id,
            Identification = identification,
            BirthDate = sharedAnimalData.BirthDate ?? (sharedAnimalData.BirthYear is null ? null : new DateOnly(sharedAnimalData.BirthYear.Value, 1, 1)),
            BirthYear = sharedAnimalData.BirthYear,
            Breed = NormalizeOfficialBreed(context.Species, sharedAnimalData.Breed),
            Sex = NormalizeNullable(sharedAnimalData.Sex),
            RegistrationDate = arrivalDate ?? departureDate,
            RegistrationCause = ParseRegistrationCause(context.Cause),
            OriginCode = context.CounterpartyCode,
            HealthDocumentNumber = NormalizeNullable(healthDocumentNumber),
            DischargeDate = null,
            DischargeCause = null,
            DestinationCode = null
        };
    }

    private async Task<MovementContext> PrepareImportContextAsync(
        long userId,
        UserRole role,
        long farmId,
        MovementImportOperation operation,
        string? counterpartyExternalCode,
        string? counterpartyExternalName,
        string? codRemo,
        MovementImportCause cause,
        CancellationToken cancellationToken)
    {
        if (operation is not (MovementImportOperation.Alta or MovementImportOperation.Baja))
        {
            throw new DomainException("Debes indicar si la importación es alta o baja masiva.");
        }

        ValidateImportCause(operation, cause);

        var currentFarm = await BuildAccessibleFarmQuery(userId, role)
            .SingleOrDefaultAsync(entity => entity.Id == farmId, cancellationToken);

        if (currentFarm is null)
        {
            throw new DomainException("Explotación no encontrada.");
        }

        var direction = operation == MovementImportOperation.Alta
            ? MovementDirection.Entry
            : MovementDirection.Exit;
        var fallbackCounterpartyName = direction == MovementDirection.Entry
            ? "Origen no informado"
            : "Destino no informado";
        var counterpartyName = NormalizeNullable(counterpartyExternalName) ?? fallbackCounterpartyName;
        var rawCounterpartyCode = NormalizeNullable(counterpartyExternalCode) ?? counterpartyName;
        var counterpartyCode = direction == MovementDirection.Exit && cause == MovementImportCause.Muerte
            ? NormalizeDeathDestinationCode(currentFarm.LivestockSpecies, rawCounterpartyCode)
            : NormalizeExternalCounterpartyRegaCode(rawCounterpartyCode);

        return new MovementContext(
            currentFarm,
            null,
            currentFarm.LivestockSpecies,
            direction,
            MovementCounterpartyType.External,
            NormalizeNullable(codRemo)?.ToUpperInvariant(),
            cause.ToString(),
            counterpartyName,
            counterpartyCode,
            true);
    }

    private async Task<MovementContext> PrepareContextAsync(
        long userId,
        UserRole role,
        long farmId,
        MovementDirection direction,
        MovementCounterpartyType counterpartyType,
        long? counterpartyFarmId,
        string? counterpartyExternalCode,
        string? counterpartyExternalName,
        string codRemo,
        string cause,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codRemo))
        {
            throw new DomainException("El código REMO de la guía es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(cause))
        {
            throw new DomainException("La causa del movimiento es obligatoria.");
        }

        ValidateMovementCause(direction, cause);

        var currentFarm = await BuildAccessibleFarmQuery(userId, role)
            .SingleOrDefaultAsync(entity => entity.Id == farmId, cancellationToken);

        if (currentFarm is null)
        {
            throw new DomainException("Explotación no encontrada.");
        }

        LivestockFarm? counterpartyFarm = null;
        if (counterpartyType == MovementCounterpartyType.Internal)
        {
            if (counterpartyFarmId is null)
            {
                throw new DomainException("Debes seleccionar una explotación de origen o destino.");
            }

            counterpartyFarm = await BuildAccessibleFarmQuery(userId, role)
                .SingleOrDefaultAsync(entity => entity.Id == counterpartyFarmId.Value, cancellationToken);

            if (counterpartyFarm is null)
            {
                throw new DomainException("La explotación contraparte no está disponible.");
            }

            if (counterpartyFarm.Id == currentFarm.Id)
            {
                throw new DomainException("La explotación origen y destino no pueden ser la misma.");
            }

            if (counterpartyFarm.LivestockSpecies != currentFarm.LivestockSpecies)
            {
                throw new DomainException("Las explotaciones implicadas en el movimiento deben ser de la misma especie.");
            }
        }
        else if (string.IsNullOrWhiteSpace(counterpartyExternalName))
        {
            throw new DomainException("Debes indicar el nombre de la contraparte externa.");
        }

        var counterpartyName = counterpartyType == MovementCounterpartyType.Internal
            ? counterpartyFarm!.Name
            : counterpartyExternalName!.Trim();
        var rawCounterpartyCode = counterpartyType == MovementCounterpartyType.Internal
            ? counterpartyFarm!.RegaCode
            : NormalizeNullable(counterpartyExternalCode) ?? counterpartyExternalName!.Trim();
        var counterpartyCode = direction == MovementDirection.Exit &&
            counterpartyType == MovementCounterpartyType.External &&
            ParseDischargeCause(cause) == AnimalDischargeCause.Muerte
                ? NormalizeDeathDestinationCode(currentFarm.LivestockSpecies, rawCounterpartyCode)
                : counterpartyType == MovementCounterpartyType.Internal
                    ? rawCounterpartyCode
                    : NormalizeExternalCounterpartyRegaCode(rawCounterpartyCode);

        return new MovementContext(
            currentFarm,
            counterpartyFarm,
            currentFarm.LivestockSpecies,
            direction,
            counterpartyType,
            codRemo.Trim().ToUpperInvariant(),
            cause.Trim(),
            counterpartyName,
            counterpartyCode,
            false);
    }

    private IQueryable<LivestockFarm> BuildAccessibleFarmQuery(long userId, UserRole role)
    {
        return role == UserRole.Manager
            ? dbContext.Farms.Where(entity => entity.Farmer.ManagerId == userId)
            : dbContext.Farms.Where(entity => entity.FarmerId == userId);
    }

    private IQueryable<MovementCertificate> BuildAccessibleMovementQuery(long userId, UserRole role)
    {
        var accessibleFarmIds = BuildAccessibleFarmQuery(userId, role).Select(entity => entity.Id);
        return dbContext.MovementCertificates.Where(entity =>
            (entity.OriginLivestockId != null && accessibleFarmIds.Contains(entity.OriginLivestockId.Value)) ||
            (entity.DestinationLivestockId != null && accessibleFarmIds.Contains(entity.DestinationLivestockId.Value)));
    }

    private async Task EnsureAccessibleFarmAsync(long userId, UserRole role, long farmId, CancellationToken cancellationToken)
    {
        var exists = await BuildAccessibleFarmQuery(userId, role)
            .AnyAsync(entity => entity.Id == farmId, cancellationToken);

        if (!exists)
        {
            throw new DomainException("Explotación no encontrada.");
        }
    }

    private async Task<Dictionary<string, Animal>> LoadAnimalsByIdentificationAsync(IReadOnlyList<string> identifications, CancellationToken cancellationToken)
    {
        var animals = await dbContext.Animals
            .AsNoTracking()
            .Include(entity => entity.LivestockFarm)
            .Where(entity => identifications.Contains(entity.Identification))
            .ToListAsync(cancellationToken);

        return animals.ToDictionary(entity => entity.Identification, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, Animal>> LoadTrackedAnimalsByIdentificationAsync(IReadOnlyList<string> identifications, CancellationToken cancellationToken)
    {
        var animals = await dbContext.Animals
            .Include(entity => entity.LivestockFarm)
            .Include(entity => entity.Porcino)
            .Include(entity => entity.OvinoCaprino)
            .Where(entity => identifications.Contains(entity.Identification))
            .ToListAsync(cancellationToken);

        return animals.ToDictionary(entity => entity.Identification, StringComparer.OrdinalIgnoreCase);
    }

    private MovementImportPreviewRowResponse BuildPreviewRow(MovementContext context, ParsedIdentificationLine parsed, Animal? animal)
    {
        if (context.IsBulkImport)
        {
            return BuildBulkImportPreviewRow(context, parsed, animal);
        }

        if (context.Direction == MovementDirection.Exit)
        {
            if (animal is null)
            {
                return new MovementImportPreviewRowResponse(
                    parsed.LineNumber,
                    parsed.Value,
                    "conflict",
                    "Excluido",
                    "El animal no existe en la aplicación.",
                    null,
                    null);
            }

            if (animal.LivestockFarmId != context.CurrentFarm.Id || animal.DischargeDate is not null)
            {
                return new MovementImportPreviewRowResponse(
                    parsed.LineNumber,
                    parsed.Value,
                    "conflict",
                    "Excluido",
                    "El animal no está activo en la explotación de salida.",
                    DescribeAnimal(animal),
                    animal.Id);
            }

            return new MovementImportPreviewRowResponse(
                parsed.LineNumber,
                parsed.Value,
                "valid",
                "Registrar salida",
                "El animal está listo para salir en esta guía.",
                DescribeAnimal(animal),
                animal.Id);
        }

        if (context.CounterpartyType == MovementCounterpartyType.Internal)
        {
            if (animal is null)
            {
                return new MovementImportPreviewRowResponse(
                    parsed.LineNumber,
                    parsed.Value,
                    "conflict",
                    "Excluido",
                    "El animal no existe en la aplicación.",
                    null,
                    null);
            }

            if (animal.LivestockFarmId != context.CounterpartyFarm!.Id || animal.DischargeDate is not null)
            {
                return new MovementImportPreviewRowResponse(
                    parsed.LineNumber,
                    parsed.Value,
                    "conflict",
                    "Excluido",
                    "El animal no está activo en la explotación origen.",
                    DescribeAnimal(animal),
                    animal.Id);
            }

            return new MovementImportPreviewRowResponse(
                parsed.LineNumber,
                parsed.Value,
                "valid",
                "Registrar entrada",
                "El animal está listo para entrar desde la explotación origen.",
                DescribeAnimal(animal),
                animal.Id);
        }

        if (animal is null)
        {
            return new MovementImportPreviewRowResponse(
                parsed.LineNumber,
                parsed.Value,
                "not_found",
                "Crear con datos compartidos",
                "El animal no existe en la aplicación y requerirá datos comunes antes de confirmar.",
                null,
                null);
        }

        if (animal.LivestockFarm.LivestockSpecies != context.Species)
        {
            return new MovementImportPreviewRowResponse(
                parsed.LineNumber,
                parsed.Value,
                "conflict",
                "Excluido",
                "El animal está registrado con una especie distinta.",
                DescribeAnimal(animal),
                animal.Id);
        }

        if (animal.DischargeDate is null && animal.LivestockFarmId == context.CurrentFarm.Id)
        {
            return new MovementImportPreviewRowResponse(
                parsed.LineNumber,
                parsed.Value,
                "conflict",
                "Excluido",
                "El animal ya está activo en la explotación destino.",
                DescribeAnimal(animal),
                animal.Id);
        }

        if (animal.DischargeDate is null && animal.LivestockFarmId != context.CurrentFarm.Id)
        {
            return new MovementImportPreviewRowResponse(
                parsed.LineNumber,
                parsed.Value,
                "conflict",
                "Excluido",
                "El animal ya está activo en otra explotación interna.",
                DescribeAnimal(animal),
                animal.Id);
        }

        return new MovementImportPreviewRowResponse(
            parsed.LineNumber,
            parsed.Value,
            "existing",
            "Reactivar en explotación",
            "El animal ya existe y se reactivará en la explotación destino.",
            DescribeAnimal(animal),
            animal.Id);
    }

    private static MovementImportPreviewRowResponse BuildBulkImportPreviewRow(MovementContext context, ParsedIdentificationLine parsed, Animal? animal)
    {
        if (context.Direction == MovementDirection.Entry)
        {
            if (animal is null)
            {
                return new MovementImportPreviewRowResponse(
                    parsed.LineNumber,
                    parsed.Value,
                    "not_found",
                    "Alta masiva",
                    "El animal se dará de alta en la explotación seleccionada.",
                    null,
                    null);
            }

            return new MovementImportPreviewRowResponse(
                parsed.LineNumber,
                parsed.Value,
                "existing",
                "Excluido",
                animal.LivestockFarmId == context.CurrentFarm.Id
                    ? "El animal ya existe en esta explotación."
                    : "El animal ya existe en Pecualia y no se dará de alta desde esta importación.",
                DescribeAnimal(animal),
                animal.Id);
        }

        if (animal is null)
        {
            return new MovementImportPreviewRowResponse(
                parsed.LineNumber,
                parsed.Value,
                "conflict",
                "Excluido",
                "El animal no existe en la explotación seleccionada.",
                null,
                null);
        }

        if (animal.LivestockFarmId != context.CurrentFarm.Id || animal.DischargeDate is not null)
        {
            return new MovementImportPreviewRowResponse(
                parsed.LineNumber,
                parsed.Value,
                "conflict",
                "Excluido",
                "El animal no está activo en la explotación seleccionada.",
                DescribeAnimal(animal),
                animal.Id);
        }

        return new MovementImportPreviewRowResponse(
            parsed.LineNumber,
            parsed.Value,
            "valid",
            "Baja masiva",
            "El animal está listo para darse de baja.",
            DescribeAnimal(animal),
            animal.Id);
    }

    private static void ValidateSharedAnimalData(MovementContext context, SharedAnimalDataRequest? sharedAnimalData)
    {
        if (context.Direction != MovementDirection.Entry || context.CounterpartyType != MovementCounterpartyType.External)
        {
            return;
        }

        if (sharedAnimalData is null)
        {
            throw new DomainException("Debes indicar los datos comunes de los animales que no existen en la aplicación.");
        }

        if (string.IsNullOrWhiteSpace(sharedAnimalData.Breed))
        {
            throw new DomainException("La raza común de los animales es obligatoria.");
        }

        _ = NormalizeOfficialBreed(context.Species, sharedAnimalData.Breed);

        if (string.IsNullOrWhiteSpace(sharedAnimalData.Sex))
        {
            throw new DomainException("El sexo común de los animales es obligatorio.");
        }

        if (context.Species == LivestockSpecies.Porcine)
        {
            if (sharedAnimalData.Porcino is null || string.IsNullOrWhiteSpace(sharedAnimalData.Porcino.AnimalType))
            {
                throw new DomainException("Debes indicar el tipo animal porcino para los nuevos registros.");
            }

            return;
        }

        if (sharedAnimalData.OvinoCaprino is null)
        {
            return;
        }

        if (sharedAnimalData.OvinoCaprino.SpeciesType is not (LivestockSpecies.Ovine or LivestockSpecies.Caprine) ||
            sharedAnimalData.OvinoCaprino.SpeciesType != context.Species)
        {
            throw new DomainException("La especie ovino/caprino indicada no es válida.");
        }
    }

    private static void ValidateImportCause(MovementImportOperation operation, MovementImportCause cause)
    {
        if (operation == MovementImportOperation.Alta &&
            cause is not (MovementImportCause.Entrada or MovementImportCause.Autorreposicion))
        {
            throw new DomainException("La causa de alta debe ser Entrada (E) o Autorreposición (A).");
        }

        if (operation == MovementImportOperation.Baja &&
            cause is not (MovementImportCause.Salida or MovementImportCause.Muerte))
        {
            throw new DomainException("La causa de baja debe ser Salida (S) o Muerte (M).");
        }
    }

    private static string NormalizeOfficialBreed(LivestockSpecies species, string? breed)
    {
        if (BookDocumentSupport.TryNormalizeBreed(species, breed, out var normalizedBreed) &&
            !string.IsNullOrWhiteSpace(normalizedBreed))
        {
            return normalizedBreed;
        }

        throw new DomainException("La raza indicada no es válida para la especie de la guía.");
    }

    private static void ValidateMovementCause(MovementDirection direction, string cause)
    {
        if (direction == MovementDirection.Entry)
        {
            _ = ParseRegistrationCause(cause);
            return;
        }

        _ = ParseDischargeCause(cause);
    }

    private static AnimalRegistrationCause ParseRegistrationCause(string cause)
    {
        if (Enum.TryParse<AnimalRegistrationCause>(cause, true, out var registrationCause))
        {
            return registrationCause;
        }

        throw new DomainException("La causa de alta debe ser Entrada (E) o Autorreposición (A).");
    }

    private static AnimalDischargeCause ParseDischargeCause(string cause)
    {
        if (Enum.TryParse<AnimalDischargeCause>(cause, true, out var dischargeCause))
        {
            return dischargeCause;
        }

        throw new DomainException("La causa de baja debe ser Salida (S) o Muerte (M).");
    }

    private static string NormalizeDeathDestinationCode(LivestockSpecies species, string? destinationCode)
    {
        var normalizedDestinationCode = NormalizeNullable(destinationCode)?.ToUpperInvariant();

        if (normalizedDestinationCode is null)
        {
            throw new DomainException("El destino de una baja por muerte es obligatorio.");
        }

        if (species == LivestockSpecies.Porcine)
        {
            if (normalizedDestinationCode != "MER")
            {
                throw new DomainException("En ganado porcino, una baja por muerte solo puede registrarse con destino MER.");
            }

            return normalizedDestinationCode;
        }

        if (normalizedDestinationCode is not ("SANDACH" or "MER"))
        {
            throw new DomainException("El destino de una baja por muerte debe ser SANDACH o MER.");
        }

        return normalizedDestinationCode;
    }

    private static string NormalizeExternalCounterpartyRegaCode(string? counterpartyExternalCode)
    {
        var normalizedCounterpartyCode = NormalizeNullable(counterpartyExternalCode)?.ToUpperInvariant();
        if (normalizedCounterpartyCode is null)
        {
            throw new DomainException("El código REGA de la contraparte externa es obligatorio.");
        }

        if (!DomainValidators.IsValidRegaCode(normalizedCounterpartyCode))
        {
            throw new DomainException("El código REGA de la contraparte externa no es válido. Debe seguir el formato ES seguido de 12 dígitos.");
        }

        return normalizedCounterpartyCode;
    }

    private async Task<MovementDetailResponse> GetMovementAsyncForCommittedTransaction(long movementId, CancellationToken cancellationToken)
    {
        var movement = await dbContext.MovementCertificates
            .AsNoTracking()
            .Include(entity => entity.OriginFarm)
            .Include(entity => entity.DestinationFarm)
            .Include(entity => entity.Animals)
            .ThenInclude(entity => entity.Animal)
            .SingleAsync(entity => entity.Id == movementId, cancellationToken);

        return MapMovementDetail(movement);
    }

    private static MovementCertificate BuildMovementCertificate(
        MovementContext context,
        string? serie,
        DateTime departureDate,
        DateTime? arrivalDate,
        DateTime? solicitationDate,
        string? meansOfTransport,
        string? transportName,
        string? vehicleRegistrationNumber,
        int numberOfAnimals)
    {
        var isEntry = context.Direction == MovementDirection.Entry;
        return new MovementCertificate
        {
            OriginLivestockId = isEntry
                ? (context.CounterpartyType == MovementCounterpartyType.Internal ? context.CounterpartyFarm!.Id : null)
                : context.CurrentFarm.Id,
            DestinationLivestockId = isEntry
                ? context.CurrentFarm.Id
                : (context.CounterpartyType == MovementCounterpartyType.Internal ? context.CounterpartyFarm!.Id : null),
            ArrivalDate = arrivalDate,
            DepartureDate = departureDate,
            MeansOfTransport = NormalizeNullable(meansOfTransport),
            NumberOfAnimals = numberOfAnimals,
            Specie = context.Species.ToString(),
            Status = MovementStatus.Pending,
            CodRemo = context.CodRemo,
            Serie = NormalizeNullable(serie),
            SolicitationDate = solicitationDate,
            TransportName = NormalizeNullable(transportName),
            VehicleRegistrationNumber = NormalizeNullable(vehicleRegistrationNumber),
            OriginExternalCode = isEntry && context.CounterpartyType == MovementCounterpartyType.External ? context.CounterpartyCode : null,
            OriginExternalName = isEntry && context.CounterpartyType == MovementCounterpartyType.External ? context.CounterpartyName : null,
            DestinationExternalCode = !isEntry && context.CounterpartyType == MovementCounterpartyType.External ? context.CounterpartyCode : null,
            DestinationExternalName = !isEntry && context.CounterpartyType == MovementCounterpartyType.External ? context.CounterpartyName : null
        };
    }

    private static FarmMovementListItemResponse MapFarmMovementListItem(MovementCertificate movement, long farmId)
    {
        var isExit = movement.OriginLivestockId == farmId;
        var counterpartyName = isExit
            ? movement.DestinationFarm?.Name ?? movement.DestinationExternalName ?? "Explotación externa"
            : movement.OriginFarm?.Name ?? movement.OriginExternalName ?? "Explotación externa";
        var counterpartyCode = isExit
            ? movement.DestinationFarm?.RegaCode ?? movement.DestinationExternalCode ?? "Externo"
            : movement.OriginFarm?.RegaCode ?? movement.OriginExternalCode ?? "Externo";

        return new FarmMovementListItemResponse(
            movement.Id,
            movement.CodRemo ?? string.Empty,
            movement.Serie,
            isExit ? "Exit" : "Entry",
            counterpartyName,
            counterpartyCode,
            movement.Specie,
            movement.NumberOfAnimals,
            movement.DepartureDate,
            movement.ArrivalDate,
            movement.TransportName,
            movement.VehicleRegistrationNumber,
            BuildMovementStatus(movement));
    }

    private static MovementDetailResponse MapMovementDetail(MovementCertificate movement)
    {
        return new MovementDetailResponse(
            movement.Id,
            movement.OriginLivestockId,
            movement.DestinationLivestockId,
            movement.CodRemo ?? string.Empty,
            movement.Serie,
            movement.Specie,
            movement.DepartureDate,
            movement.ArrivalDate,
            movement.SolicitationDate,
            movement.MeansOfTransport,
            movement.TransportName,
            movement.VehicleRegistrationNumber,
            movement.OriginFarm?.Name ?? movement.OriginExternalName,
            movement.OriginFarm?.RegaCode ?? movement.OriginExternalCode,
            movement.DestinationFarm?.Name ?? movement.DestinationExternalName,
            movement.DestinationFarm?.RegaCode ?? movement.DestinationExternalCode,
            movement.NumberOfAnimals,
            BuildMovementStatus(movement),
            movement.Animals
                .OrderBy(entity => entity.Animal.Identification)
                .Select(entity => new MovementAnimalItemResponse(
                    entity.AnimalId,
                    entity.Animal.Identification,
                    NormalizeNullable(entity.Animal.Breed),
                    NormalizeNullable(entity.Animal.Sex),
                    FarmCensusProjectionSupport.ResolveBirthYear(entity.Animal),
                    entity.Animal.DischargeDate is null ? "Active" : "Discharged"))
                .ToList());
    }

    private static string BuildMovementStatus(MovementCertificate movement)
    {
        return movement.Status.ToString();
    }

    private static void ValidateMovementTimeline(
        DateTime departureDate,
        DateTime arrivalDate,
        DateTime? solicitationDate)
    {
        if (arrivalDate < departureDate)
        {
            throw new DomainException("La fecha de llegada no puede ser anterior a la fecha de salida.");
        }

        if (solicitationDate is not null && solicitationDate > departureDate)
        {
            throw new DomainException("La fecha de solicitud no puede ser posterior a la fecha de salida.");
        }
    }

    private static DateOnly ToDateOnly(DateTime value)
    {
        return DateOnly.FromDateTime(value.Date);
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeIdentification(string value)
    {
        var normalizedLine = value.Trim().ToUpperInvariant();
        var officialMatch = SpanishOfficialIdentificationFinderRegex.Match(normalizedLine);
        if (officialMatch.Success)
        {
            return NormalizeIdentifierToken(officialMatch.Value);
        }

        var porcineAlternativeMatch = PorcineAlternativeIdentificationFinderRegex.Match(normalizedLine);
        if (porcineAlternativeMatch.Success)
        {
            return NormalizeIdentifierToken(porcineAlternativeMatch.Value);
        }

        var firstToken = normalizedLine
            .Split(new[] { ' ', '\t', ',', ';', '|', ':', '#', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;

        return NormalizeIdentifierToken(firstToken);
    }

    private static bool IsIdentificationValid(LivestockSpecies species, string identification)
    {
        return DomainValidators.IsValidAnimalIdentification(species, identification);
    }

    private static string BuildIdentificationFormatMessage(LivestockSpecies species)
    {
        return species == LivestockSpecies.Porcine
            ? "Formato inválido. Para porcino se espera ES seguido de 12 dígitos o GT seguido de números."
            : "Formato inválido. Para ovino/caprino se espera ES seguido de 12 dígitos o ES seguido de 12 dígitos con sufijo.";
    }

    private static string NormalizeIdentifierToken(string value)
    {
        var token = value
            .Trim()
            .ToUpperInvariant();

        var officialMatch = Regex.Match(token, "^ES[\\s._-]*((?:\\d[\\s._-]*){12})(?:-([A-Z0-9]{3,}))?$");
        if (officialMatch.Success)
        {
            var digits = Regex.Replace(officialMatch.Groups[1].Value, "\\D", string.Empty);
            var suffix = officialMatch.Groups[2].Success ? $"-{officialMatch.Groups[2].Value}" : string.Empty;
            return $"ES{digits}{suffix}";
        }

        var porcineAlternativeMatch = Regex.Match(token, "^GT[\\s._-]*(\\d+)$");
        if (porcineAlternativeMatch.Success)
        {
            return $"GT{porcineAlternativeMatch.Groups[1].Value}";
        }

        return token
            .Replace(" ", string.Empty)
            .Replace("\t", string.Empty)
            .Replace(".", string.Empty)
            .Replace("_", string.Empty);
    }

    private static IReadOnlyList<ParsedIdentificationLine> ParseLines(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return [];
        }

        var lines = rawText
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.None)
            .Select((entity, index) => new ParsedIdentificationLine(index + 1, entity))
            .Where(entity => !string.IsNullOrWhiteSpace(entity.Value))
            .ToList();

        return lines;
    }

    private static IReadOnlyList<ParsedIdentificationLine> ParseLines(IReadOnlyList<string>? rawLines)
    {
        if (rawLines is null || rawLines.Count == 0)
        {
            return [];
        }

        return rawLines
            .Select((entity, index) => new ParsedIdentificationLine(index + 1, entity))
            .Where(entity => !string.IsNullOrWhiteSpace(entity.Value))
            .ToList();
    }

    private static string? DescribeAnimal(Animal animal)
    {
        var tokens = new List<string>();

        if (!string.IsNullOrWhiteSpace(animal.Breed))
        {
            tokens.Add(animal.Breed.Trim());
        }

        if (!string.IsNullOrWhiteSpace(animal.Sex))
        {
            tokens.Add(animal.Sex.Trim());
        }

        var birthYear = FarmCensusProjectionSupport.ResolveBirthYear(animal);
        if (birthYear is not null)
        {
            tokens.Add(birthYear.Value.ToString());
        }

        return tokens.Count == 0 ? null : string.Join(" · ", tokens);
    }

    private static BalanceOvinoCaprino BuildOvineOrCaprineBalance(long balanceId, IReadOnlyCollection<Animal> animals, DateOnly asOfDate)
    {
        var classification = ClassifyOvineOrCaprineAnimals(animals, asOfDate);
        return new BalanceOvinoCaprino
        {
            BalanceId = balanceId,
            NonReproductiveBetween4And12Months = classification.NonReproductiveBetween4And12Months,
            NonReproductiveUnder4Months = classification.NonReproductiveUnder4Months,
            ReproductiveFemales = classification.ReproductiveFemales,
            ReproductiveMales = classification.ReproductiveMales,
            TransportTicketNumber = null,
            TransporterName = null
        };
    }

    private static CensusOvinoCaprino BuildOvineOrCaprineCensus(long censusId, IReadOnlyCollection<Animal> animals, DateOnly asOfDate)
    {
        var classification = ClassifyOvineOrCaprineAnimals(animals, asOfDate);
        return new CensusOvinoCaprino
        {
            CensusId = censusId,
            NonReproductiveBetween4And12Months = classification.NonReproductiveBetween4And12Months,
            NonReproductiveUnder4Months = classification.NonReproductiveUnder4Months,
            ReproductiveFemale = classification.ReproductiveFemales,
            ReproductiveMale = classification.ReproductiveMales
        };
    }

    private static OvineOrCaprineBreakdown ClassifyOvineOrCaprineAnimals(IReadOnlyCollection<Animal> animals, DateOnly asOfDate)
    {
        var underFourMonths = 0;
        var betweenFourAndTwelveMonths = 0;
        var reproductiveFemales = 0;
        var reproductiveMales = 0;

        foreach (var animal in animals)
        {
            if (animal.RegistrationCause == AnimalRegistrationCause.Autorreposicion)
            {
                var autorepositionSex = FarmCensusProjectionSupport.NormalizeSex(animal.Sex);
                if (autorepositionSex == "female")
                {
                    reproductiveFemales++;
                }
                else if (autorepositionSex == "male")
                {
                    reproductiveMales++;
                }
                else
                {
                    betweenFourAndTwelveMonths++;
                }

                continue;
            }

            var birthDate = FarmCensusProjectionSupport.ResolveBirthDate(animal);
            var normalizedSex = FarmCensusProjectionSupport.NormalizeSex(animal.Sex);

            if (birthDate is not null && FarmCensusProjectionSupport.IsYoungerThanMonths(birthDate.Value, asOfDate, 4))
            {
                underFourMonths++;
                continue;
            }

            if (birthDate is not null && FarmCensusProjectionSupport.IsYoungerThanMonths(birthDate.Value, asOfDate, 12))
            {
                betweenFourAndTwelveMonths++;
                continue;
            }

            if (normalizedSex == "female")
            {
                reproductiveFemales++;
            }
            else if (normalizedSex == "male")
            {
                reproductiveMales++;
            }
            else
            {
                betweenFourAndTwelveMonths++;
            }
        }

        return new OvineOrCaprineBreakdown(
            underFourMonths,
            betweenFourAndTwelveMonths,
            reproductiveFemales,
            reproductiveMales);
    }

    private static BalancePorcino BuildPorcineBalance(long balanceId, IReadOnlyCollection<Animal> animals, DateOnly asOfDate)
    {
        var classification = ClassifyPorcineAnimals(animals, asOfDate);
        var firstPorcine = animals.Select(entity => entity.Porcino).FirstOrDefault(entity => entity is not null);

        return new BalancePorcino
        {
            BalanceId = balanceId,
            Baits = classification.Baits,
            Boars = classification.Boars,
            Breed = NormalizeNullable(animals.Select(entity => entity.Breed).FirstOrDefault(entity => !string.IsNullOrWhiteSpace(entity))),
            Piglets = classification.Piglets,
            PigsReposition = classification.PigsReposition,
            Rear = classification.Rears,
            SowsForLive = classification.Sows,
            SowsReposition = classification.SowsReposition,
            Tag = NormalizeNullable(firstPorcine?.Tag),
            Type = NormalizeNullable(firstPorcine?.AnimalType)
        };
    }

    private static CensusPorcino BuildPorcineCensus(long censusId, IReadOnlyCollection<Animal> animals, DateOnly asOfDate)
    {
        var classification = ClassifyPorcineAnimals(animals, asOfDate);
        return new CensusPorcino
        {
            CensusId = censusId,
            Baits = classification.Baits,
            Boars = classification.Boars,
            Piglets = classification.Piglets,
            PigsReposition = classification.PigsReposition,
            Rears = classification.Rears,
            Sow = classification.Sows,
            SowsReposition = classification.SowsReposition
        };
    }

    private static PorcineBreakdown ClassifyPorcineAnimals(IReadOnlyCollection<Animal> animals, DateOnly asOfDate)
    {
        var breakdown = new PorcineBreakdown();

        foreach (var animal in animals)
        {
            var type = FarmCensusProjectionSupport.NormalizeType(animal.Porcino?.AnimalType);
            if (string.IsNullOrWhiteSpace(type))
            {
                var birthDate = FarmCensusProjectionSupport.ResolveBirthDate(animal);
                if (birthDate is not null && FarmCensusProjectionSupport.IsYoungerThanMonths(birthDate.Value, asOfDate, 4))
                {
                    breakdown.Piglets++;
                }
                else
                {
                    breakdown.Rears++;
                }
                continue;
            }

            if (type.Contains("bait", StringComparison.Ordinal) || type.Contains("cebo", StringComparison.Ordinal))
            {
                breakdown.Baits++;
            }
            else if (type.Contains("boar", StringComparison.Ordinal) || type.Contains("verraco", StringComparison.Ordinal))
            {
                breakdown.Boars++;
            }
            else if (type.Contains("piglet", StringComparison.Ordinal) || type.Contains("lech", StringComparison.Ordinal))
            {
                breakdown.Piglets++;
            }
            else if (type.Contains("reposition", StringComparison.Ordinal) && (type.Contains("sow", StringComparison.Ordinal) || type.Contains("cerda", StringComparison.Ordinal)))
            {
                breakdown.SowsReposition++;
            }
            else if (type.Contains("reposition", StringComparison.Ordinal) || type.Contains("repos", StringComparison.Ordinal))
            {
                breakdown.PigsReposition++;
            }
            else if (type.Contains("sow", StringComparison.Ordinal) || type.Contains("cerda", StringComparison.Ordinal))
            {
                breakdown.Sows++;
            }
            else
            {
                breakdown.Rears++;
            }
        }

        return breakdown;
    }

    private sealed record MovementContext(
        LivestockFarm CurrentFarm,
        LivestockFarm? CounterpartyFarm,
        LivestockSpecies Species,
        MovementDirection Direction,
        MovementCounterpartyType CounterpartyType,
        string? CodRemo,
        string Cause,
        string CounterpartyName,
        string CounterpartyCode,
        bool IsBulkImport);

    private sealed record ParsedIdentificationLine(int LineNumber, string Value);

    private sealed record SnapshotRequest(
        long FarmId,
        LivestockSpecies Species,
        DateOnly Date,
        string Cause,
        string? OriginCode,
        string? DestinationCode);

    private sealed record RevalidatedImportRow(MovementImportPreviewRowResponse Row, Animal? Animal);

    private sealed record OvineOrCaprineBreakdown(
        int NonReproductiveUnder4Months,
        int NonReproductiveBetween4And12Months,
        int ReproductiveFemales,
        int ReproductiveMales);

    private sealed class PorcineBreakdown
    {
        public int Baits { get; set; }

        public int Boars { get; set; }

        public int Piglets { get; set; }

        public int PigsReposition { get; set; }

        public int Rears { get; set; }

        public int Sows { get; set; }

        public int SowsReposition { get; set; }
    }
}
