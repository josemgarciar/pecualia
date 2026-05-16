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

public sealed class MovementService(PecualiaDbContext dbContext, IFarmCensusProjectionService censusProjectionService, IClock clock) : IMovementService
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

        if (context.Species == LivestockSpecies.Porcine)
        {
            return await CreateAggregatePorcineMovementAsync(
                context,
                request.Serie,
                request.DepartureDate,
                request.ArrivalDate,
                request.SolicitationDate,
                request.MeansOfTransport,
                request.TransportName,
                request.VehicleRegistrationNumber,
                request.NumberOfAnimals,
                request.Breed,
                request.AnimalType,
                cancellationToken);
        }

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
            ValidateUnidentifiedAnimalRequest(context, request.UnidentifiedAnimalCount.Value, request.UnidentifiedCategory);
            var summary = new MovementImportPreviewSummaryResponse(0, 0, 0, 0, 0, 0, 0, 0);
            return new MovementImportPreviewResponse(context.Species.ToString(), false, [], summary);
        }

        if (context.Species == LivestockSpecies.Porcine)
        {
            ValidatePorcineAggregateMovementRequest(request.NumberOfAnimals, request.Breed, request.AnimalType);

            return new MovementImportPreviewResponse(
                context.Species.ToString(),
                false,
                [],
                new MovementImportPreviewSummaryResponse(0, 0, request.NumberOfAnimals!.Value, 0, 0, 0, 0, 0));
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
            ValidateUnidentifiedAnimalRequest(context, request.UnidentifiedAnimalCount.Value, request.UnidentifiedCategory);
            return await CommitUnidentifiedAnimalMovementAsync(
                context,
                request.Serie,
                request.DepartureDate,
                request.ArrivalDate,
                request.SolicitationDate,
                request.MeansOfTransport,
                request.TransportName,
                request.VehicleRegistrationNumber,
                request.UnidentifiedAnimalCount.Value,
                request.UnidentifiedCategory!.Value,
                cancellationToken);
        }

        if (context.Species == LivestockSpecies.Porcine)
        {
            var porcineMovement = await CreateAggregatePorcineMovementAsync(
                context,
                request.Serie,
                request.DepartureDate,
                request.ArrivalDate,
                request.SolicitationDate,
                request.MeansOfTransport,
                request.TransportName,
                request.VehicleRegistrationNumber,
                request.NumberOfAnimals,
                request.Breed,
                request.AnimalType,
                cancellationToken);

            var processedRows = request.NumberOfAnimals ?? 0;
            return new MovementImportCommitResponse(
                porcineMovement.Id,
                porcineMovement.CodRemo,
                processedRows,
                0,
                false,
                new MovementImportPreviewSummaryResponse(0, 0, processedRows, 0, 0, 0, 0, 0));
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
        int unidentifiedAnimalCount,
        MovementUnidentifiedCategory unidentifiedCategory,
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
            unidentifiedAnimalCount,
            unidentifiedCategory: unidentifiedCategory);

        dbContext.MovementCertificates.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordUnidentifiedMovementSnapshotsAsync(
            context,
            unidentifiedAnimalCount,
            unidentifiedCategory,
            ToDateOnly(departureDate),
            arrivalDate is null ? null : ToDateOnly(arrivalDate.Value),
            transportName,
            vehicleRegistrationNumber,
            cancellationToken);

        var summary = new MovementImportPreviewSummaryResponse(0, 0, 0, 0, 0, 0, 0, 0);

        return new MovementImportCommitResponse(
            movement.Id,
            movement.CodRemo,
            unidentifiedAnimalCount,
            0,
            false,
            summary);
    }

    private async Task<MovementDetailResponse> CreateAggregatePorcineMovementAsync(
        MovementContext context,
        string? serie,
        DateTime departureDate,
        DateTime? arrivalDate,
        DateTime? solicitationDate,
        string? meansOfTransport,
        string? transportName,
        string? vehicleRegistrationNumber,
        int? numberOfAnimals,
        string? breed,
        string? animalType,
        CancellationToken cancellationToken)
    {
        if (context.Species != LivestockSpecies.Porcine)
        {
            throw new DomainException("Este flujo agregado solo está disponible para movimientos porcinos.");
        }

        ValidatePorcineAggregateMovementRequest(numberOfAnimals, breed, animalType);

        var normalizedBreed = NormalizeOfficialBreed(LivestockSpecies.Porcine, breed);
        var normalizedType = NormalizeRequiredAnimalType(animalType);
        var departureDay = ToDateOnly(departureDate);
        DateOnly? arrivalDay = arrivalDate is null ? null : ToDateOnly(arrivalDate.Value);

        await ValidatePorcineAggregateAvailabilityAsync(
            context,
            normalizedType,
            numberOfAnimals!.Value,
            departureDay,
            arrivalDay,
            cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var movement = BuildMovementCertificate(
            context,
            serie,
            departureDate,
            arrivalDate,
            solicitationDate,
            meansOfTransport,
            transportName,
            vehicleRegistrationNumber,
            numberOfAnimals.Value,
            animalType: normalizedType);

        dbContext.MovementCertificates.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);

        await RecordAggregatePorcineMovementSnapshotsAsync(
            context,
            numberOfAnimals.Value,
            normalizedBreed,
            normalizedType,
            departureDay,
            arrivalDay,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetMovementAsyncForCommittedTransaction(movement.Id, cancellationToken);
    }

    private static void ValidateUnidentifiedAnimalRequest(MovementContext context, int count, MovementUnidentifiedCategory? category)
    {
        if (context.Species is not (LivestockSpecies.Ovine or LivestockSpecies.Caprine))
        {
            throw new DomainException("Solo se pueden registrar movimientos sin identificación para ganado ovino o caprino.");
        }

        if (count <= 0 || count > 10000)
        {
            throw new DomainException("El número de animales sin identificar debe estar entre 1 y 10.000.");
        }

        if (category is null)
        {
            throw new DomainException("Debes indicar si los animales sin identificar son menores de 4 meses o de 4 a 12 meses.");
        }
    }

    private static void ValidatePorcineAggregateMovementRequest(
        int? numberOfAnimals,
        string? breed,
        string? animalType)
    {
        if (numberOfAnimals is null or <= 0)
        {
            throw new DomainException("Debes indicar el número de animales porcinos de la guía.");
        }

        if (string.IsNullOrWhiteSpace(breed))
        {
            throw new DomainException("La raza es obligatoria en los movimientos porcinos.");
        }

        if (string.IsNullOrWhiteSpace(animalType))
        {
            throw new DomainException("El tipo de animal es obligatorio en los movimientos porcinos.");
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
            ApplyMovementToExistingAnimal(context, animal, departureDay, arrivalDay);
        }

        dbContext.MovementCertificateAnimals.AddRange(animals.Select(entity => new MovementCertificateAnimal
        {
            MovementCertificateId = movement.Id,
            AnimalId = entity.Id
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordMovementSnapshotsAsync(
            context,
            animals,
            departureDay,
            arrivalDay,
            transportName,
            vehicleRegistrationNumber,
            BuildPorcineBalanceMetadata(animals),
            cancellationToken);

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
            ApplyExternalEntryToExistingAnimal(context, animal, departureDay, arrivalDay);
            affectedAnimals.Add(animal);
        }

        var newAnimals = revalidatedRows
            .Where(entity => entity.Animal is null)
            .Select(entity => BuildNewAnimalForExternalEntry(
                context,
                entity.Row.Identification,
                sharedAnimalData!,
                departureDay,
                arrivalDay))
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
        await RecordMovementSnapshotsAsync(
            context,
            affectedAnimals,
            departureDay,
            arrivalDay,
            transportName,
            vehicleRegistrationNumber,
            sharedAnimalData?.Porcino is null
                ? BuildPorcineBalanceMetadata(affectedAnimals)
                : new PorcineBalanceMetadata(
                    sharedAnimalData.Porcino.AnimalType,
                    sharedAnimalData.Breed,
                    sharedAnimalData.Porcino.Tag),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetMovementAsyncForCommittedTransaction(movement.Id, cancellationToken);
    }

    private async Task RecordMovementSnapshotsAsync(
        MovementContext context,
        IReadOnlyCollection<Animal> movedAnimals,
        DateOnly departureDate,
        DateOnly? arrivalDate,
        string? transportName,
        string? vehicleRegistrationNumber,
        PorcineBalanceMetadata? porcineMetadata,
        CancellationToken cancellationToken)
    {
        var movementDate = arrivalDate ?? departureDate;
        var snapshots = BuildSnapshotRequests(context, movementDate);

        foreach (var snapshot in snapshots)
        {
            var balance = new Balance
            {
                LivestockFarmId = snapshot.Farm.Id,
                BalanceDate = snapshot.Date,
                DestinationLivestockCode = snapshot.DestinationCode,
                ModificationCause = snapshot.Cause,
                NumberOfAnimals = movedAnimals.Count,
                OriginLivestockCode = snapshot.OriginCode
            };

            dbContext.Balances.Add(balance);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (snapshot.Farm.LivestockSpecies == LivestockSpecies.Porcine)
            {
                dbContext.BalancePorcino.Add(BuildPorcineBalance(balance.Id, movedAnimals, snapshot.Date, porcineMetadata));
            }
            else
            {
                dbContext.BalanceOvinoCaprino.Add(BuildOvineOrCaprineBalance(
                    balance.Id,
                    movedAnimals,
                    snapshot.Date,
                    new OvineBalanceMetadata(transportName, vehicleRegistrationNumber)));
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var census = new Census
            {
                LivestockFarmId = snapshot.Farm.Id,
                CensusDate = snapshot.Date
            };

            dbContext.Census.Add(census);
            await dbContext.SaveChangesAsync(cancellationToken);

            var balanceSnapshot = await censusProjectionService.BuildSnapshotAsync(snapshot.Farm, snapshot.Date, cancellationToken);
            await BalanceSnapshotSupport.UpsertCensusSnapshotAsync(dbContext, census, snapshot.Farm, balanceSnapshot, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RecordAggregatePorcineMovementSnapshotsAsync(
        MovementContext context,
        int numberOfAnimals,
        string breed,
        string animalType,
        DateOnly departureDate,
        DateOnly? arrivalDate,
        CancellationToken cancellationToken)
    {
        var movementDate = arrivalDate ?? departureDate;
        var snapshots = BuildSnapshotRequests(context, movementDate);

        foreach (var snapshot in snapshots)
        {
            var balance = new Balance
            {
                LivestockFarmId = snapshot.Farm.Id,
                BalanceDate = snapshot.Date,
                DestinationLivestockCode = snapshot.DestinationCode,
                ModificationCause = snapshot.Cause,
                NumberOfAnimals = numberOfAnimals,
                OriginLivestockCode = snapshot.OriginCode
            };

            dbContext.Balances.Add(balance);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.BalancePorcino.Add(BuildPorcineBalance(balance.Id, numberOfAnimals, breed, animalType));
            await dbContext.SaveChangesAsync(cancellationToken);

            var census = new Census
            {
                LivestockFarmId = snapshot.Farm.Id,
                CensusDate = snapshot.Date
            };

            dbContext.Census.Add(census);
            await dbContext.SaveChangesAsync(cancellationToken);

            var balanceSnapshot = await censusProjectionService.BuildSnapshotAsync(snapshot.Farm, snapshot.Date, cancellationToken);
            await BalanceSnapshotSupport.UpsertCensusSnapshotAsync(dbContext, census, snapshot.Farm, balanceSnapshot, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ValidatePorcineAggregateAvailabilityAsync(
        MovementContext context,
        string animalType,
        int numberOfAnimals,
        DateOnly departureDate,
        DateOnly? arrivalDate,
        CancellationToken cancellationToken)
    {
        var sourceFarm = context.Direction == MovementDirection.Exit
            ? context.CurrentFarm
            : context.CounterpartyType == MovementCounterpartyType.Internal
                ? context.CounterpartyFarm
                : null;
        var destinationFarm = context.Direction == MovementDirection.Entry
            ? context.CurrentFarm
            : context.CounterpartyType == MovementCounterpartyType.Internal
                ? context.CounterpartyFarm
                : null;

        if (sourceFarm is not null)
        {
            await EnsurePorcineStockAvailabilityAsync(sourceFarm, animalType, numberOfAnimals, departureDate, cancellationToken);
        }

        if (destinationFarm is not null)
        {
            var snapshotDate = arrivalDate ?? departureDate;
            await EnsurePorcineEntryCapacityAsync(destinationFarm, animalType, numberOfAnimals, snapshotDate, cancellationToken);
        }
    }

    private async Task EnsurePorcineStockAvailabilityAsync(
        LivestockFarm farm,
        string animalType,
        int numberOfAnimals,
        DateOnly snapshotDate,
        CancellationToken cancellationToken)
    {
        var snapshot = await censusProjectionService.BuildSnapshotAsync(farm, snapshotDate, cancellationToken);
        var availableAnimals = PorcineMovementSupport.GetAvailableAnimals(snapshot, animalType);
        if (numberOfAnimals > availableAnimals)
        {
            throw new DomainException(
                $"No puedes registrar {numberOfAnimals} animales de tipo {animalType} porque el censo disponible en la explotación es {availableAnimals}.");
        }
    }

    private async Task EnsurePorcineEntryCapacityAsync(
        LivestockFarm farm,
        string animalType,
        int numberOfAnimals,
        DateOnly snapshotDate,
        CancellationToken cancellationToken)
    {
        var snapshot = await censusProjectionService.BuildSnapshotAsync(farm, snapshotDate, cancellationToken);
        PorcineCapacitySupport.EnsureWithinCapacity(farm, snapshot, animalType, numberOfAnimals);
    }

    private static IReadOnlyList<SnapshotRequest> BuildSnapshotRequests(
        MovementContext context,
        DateOnly movementDate)
    {
        var requests = new List<SnapshotRequest>();
        if (context.Direction == MovementDirection.Exit)
        {
            requests.Add(new SnapshotRequest(
                context.CurrentFarm,
                movementDate,
                context.Cause,
                context.CurrentFarm.RegaCode,
                context.CounterpartyCode));

            if (context.CounterpartyFarm is not null)
            {
                requests.Add(new SnapshotRequest(
                    context.CounterpartyFarm,
                    movementDate,
                    context.Cause,
                    context.CurrentFarm.RegaCode,
                    context.CounterpartyFarm.RegaCode));
            }

            return requests;
        }

        requests.Add(new SnapshotRequest(
            context.CurrentFarm,
            movementDate,
            context.Cause,
            context.CounterpartyCode,
            context.CurrentFarm.RegaCode));

        if (context.CounterpartyFarm is not null)
        {
            requests.Add(new SnapshotRequest(
                context.CounterpartyFarm,
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
        DateOnly? arrivalDate)
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
        animal.DischargeDate = null;
        animal.DischargeCause = null;
        animal.DestinationCode = null;
    }

    private static void ApplyExternalEntryToExistingAnimal(
        MovementContext context,
        Animal animal,
        DateOnly departureDate,
        DateOnly? arrivalDate)
    {
        animal.LivestockFarmId = context.CurrentFarm.Id;
        animal.RegistrationDate = arrivalDate ?? departureDate;
        animal.RegistrationCause = ParseRegistrationCause(context.Cause);
        animal.OriginCode = context.CounterpartyCode;
        animal.DischargeDate = null;
        animal.DischargeCause = null;
        animal.DestinationCode = null;
    }

    private static Animal BuildNewAnimalForExternalEntry(
        MovementContext context,
        string identification,
        SharedAnimalDataRequest sharedAnimalData,
        DateOnly departureDate,
        DateOnly? arrivalDate)
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

    private static string NormalizeRequiredAnimalType(string? animalType)
    {
        var normalizedType = NormalizeNullable(animalType);
        if (normalizedType is null)
        {
            throw new DomainException("El tipo de animal es obligatorio en los movimientos porcinos.");
        }

        return normalizedType;
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

    private string NormalizeDeathDestinationCode(LivestockSpecies species, string? destinationCode)
    {
        return MerCodeSupport.NormalizeDeathDestinationCode(species, destinationCode, clock.UtcNow.Year);
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
            throw new DomainException("El código REGA no es válido. Debe seguir el formato ES seguido de 12 dígitos.");
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
        int numberOfAnimals,
        string? animalType = null,
        MovementUnidentifiedCategory? unidentifiedCategory = null)
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
            AnimalType = NormalizeNullable(animalType),
            OriginExternalCode = isEntry && context.CounterpartyType == MovementCounterpartyType.External ? context.CounterpartyCode : null,
            OriginExternalName = isEntry && context.CounterpartyType == MovementCounterpartyType.External ? context.CounterpartyName : null,
            DestinationExternalCode = !isEntry && context.CounterpartyType == MovementCounterpartyType.External ? context.CounterpartyCode : null,
            DestinationExternalName = !isEntry && context.CounterpartyType == MovementCounterpartyType.External ? context.CounterpartyName : null,
            UnidentifiedCategory = unidentifiedCategory
        };
    }

    private async Task RecordUnidentifiedMovementSnapshotsAsync(
        MovementContext context,
        int numberOfAnimals,
        MovementUnidentifiedCategory unidentifiedCategory,
        DateOnly departureDate,
        DateOnly? arrivalDate,
        string? transportName,
        string? vehicleRegistrationNumber,
        CancellationToken cancellationToken)
    {
        var movementDate = arrivalDate ?? departureDate;
        var snapshots = BuildSnapshotRequests(context, movementDate);

        foreach (var snapshot in snapshots)
        {
            var balance = new Balance
            {
                LivestockFarmId = snapshot.Farm.Id,
                BalanceDate = snapshot.Date,
                DestinationLivestockCode = snapshot.DestinationCode,
                ModificationCause = snapshot.Cause,
                NumberOfAnimals = numberOfAnimals,
                OriginLivestockCode = snapshot.OriginCode
            };

            dbContext.Balances.Add(balance);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.BalanceOvinoCaprino.Add(BuildUnidentifiedOvineBalance(
                balance.Id,
                numberOfAnimals,
                unidentifiedCategory,
                transportName,
                vehicleRegistrationNumber));
            await dbContext.SaveChangesAsync(cancellationToken);

            var census = new Census
            {
                LivestockFarmId = snapshot.Farm.Id,
                CensusDate = snapshot.Date
            };

            dbContext.Census.Add(census);
            await dbContext.SaveChangesAsync(cancellationToken);

            var balanceSnapshot = await censusProjectionService.BuildSnapshotAsync(snapshot.Farm, snapshot.Date, cancellationToken);
            await BalanceSnapshotSupport.UpsertCensusSnapshotAsync(dbContext, census, snapshot.Farm, balanceSnapshot, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
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
            movement.AnimalType,
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

        var normalizedWholeLine = DomainValidators.NormalizeAnimalIdentification(normalizedLine);
        if (!string.Equals(normalizedWholeLine, normalizedLine, StringComparison.Ordinal))
        {
            return normalizedWholeLine;
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
        return DomainValidators.NormalizeAnimalIdentification(value)
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

    private static BalanceOvinoCaprino BuildOvineOrCaprineBalance(
        long balanceId,
        IReadOnlyCollection<Animal> animals,
        DateOnly asOfDate,
        OvineBalanceMetadata? metadata)
    {
        var classification = ClassifyOvineOrCaprineAnimals(animals, asOfDate);
        return new BalanceOvinoCaprino
        {
            BalanceId = balanceId,
            NonReproductiveBetween4And12Months = classification.NonReproductiveBetween4And12Months,
            NonReproductiveUnder4Months = classification.NonReproductiveUnder4Months,
            ReproductiveFemales = classification.ReproductiveFemales,
            ReproductiveMales = classification.ReproductiveMales,
            TransportTicketNumber = metadata?.TransportTicketNumber,
            TransporterName = metadata?.TransporterName
        };
    }

    private static BalanceOvinoCaprino BuildUnidentifiedOvineBalance(
        long balanceId,
        int numberOfAnimals,
        MovementUnidentifiedCategory category,
        string? transportName,
        string? vehicleRegistrationNumber)
    {
        return new BalanceOvinoCaprino
        {
            BalanceId = balanceId,
            NonReproductiveUnder4Months = category == MovementUnidentifiedCategory.Under4Months ? numberOfAnimals : 0,
            NonReproductiveBetween4And12Months = category == MovementUnidentifiedCategory.Between4And12Months ? numberOfAnimals : 0,
            ReproductiveFemales = 0,
            ReproductiveMales = 0,
            TransporterName = NormalizeNullable(transportName),
            TransportTicketNumber = NormalizeNullable(vehicleRegistrationNumber)
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

    private static BalancePorcino BuildPorcineBalance(
        long balanceId,
        IReadOnlyCollection<Animal> animals,
        DateOnly asOfDate,
        PorcineBalanceMetadata? metadata)
    {
        var classification = ClassifyPorcineAnimals(animals, asOfDate, metadata?.Type);

        return new BalancePorcino
        {
            BalanceId = balanceId,
            Baits = classification.Baits,
            Boars = classification.Boars,
            Breed = NormalizeNullable(metadata?.Breed ?? animals.Select(entity => entity.Breed).FirstOrDefault(entity => !string.IsNullOrWhiteSpace(entity))),
            Piglets = classification.Piglets,
            PigsReposition = classification.PigsReposition,
            Rear = classification.Rears,
            SowsForLive = classification.Sows,
            SowsReposition = classification.SowsReposition,
            Tag = NormalizeNullable(metadata?.Tag),
            Type = NormalizeNullable(metadata?.Type)
        };
    }

    private static BalancePorcino BuildPorcineBalance(
        long balanceId,
        int numberOfAnimals,
        string breed,
        string animalType)
    {
        var classification = PorcineMovementSupport.BuildBreakdown(animalType, numberOfAnimals);

        return new BalancePorcino
        {
            BalanceId = balanceId,
            Baits = classification.Baits,
            Boars = classification.Boars,
            Breed = NormalizeNullable(breed),
            Piglets = classification.Piglets,
            PigsReposition = classification.PigsReposition,
            Rear = classification.Rears,
            SowsForLive = classification.Sows,
            SowsReposition = classification.SowsReposition,
            Type = NormalizeNullable(animalType)
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

    private static PorcineBreakdown ClassifyPorcineAnimals(IReadOnlyCollection<Animal> animals, DateOnly asOfDate, string? fallbackType = null)
    {
        var breakdown = new PorcineBreakdown();

        foreach (var animal in animals)
        {
            var type = FarmCensusProjectionSupport.NormalizeType(animal.Porcino?.AnimalType ?? fallbackType);
            if (string.IsNullOrWhiteSpace(type))
            {
                var birthDate = FarmCensusProjectionSupport.ResolveBirthDate(animal);
                if (birthDate is not null && PorcineTransitionSupport.IsPigletStage(birthDate.Value, asOfDate))
                {
                    breakdown.Piglets++;
                }
                else if (birthDate is not null && PorcineTransitionSupport.IsIntermediateStage(birthDate.Value, asOfDate))
                {
                    breakdown.Rears++;
                }
                else
                {
                    breakdown.Baits++;
                }
                continue;
            }

            breakdown.Accumulate(PorcineMovementSupport.BuildBreakdown(type, 1));
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
        LivestockFarm Farm,
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

        public void Accumulate(PorcineMovementBreakdown breakdown)
        {
            Baits += breakdown.Baits;
            Boars += breakdown.Boars;
            Piglets += breakdown.Piglets;
            PigsReposition += breakdown.PigsReposition;
            Rears += breakdown.Rears;
            Sows += breakdown.Sows;
            SowsReposition += breakdown.SowsReposition;
        }
    }

    private static PorcineBalanceMetadata BuildPorcineBalanceMetadata(IReadOnlyCollection<Animal> animals)
    {
        var firstPorcine = animals.Select(entity => entity.Porcino).FirstOrDefault(entity => entity is not null);
        return new PorcineBalanceMetadata(
            firstPorcine?.AnimalType,
            animals.Select(entity => entity.Breed).FirstOrDefault(entity => !string.IsNullOrWhiteSpace(entity)),
            firstPorcine?.Tag);
    }
}
