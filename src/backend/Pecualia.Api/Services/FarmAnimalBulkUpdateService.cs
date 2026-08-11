using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pecualia.Api.Contracts.Animals;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

public interface IFarmAnimalBulkUpdateService
{
    Task<AnimalBulkUpdatePreviewResponse> PreviewAsync(
        long userId,
        UserRole role,
        long farmId,
        PreviewAnimalBulkUpdateRequest request,
        CancellationToken cancellationToken);

    Task<AnimalBulkUpdateCommitResponse> CommitAsync(
        long userId,
        UserRole role,
        long farmId,
        CommitAnimalBulkUpdateRequest request,
        CancellationToken cancellationToken);
}

public sealed class FarmAnimalBulkUpdateService(
    PecualiaDbContext dbContext,
    IClock clock) : IFarmAnimalBulkUpdateService
{
    private const int MaximumAnimals = 10_000;
    private const string OperationCompleted = "Completed";
    private const string OperationProcessing = "Processing";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<AnimalBulkUpdatePreviewResponse> PreviewAsync(
        long userId,
        UserRole role,
        long farmId,
        PreviewAnimalBulkUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new DomainException("La solicitud de previsualización está incompleta.");
        }

        ValidateDefinition(request.Changes);
        if (request.Selection is null)
        {
            throw new DomainException("La selección de animales es obligatoria.");
        }

        var farm = await GetAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        var animalIds = await ResolveSelectionAsync(userId, role, farmId, request.Selection, cancellationToken);
        return await EvaluateAsync(userId, role, farm, animalIds, request.Changes, false, cancellationToken);
    }

    public async Task<AnimalBulkUpdateCommitResponse> CommitAsync(
        long userId,
        UserRole role,
        long farmId,
        CommitAnimalBulkUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new DomainException("La solicitud de modificación está incompleta.");
        }

        ValidateDefinition(request.Changes);
        if (request.OperationId == Guid.Empty)
        {
            throw new DomainException("El identificador de la operación es obligatorio.");
        }

        var animalIds = NormalizeConcreteIds(request.AnimalIds);
        if (string.IsNullOrWhiteSpace(request.StateFingerprint))
        {
            throw new DomainException("La huella de la previsualización es obligatoria.");
        }

        var requestHash = ComputeHash(JsonSerializer.Serialize(new
        {
            farmId,
            AnimalIds = animalIds,
            request.StateFingerprint,
            request.Changes
        }, JsonOptions));

        var replay = await TryGetReplayAsync(userId, farmId, request.OperationId, requestHash, cancellationToken);
        if (replay is not null)
        {
            return replay with { Replayed = true };
        }

        IDbContextTransaction? transaction = null;
        try
        {
            if (dbContext.Database.IsRelational())
            {
                transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            }

            dbContext.AnimalBulkUpdateOperations.Add(new AnimalBulkUpdateOperation
            {
                Id = request.OperationId,
                UserId = userId,
                FarmId = farmId,
                RequestHash = requestHash,
                State = OperationProcessing,
                CreatedAt = clock.UtcNow
            });
            if (dbContext.Database.IsRelational())
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var farm = await GetAccessibleFarmAsync(userId, role, farmId, cancellationToken);
            var preview = await EvaluateAsync(userId, role, farm, animalIds, request.Changes, true, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(preview.StateFingerprint),
                    Encoding.UTF8.GetBytes(request.StateFingerprint)))
            {
                throw new DomainException("La previsualización ha quedado obsoleta. Vuelve a previsualizar antes de guardar.");
            }

            if (preview.ConflictAnimals > 0)
            {
                throw new DomainException("La operación contiene conflictos y no se ha modificado ningún animal.");
            }

            var animals = await dbContext.Animals
                .Where(entity => animalIds.Contains(entity.Id) && entity.LivestockFarmId == farmId)
                .ToListAsync(cancellationToken);
            if (animals.Count != animalIds.Count)
            {
                throw new DomainException("La selección contiene animales que ya no están disponibles en esta explotación.");
            }

            foreach (var animal in animals)
            {
                ApplyChanges(animal, request.Changes);
            }

            var guideResult = await ApplyGuideAsync(userId, role, farm, animals, request.Changes.Guide, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var result = new AnimalBulkUpdateCommitResponse(
                request.OperationId,
                animals.Count,
                guideResult.Linked,
                guideResult.Unlinked,
                guideResult.MovementId,
                request.Changes.Guide.Action.ToString(),
                false);

            var operation = await dbContext.AnimalBulkUpdateOperations
                .SingleAsync(entity => entity.Id == request.OperationId, cancellationToken);
            operation.State = OperationCompleted;
            operation.ResultJson = JsonSerializer.Serialize(result, JsonOptions);
            operation.CompletedAt = clock.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return result;
        }
        catch (DbUpdateException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            dbContext.ChangeTracker.Clear();
            var concurrentReplay = await TryGetReplayAsync(
                userId,
                farmId,
                request.OperationId,
                requestHash,
                cancellationToken);
            if (concurrentReplay is not null)
            {
                return concurrentReplay with { Replayed = true };
            }

            throw new DomainException("Los datos cambiaron mientras se guardaba la operación. Vuelve a previsualizar antes de reintentarlo.");
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<AnimalBulkUpdatePreviewResponse> EvaluateAsync(
        long userId,
        UserRole role,
        LivestockFarm farm,
        IReadOnlyList<long> animalIds,
        BulkAnimalUpdateDefinition changes,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Animals.Where(entity =>
            entity.LivestockFarmId == farm.Id && animalIds.Contains(entity.Id));
        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        var animals = await query
            .OrderBy(entity => entity.Identification)
            .ToListAsync(cancellationToken);
        if (animals.Count != animalIds.Count)
        {
            throw new DomainException("La selección contiene animales que no pertenecen a esta explotación.");
        }

        var links = await dbContext.MovementCertificateAnimals
            .AsNoTracking()
            .Where(entity => animalIds.Contains(entity.AnimalId))
            .Include(entity => entity.MovementCertificate)
            .ToListAsync(cancellationToken);

        var guideResolution = await ResolveGuideAsync(userId, role, farm, changes.Guide, cancellationToken);
        var rows = new List<AnimalBulkUpdateRowResponse>(animals.Count);
        foreach (var animal in animals)
        {
            var registrationCause = ResolveValue(animal.RegistrationCause, changes.RegistrationCause);
            var registrationDate = ResolveValue(animal.RegistrationDate, changes.RegistrationDate);
            var dischargeCause = ResolveValue(animal.DischargeCause, changes.DischargeCause);
            var dischargeDate = ResolveValue(animal.DischargeDate, changes.DischargeDate);
            var errors = ValidateResult(
                registrationCause,
                registrationDate,
                dischargeCause,
                dischargeDate,
                changes.Guide);

            rows.Add(new AnimalBulkUpdateRowResponse(
                animal.Id,
                animal.Identification,
                animal.RegistrationCause?.ToString(),
                animal.RegistrationDate,
                animal.DischargeCause?.ToString(),
                animal.DischargeDate,
                registrationCause?.ToString(),
                registrationDate,
                dischargeCause?.ToString(),
                dischargeDate,
                errors.Count == 0,
                errors.Count == 0 ? null : string.Join(" ", errors)));
        }

        var fingerprintSource = JsonSerializer.Serialize(new
        {
            FarmId = farm.Id,
            Animals = animals.OrderBy(entity => entity.Id).Select(entity => new
            {
                entity.Id,
                entity.LivestockFarmId,
                entity.RegistrationCause,
                entity.RegistrationDate,
                entity.DischargeCause,
                entity.DischargeDate
            }),
            Links = links.OrderBy(entity => entity.AnimalId).ThenBy(entity => entity.MovementCertificateId).Select(entity => new
            {
                entity.AnimalId,
                entity.MovementCertificateId,
                entity.MovementCertificate.OriginLivestockId,
                entity.MovementCertificate.DestinationLivestockId,
                entity.MovementCertificate.CodRemo,
                entity.MovementCertificate.Serie,
                entity.MovementCertificate.DepartureDate,
                entity.MovementCertificate.ArrivalDate,
                entity.MovementCertificate.Status
            }),
            Guide = guideResolution.Fingerprint,
            Changes = changes
        }, JsonOptions);

        return new AnimalBulkUpdatePreviewResponse(
            animalIds,
            ComputeHash(fingerprintSource),
            rows,
            new AnimalBulkGuidePreviewResponse(
                changes.Guide.Action.ToString(),
                guideResolution.Movement?.Id,
                guideResolution.Resolution,
                animalIds.Count),
            rows.Count,
            rows.Count(entity => entity.IsValid),
            rows.Count(entity => !entity.IsValid));
    }

    private async Task<IReadOnlyList<long>> ResolveSelectionAsync(
        long userId,
        UserRole role,
        long farmId,
        BulkAnimalSelectionRequest selection,
        CancellationToken cancellationToken)
    {
        IQueryable<Animal> query = BuildAccessibleAnimalQuery(userId, role)
            .Where(entity => entity.LivestockFarmId == farmId);

        if (selection.Mode == BulkAnimalSelectionMode.Explicit)
        {
            var ids = NormalizeConcreteIds(selection.AnimalIds ?? Array.Empty<long>());
            query = query.Where(entity => ids.Contains(entity.Id));
            var resolved = await query.OrderBy(entity => entity.Id).Select(entity => entity.Id).ToListAsync(cancellationToken);
            if (resolved.Count != ids.Count)
            {
                throw new DomainException("La selección contiene animales que no pertenecen a esta explotación.");
            }

            return resolved;
        }

        if (selection.Mode != BulkAnimalSelectionMode.Filtered)
        {
            throw new DomainException("El modo de selección no es válido.");
        }

        if (selection.MovementId is not null)
        {
            query = query.Where(entity => dbContext.MovementCertificateAnimals.Any(link =>
                link.AnimalId == entity.Id &&
                link.MovementCertificateId == selection.MovementId.Value &&
                (link.MovementCertificate.OriginLivestockId == farmId ||
                 link.MovementCertificate.DestinationLivestockId == farmId)));
        }

        if (!string.IsNullOrWhiteSpace(selection.Search))
        {
            var search = selection.Search.Trim().ToLowerInvariant();
            query = query.Where(entity =>
                entity.Identification.ToLower().Contains(search) ||
                (entity.Breed != null && entity.Breed.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(selection.Sex))
        {
            var sex = selection.Sex.Trim();
            query = query.Where(entity => entity.Sex == sex);
        }

        if (!string.IsNullOrWhiteSpace(selection.Status))
        {
            query = selection.Status.Trim().Equals("Discharged", StringComparison.OrdinalIgnoreCase)
                ? query.Where(entity => entity.DischargeDate != null)
                : query.Where(entity => entity.DischargeDate == null);
        }

        var excluded = (selection.ExcludedAnimalIds ?? Array.Empty<long>()).Distinct().ToArray();
        if (excluded.Length > 0)
        {
            query = query.Where(entity => !excluded.Contains(entity.Id));
        }

        var selected = await query.OrderBy(entity => entity.Id).Select(entity => entity.Id)
            .Take(MaximumAnimals + 1)
            .ToListAsync(cancellationToken);
        return NormalizeConcreteIds(selected);
    }

    private async Task<GuideResolution> ResolveGuideAsync(
        long userId,
        UserRole role,
        LivestockFarm farm,
        BulkAnimalGuideRequest guide,
        CancellationToken cancellationToken)
    {
        if (guide.Action is BulkGuideAction.Unchanged or BulkGuideAction.ClearLatestEntry or BulkGuideAction.ClearLatestExit)
        {
            return new GuideResolution(null, guide.Action == BulkGuideAction.Unchanged ? "Sin cambios" : "Desvincular última guía", guide.Action.ToString());
        }

        ValidateGuideRequest(guide);
        LivestockFarm? counterparty = null;
        if (guide.CounterpartyType == MovementCounterpartyType.Internal)
        {
            counterparty = await BuildAccessibleFarmQuery(userId, role)
                .AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.Id == guide.CounterpartyFarmId, cancellationToken);
            if (counterparty is null)
            {
                throw new DomainException("La explotación contraparte no está disponible.");
            }

            if (counterparty.Id == farm.Id || counterparty.LivestockSpecies != farm.LivestockSpecies)
            {
                throw new DomainException("La contraparte debe ser otra explotación accesible de la misma especie.");
            }
        }

        var normalizedRemo = NormalizeRequired(guide.CodRemo);
        var normalizedSerie = NormalizeRequired(guide.Serie);
        var isEntry = guide.Action == BulkGuideAction.SetEntry;
        var existing = await dbContext.MovementCertificates
            .AsNoTracking()
            .Where(entity => isEntry
                ? entity.DestinationLivestockId == farm.Id
                : entity.OriginLivestockId == farm.Id)
            .ToListAsync(cancellationToken);
        existing = existing
            .Where(entity =>
                string.Equals(Normalize(entity.CodRemo), normalizedRemo, StringComparison.Ordinal) &&
                string.Equals(Normalize(entity.Serie), normalizedSerie, StringComparison.Ordinal))
            .ToList();

        if (existing.Count > 1)
        {
            throw new DomainException("Existen varias guías con el mismo REMO y serie; corrige la duplicidad antes de continuar.");
        }

        var movement = existing.SingleOrDefault();
        if (movement is not null && !GuideMatches(movement, farm, counterparty, guide))
        {
            throw new DomainException("Ya existe una guía con ese REMO y serie, pero sus datos no coinciden.");
        }

        var fingerprint = JsonSerializer.Serialize(new
        {
            Existing = movement is null ? null : new
            {
                movement.Id,
                movement.Status,
                movement.NumberOfAnimals,
                movement.OriginLivestockId,
                movement.DestinationLivestockId,
                movement.OriginExternalCode,
                movement.OriginExternalName,
                movement.DestinationExternalCode,
                movement.DestinationExternalName
            },
            CounterpartyId = counterparty?.Id
        }, JsonOptions);
        var resolution = movement is null
            ? "Crear guía confirmada"
            : movement.Status == MovementStatus.Pending
                ? "Reutilizar y confirmar guía pendiente"
                : "Reutilizar guía confirmada";
        return new GuideResolution(movement, resolution, fingerprint);
    }

    private async Task<GuideApplyResult> ApplyGuideAsync(
        long userId,
        UserRole role,
        LivestockFarm farm,
        IReadOnlyList<Animal> animals,
        BulkAnimalGuideRequest guide,
        CancellationToken cancellationToken)
    {
        if (guide.Action == BulkGuideAction.Unchanged)
        {
            return new GuideApplyResult(null, 0, 0);
        }

        var animalIds = animals.Select(entity => entity.Id).ToArray();
        if (guide.Action is BulkGuideAction.ClearLatestEntry or BulkGuideAction.ClearLatestExit)
        {
            var isEntry = guide.Action == BulkGuideAction.ClearLatestEntry;
            var links = await dbContext.MovementCertificateAnimals
                .Where(entity => animalIds.Contains(entity.AnimalId))
                .Include(entity => entity.MovementCertificate)
                .ToListAsync(cancellationToken);
            var latestLinks = links
                .Where(entity => isEntry
                    ? entity.MovementCertificate.DestinationLivestockId == farm.Id
                    : entity.MovementCertificate.OriginLivestockId == farm.Id)
                .GroupBy(entity => entity.AnimalId)
                .Select(group => group
                    .OrderByDescending(entity => entity.MovementCertificate.DepartureDate)
                    .ThenByDescending(entity => entity.MovementCertificateId)
                    .First())
                .ToList();
            dbContext.MovementCertificateAnimals.RemoveRange(latestLinks);
            return new GuideApplyResult(null, 0, latestLinks.Count);
        }

        var resolution = await ResolveGuideAsync(userId, role, farm, guide, cancellationToken);
        var movement = resolution.Movement is null
            ? await CreateGuideAsync(userId, role, farm, animals.Count, guide, cancellationToken)
            : await dbContext.MovementCertificates.SingleAsync(entity => entity.Id == resolution.Movement.Id, cancellationToken);
        movement.Status = MovementStatus.Confirmed;

        var existingLinks = await dbContext.MovementCertificateAnimals
            .Where(entity => entity.MovementCertificateId == movement.Id && animalIds.Contains(entity.AnimalId))
            .Select(entity => entity.AnimalId)
            .ToListAsync(cancellationToken);
        var newLinks = animalIds
            .Except(existingLinks)
            .Select(animalId => new MovementCertificateAnimal
            {
                MovementCertificateId = movement.Id,
                AnimalId = animalId
            })
            .ToList();
        dbContext.MovementCertificateAnimals.AddRange(newLinks);
        var totalLinked = await dbContext.MovementCertificateAnimals
            .CountAsync(entity => entity.MovementCertificateId == movement.Id, cancellationToken);
        movement.NumberOfAnimals = Math.Max(1, totalLinked + newLinks.Count);
        return new GuideApplyResult(movement.Id, newLinks.Count, 0);
    }

    private async Task<MovementCertificate> CreateGuideAsync(
        long userId,
        UserRole role,
        LivestockFarm farm,
        int animalCount,
        BulkAnimalGuideRequest guide,
        CancellationToken cancellationToken)
    {
        LivestockFarm? counterparty = null;
        if (guide.CounterpartyType == MovementCounterpartyType.Internal)
        {
            counterparty = await BuildAccessibleFarmQuery(userId, role)
                .SingleAsync(entity => entity.Id == guide.CounterpartyFarmId, cancellationToken);
        }

        var isEntry = guide.Action == BulkGuideAction.SetEntry;
        var externalCode = guide.CounterpartyType == MovementCounterpartyType.External
            ? DomainValidators.NormalizeRegaCode(guide.CounterpartyExternalCode!)
            : null;
        var movement = new MovementCertificate
        {
            OriginLivestockId = isEntry ? counterparty?.Id : farm.Id,
            DestinationLivestockId = isEntry ? farm.Id : counterparty?.Id,
            OriginExternalCode = isEntry ? externalCode : null,
            OriginExternalName = isEntry && guide.CounterpartyType == MovementCounterpartyType.External ? guide.CounterpartyExternalName!.Trim() : null,
            DestinationExternalCode = !isEntry ? externalCode : null,
            DestinationExternalName = !isEntry && guide.CounterpartyType == MovementCounterpartyType.External ? guide.CounterpartyExternalName!.Trim() : null,
            CodRemo = NormalizeRequired(guide.CodRemo),
            Serie = NormalizeRequired(guide.Serie),
            DepartureDate = guide.DepartureDate!.Value,
            ArrivalDate = guide.ArrivalDate,
            SolicitationDate = guide.SolicitationDate,
            MeansOfTransport = EmptyToNull(guide.MeansOfTransport),
            TransportName = EmptyToNull(guide.TransportName),
            VehicleRegistrationNumber = EmptyToNull(guide.VehicleRegistrationNumber),
            NumberOfAnimals = animalCount,
            Specie = farm.LivestockSpecies.ToString(),
            Status = MovementStatus.Confirmed
        };
        dbContext.MovementCertificates.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);
        return movement;
    }

    private static void ApplyChanges(Animal animal, BulkAnimalUpdateDefinition changes)
    {
        animal.RegistrationCause = ResolveValue(animal.RegistrationCause, changes.RegistrationCause);
        animal.RegistrationDate = ResolveValue(animal.RegistrationDate, changes.RegistrationDate);
        animal.DischargeCause = ResolveValue(animal.DischargeCause, changes.DischargeCause);
        animal.DischargeDate = ResolveValue(animal.DischargeDate, changes.DischargeDate);
        if (animal.DischargeCause is null && animal.DischargeDate is null)
        {
            animal.DestinationCode = null;
        }
    }

    private static List<string> ValidateResult(
        AnimalRegistrationCause? registrationCause,
        DateOnly? registrationDate,
        AnimalDischargeCause? dischargeCause,
        DateOnly? dischargeDate,
        BulkAnimalGuideRequest guide)
    {
        var errors = new List<string>();
        if ((registrationCause is null) != (registrationDate is null))
        {
            errors.Add("La causa y la fecha de alta deben informarse o borrarse juntas.");
        }

        if ((dischargeCause is null) != (dischargeDate is null))
        {
            errors.Add("La causa y la fecha de baja deben informarse o borrarse juntas.");
        }

        if (guide.Action == BulkGuideAction.SetEntry &&
            (registrationCause != AnimalRegistrationCause.Entrada ||
             registrationDate != DateOnly.FromDateTime(guide.ArrivalDate!.Value)))
        {
            errors.Add("La guía de entrada exige causa Entrada y la fecha de llegada como fecha de alta.");
        }

        if (guide.Action == BulkGuideAction.SetExit &&
            (dischargeCause != AnimalDischargeCause.Salida ||
             dischargeDate != DateOnly.FromDateTime(guide.DepartureDate!.Value)))
        {
            errors.Add("La guía de salida exige causa Salida y la fecha de salida de la guía como fecha de baja.");
        }

        return errors;
    }

    private static T? ResolveValue<T>(T? current, BulkRegistrationCauseChange change) where T : struct =>
        change.Mode switch
        {
            BulkFieldChangeMode.Unchanged => current,
            BulkFieldChangeMode.Set => (T?)(object?)change.Value,
            BulkFieldChangeMode.Clear => null,
            _ => throw new DomainException("El modo de cambio no es válido.")
        };

    private static T? ResolveValue<T>(T? current, BulkDischargeCauseChange change) where T : struct =>
        change.Mode switch
        {
            BulkFieldChangeMode.Unchanged => current,
            BulkFieldChangeMode.Set => (T?)(object?)change.Value,
            BulkFieldChangeMode.Clear => null,
            _ => throw new DomainException("El modo de cambio no es válido.")
        };

    private static DateOnly? ResolveValue(DateOnly? current, BulkDateChange change) =>
        change.Mode switch
        {
            BulkFieldChangeMode.Unchanged => current,
            BulkFieldChangeMode.Set => change.Value,
            BulkFieldChangeMode.Clear => null,
            _ => throw new DomainException("El modo de cambio no es válido.")
        };

    private static void ValidateDefinition(BulkAnimalUpdateDefinition? changes)
    {
        if (changes is null ||
            changes.RegistrationCause is null ||
            changes.DischargeCause is null ||
            changes.RegistrationDate is null ||
            changes.DischargeDate is null ||
            changes.Guide is null)
        {
            throw new DomainException("La definición de cambios está incompleta.");
        }

        ValidateSetValue(changes.RegistrationCause.Mode, changes.RegistrationCause.Value, "causa de alta");
        ValidateSetValue(changes.DischargeCause.Mode, changes.DischargeCause.Value, "causa de baja");
        ValidateSetValue(changes.RegistrationDate.Mode, changes.RegistrationDate.Value, "fecha de alta");
        ValidateSetValue(changes.DischargeDate.Mode, changes.DischargeDate.Value, "fecha de baja");
        if (changes.RegistrationCause.Mode == BulkFieldChangeMode.Unchanged &&
            changes.DischargeCause.Mode == BulkFieldChangeMode.Unchanged &&
            changes.RegistrationDate.Mode == BulkFieldChangeMode.Unchanged &&
            changes.DischargeDate.Mode == BulkFieldChangeMode.Unchanged &&
            changes.Guide.Action == BulkGuideAction.Unchanged)
        {
            throw new DomainException("Selecciona al menos un dato para modificar.");
        }

        if (changes.Guide.Action is BulkGuideAction.SetEntry or BulkGuideAction.SetExit)
        {
            ValidateGuideRequest(changes.Guide);
        }
    }

    private static void ValidateSetValue<T>(BulkFieldChangeMode mode, T? value, string field) where T : struct
    {
        if (!Enum.IsDefined(mode))
        {
            throw new DomainException($"El modo de la {field} no es válido.");
        }

        if (mode == BulkFieldChangeMode.Set && value is null)
        {
            throw new DomainException($"Indica un valor para la {field}.");
        }
    }

    private static void ValidateGuideRequest(BulkAnimalGuideRequest guide)
    {
        if (guide.Action is not (BulkGuideAction.SetEntry or BulkGuideAction.SetExit))
        {
            return;
        }

        if (guide.CounterpartyType is null || !Enum.IsDefined(guide.CounterpartyType.Value))
        {
            throw new DomainException("Selecciona el tipo de contraparte de la guía.");
        }

        _ = NormalizeRequired(guide.CodRemo);
        _ = NormalizeRequired(guide.Serie);
        if (guide.DepartureDate is null || guide.ArrivalDate is null)
        {
            throw new DomainException("Las fechas de salida y llegada de la guía son obligatorias.");
        }

        if (guide.ArrivalDate < guide.DepartureDate)
        {
            throw new DomainException("La llegada de la guía no puede ser anterior a la salida.");
        }

        if (guide.SolicitationDate > guide.DepartureDate)
        {
            throw new DomainException("La solicitud de la guía no puede ser posterior a la salida.");
        }

        if (guide.CounterpartyType == MovementCounterpartyType.Internal && guide.CounterpartyFarmId is null)
        {
            throw new DomainException("Selecciona la explotación contraparte.");
        }

        if (guide.CounterpartyType == MovementCounterpartyType.External)
        {
            if (string.IsNullOrWhiteSpace(guide.CounterpartyExternalName))
            {
                throw new DomainException("Indica el nombre de la contraparte externa.");
            }

            if (string.IsNullOrWhiteSpace(guide.CounterpartyExternalCode) ||
                !DomainValidators.IsValidRegaCode(guide.CounterpartyExternalCode))
            {
                throw new DomainException("El código REGA externo debe tener formato ES seguido de 12 dígitos.");
            }
        }
    }

    private static bool GuideMatches(
        MovementCertificate movement,
        LivestockFarm farm,
        LivestockFarm? counterparty,
        BulkAnimalGuideRequest guide)
    {
        var isEntry = guide.Action == BulkGuideAction.SetEntry;
        var externalCode = guide.CounterpartyType == MovementCounterpartyType.External
            ? DomainValidators.NormalizeRegaCode(guide.CounterpartyExternalCode!)
            : null;
        return movement.Specie.Equals(farm.LivestockSpecies.ToString(), StringComparison.OrdinalIgnoreCase) &&
               movement.OriginLivestockId == (isEntry ? counterparty?.Id : farm.Id) &&
               movement.DestinationLivestockId == (isEntry ? farm.Id : counterparty?.Id) &&
               StringEquals(movement.OriginExternalCode, isEntry ? externalCode : null) &&
               StringEquals(movement.OriginExternalName, isEntry && externalCode is not null ? guide.CounterpartyExternalName : null) &&
               StringEquals(movement.DestinationExternalCode, !isEntry ? externalCode : null) &&
               StringEquals(movement.DestinationExternalName, !isEntry && externalCode is not null ? guide.CounterpartyExternalName : null) &&
               movement.DepartureDate == guide.DepartureDate &&
               movement.ArrivalDate == guide.ArrivalDate &&
               movement.SolicitationDate == guide.SolicitationDate &&
               StringEquals(movement.MeansOfTransport, guide.MeansOfTransport) &&
               StringEquals(movement.TransportName, guide.TransportName) &&
               StringEquals(movement.VehicleRegistrationNumber, guide.VehicleRegistrationNumber);
    }

    private async Task<AnimalBulkUpdateCommitResponse?> TryGetReplayAsync(
        long userId,
        long farmId,
        Guid operationId,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var operation = await dbContext.AnimalBulkUpdateOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == operationId, cancellationToken);
        if (operation is null)
        {
            return null;
        }

        if (operation.UserId != userId || operation.FarmId != farmId || operation.RequestHash != requestHash)
        {
            throw new DomainException("El identificador de operación ya se utilizó con otra petición.");
        }

        if (operation.State != OperationCompleted || string.IsNullOrWhiteSpace(operation.ResultJson))
        {
            throw new DomainException("La misma operación ya está siendo procesada. Espera unos instantes y vuelve a consultar.");
        }

        return JsonSerializer.Deserialize<AnimalBulkUpdateCommitResponse>(operation.ResultJson, JsonOptions)
            ?? throw new DomainException("No se pudo recuperar el resultado de la operación ya completada.");
    }

    private async Task<LivestockFarm> GetAccessibleFarmAsync(
        long userId,
        UserRole role,
        long farmId,
        CancellationToken cancellationToken) =>
        await BuildAccessibleFarmQuery(userId, role)
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == farmId, cancellationToken)
        ?? throw new DomainException("Explotación no encontrada.");

    private IQueryable<LivestockFarm> BuildAccessibleFarmQuery(long userId, UserRole role) =>
        role == UserRole.Manager
            ? dbContext.Farms.Where(entity => entity.Farmer.ManagerId == userId)
            : dbContext.Farms.Where(entity => entity.FarmerId == userId);

    private IQueryable<Animal> BuildAccessibleAnimalQuery(long userId, UserRole role) =>
        role == UserRole.Manager
            ? dbContext.Animals.Where(entity => entity.LivestockFarm.Farmer.ManagerId == userId)
            : dbContext.Animals.Where(entity => entity.LivestockFarmId != 0 && entity.LivestockFarm.FarmerId == userId);

    private static IReadOnlyList<long> NormalizeConcreteIds(IEnumerable<long>? ids)
    {
        var normalized = (ids ?? Array.Empty<long>())
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        if (normalized.Count == 0)
        {
            throw new DomainException("Selecciona al menos un animal.");
        }

        if (normalized.Count > MaximumAnimals)
        {
            throw new DomainException($"La modificación masiva admite un máximo de {MaximumAnimals:N0} animales.");
        }

        return normalized;
    }

    private static string NormalizeRequired(string? value)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException("El REMO y la serie de la guía son obligatorios.");
        }

        return normalized;
    }

    private static string Normalize(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool StringEquals(string? left, string? right) =>
        string.Equals(EmptyToNull(left), EmptyToNull(right), StringComparison.OrdinalIgnoreCase);

    private static string ComputeHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record GuideResolution(MovementCertificate? Movement, string Resolution, string Fingerprint);

    private sealed record GuideApplyResult(long? MovementId, int Linked, int Unlinked);
}
