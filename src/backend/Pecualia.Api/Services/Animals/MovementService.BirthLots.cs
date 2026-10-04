using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Movements;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

public sealed partial class MovementService
{
    public async Task<MovementBirthLotOptions> GetBirthLotsAsync(long userId, UserRole role,
        long farmId, DateOnly date, long? movementId, CancellationToken cancellationToken)
    {
        await EnsureAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        if (movementId != null && !await BuildAccessibleMovementQuery(userId, role)
            .AnyAsync(movement => movement.Id == movementId && movement.OriginLivestockId == farmId, cancellationToken))
            throw new DomainException("Movimiento no encontrado.");
        return await MovementBirthLotSupport.GetOptionsAsync(dbContext, farmId, date, movementId, cancellationToken);
    }

    public async Task AssignBirthLotsAsync(long userId, UserRole role, long movementId,
        AssignMovementBirthLotsRequest request, CancellationToken cancellationToken)
    {
        var movement = await BuildAccessibleMovementQuery(userId, role).Include(movement => movement.BirthLots)
            .SingleOrDefaultAsync(movement => movement.Id == movementId, cancellationToken);
        if (movement?.OriginLivestockId is not long farmId || movement.UnidentifiedCategory == null ||
            movement.Specie == LivestockSpecies.Porcine.ToString())
            throw new DomainException("La guía no es una salida de ovino o caprino sin identificar.");
        await EnsureAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await MovementBirthLotSupport.LockFarmAsync(dbContext, farmId, cancellationToken);
        // Reload after acquiring the lock, so concurrent edits cannot leave stale allocations tracked.
        dbContext.ChangeTracker.Clear();
        movement = await dbContext.MovementCertificates.Include(entity => entity.BirthLots)
            .SingleAsync(entity => entity.Id == movementId, cancellationToken);
        await MovementBirthLotSupport.ValidateAsync(dbContext, farmId, ToDateOnly(movement.DepartureDate),
            movement.NumberOfAnimals, request.BirthLots, movementId, cancellationToken);
        movement.UnidentifiedCategory = MovementUnidentifiedCategory.BirthLots;
        dbContext.MovementBirthLots.RemoveRange(movement.BirthLots);
        await dbContext.SaveChangesAsync(cancellationToken);
        movement.BirthLots = request.BirthLots.Select(lot => new MovementBirthLot
        {
            MovementCertificateId = movementId, BirthId = lot.BirthId, Quantity = lot.Quantity
        }).ToList();
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

}
