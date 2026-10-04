using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Movements;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

internal static class MovementBirthLotSupport
{
    internal static async Task LockFarmAsync(PecualiaDbContext database, long farmId, CancellationToken cancellationToken)
    {
        if (database.Database.IsNpgsql())
            await database.Farms.FromSqlInterpolated($"SELECT * FROM livestock_farm WHERE id = {farmId} FOR UPDATE")
                .ToListAsync(cancellationToken);
    }

    internal static async Task<Dictionary<long, int>> CountIndividualConsumptionAsync(
        PecualiaDbContext database, IReadOnlyList<AnimalBirth> births, DateOnly? throughDate,
        CancellationToken cancellationToken)
    {
        var farmIds = births.Select(birth => birth.LivestockFarmId).Distinct().ToArray();
        var ids = births.Select(birth => birth.Id).ToArray();
        var animals = await database.Animals.AsNoTracking().Where(animal =>
            (animal.SourceBirthId != null && ids.Contains(animal.SourceBirthId.Value) ||
             farmIds.Contains(animal.LivestockFarmId) && animal.SourceBirthId == null &&
             animal.RegistrationCause == AnimalRegistrationCause.Autorreposicion) &&
            (throughDate == null || animal.RegistrationDate <= throughDate)).ToListAsync(cancellationToken);
        var consumption = new Dictionary<long, int>();
        foreach (var animal in animals)
        {
            var birthId = animal.SourceBirthId;
            if (birthId == null && animal.BirthDate != null)
            {
                var matches = births.Where(birth => birth.LivestockFarmId == animal.LivestockFarmId &&
                    birth.BirthDate == animal.BirthDate).Take(2).ToList();
                if (matches.Count == 1) birthId = matches[0].Id;
            }
            if (birthId is long id) consumption[id] = consumption.GetValueOrDefault(id) + 1;
        }
        return consumption;
    }

    internal static async Task<MovementBirthLotOptions> GetOptionsAsync(PecualiaDbContext database,
        long farmId, DateOnly date, long? excludedMovementId, CancellationToken cancellationToken)
    {
        var births = await database.AnimalBirths.AsNoTracking()
            .Where(birth => birth.LivestockFarmId == farmId && birth.BirthDate <= date && birth.BirthDate.AddMonths(12) > date)
            .OrderBy(birth => birth.BirthDate).ThenBy(birth => birth.Id).ToListAsync(cancellationToken);
        // Reserve all recorded uses, including later guides: backdated entries must not oversell a lot.
        var consumed = await CountIndividualConsumptionAsync(database, births, null, cancellationToken);
        var ids = births.Select(birth => birth.Id).ToArray();
        var exits = await database.MovementBirthLots.AsNoTracking()
            .Where(lot => ids.Contains(lot.BirthId) && lot.MovementCertificateId != excludedMovementId)
            .GroupBy(lot => lot.BirthId).Select(group => new { Id = group.Key, Quantity = group.Sum(lot => lot.Quantity) })
            .ToDictionaryAsync(item => item.Id, item => item.Quantity, cancellationToken);
        var unresolved = await database.MovementCertificates.AsNoTracking().CountAsync(movement =>
            movement.OriginLivestockId == farmId && movement.UnidentifiedCategory != null &&
            movement.Id != excludedMovementId && !movement.BirthLots.Any(), cancellationToken);
        return new MovementBirthLotOptions(births.Select(birth => new MovementBirthLotOption(
            birth.Id, birth.BirthDate, birth.OffspringNumber,
            Math.Max(0, birth.OffspringNumber - consumed.GetValueOrDefault(birth.Id) - exits.GetValueOrDefault(birth.Id)),
            CategoryAt(birth.BirthDate, date).ToString())).ToList(), unresolved);
    }

    internal static MovementUnidentifiedCategory CategoryAt(DateOnly birthDate, DateOnly date) =>
        birthDate.AddMonths(4) > date ? MovementUnidentifiedCategory.Under4Months : MovementUnidentifiedCategory.Between4And12Months;

    internal static void ApplyBalanceBreakdown(BalanceOvinoCaprino detail,
        IEnumerable<MovementBirthLot> lots, DateOnly date)
    {
        detail.NonReproductiveUnder4Months = 0;
        detail.NonReproductiveBetween4And12Months = 0;
        detail.ReproductiveFemales = 0;
        detail.ReproductiveMales = 0;
        foreach (var lot in lots)
        {
            if (CategoryAt(lot.Birth.BirthDate, date) == MovementUnidentifiedCategory.Under4Months)
                detail.NonReproductiveUnder4Months += lot.Quantity;
            else
                detail.NonReproductiveBetween4And12Months += lot.Quantity;
        }
    }

    internal static async Task ValidateAsync(PecualiaDbContext database, long farmId, DateOnly date,
        int total, IReadOnlyList<MovementBirthLotRequest>? selections,
        long? excludedMovementId, CancellationToken cancellationToken)
    {
        if (selections is not { Count: > 0 } || selections.Any(lot => lot.Quantity <= 0) ||
            selections.Select(lot => lot.BirthId).Distinct().Count() != selections.Count ||
            selections.Sum(lot => (long)lot.Quantity) != total)
            throw new DomainException("Selecciona los lotes de nacimiento y las cantidades de la guía.");

        var options = await GetOptionsAsync(database, farmId, date, excludedMovementId, cancellationToken);
        if (excludedMovementId == null && options.UnallocatedMovements > 0)
            throw new DomainException("Asigna primero los lotes de las salidas anteriores sin vincular para conocer las existencias disponibles.");
        foreach (var selected in selections)
        {
            var lot = options.Lots.SingleOrDefault(lot => lot.BirthId == selected.BirthId);
            if (lot == null || selected.Quantity > lot.Available)
                throw new DomainException("El lote no pertenece a la explotación, no tiene la edad indicada o no dispone de suficientes animales.");
        }
    }
}
