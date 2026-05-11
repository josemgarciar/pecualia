using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

internal sealed record BookBalanceMovementInfo(string? CounterpartyCode, string? GuideNumber);

internal static class BookBalanceSupport
{
    internal static IReadOnlyDictionary<long, BookBalanceMovementInfo> BuildBalanceMovementLookup(
        LivestockFarm farm,
        IReadOnlyList<Balance> balances,
        IReadOnlyList<MovementCertificate> movements)
    {
        if (balances.Count == 0 || movements.Count == 0)
        {
            return new Dictionary<long, BookBalanceMovementInfo>();
        }

        var candidates = movements
            .Where(entity => IsMovementForFarm(entity, farm.Id) && IsMovementForSpecies(entity, farm.LivestockSpecies))
            .Select(entity => new BalanceMovementCandidate(
                entity,
                (entity.ArrivalDate.HasValue
                    ? DateOnly.FromDateTime(entity.ArrivalDate.Value)
                    : DateOnly.FromDateTime(entity.DepartureDate)),
                entity.OriginLivestockId == farm.Id,
                entity.OriginLivestockId == farm.Id
                    ? BookDocumentSupport.EmptyToNull(entity.DestinationFarm?.RegaCode) ?? BookDocumentSupport.EmptyToNull(entity.DestinationExternalCode)
                    : BookDocumentSupport.EmptyToNull(entity.OriginFarm?.RegaCode) ?? BookDocumentSupport.EmptyToNull(entity.OriginExternalCode)))
            .OrderBy(entity => entity.MovementDate)
            .ThenBy(entity => entity.Movement.Id)
            .ToList();

        var usedMovementIds = new HashSet<long>();
        var lookup = new Dictionary<long, BookBalanceMovementInfo>();

        foreach (var balance in balances.OrderBy(entity => entity.BalanceDate).ThenBy(entity => entity.Id))
        {
            var match = candidates.FirstOrDefault(entity => !usedMovementIds.Contains(entity.Movement.Id) && MatchesBalance(balance, entity));
            if (match is null)
            {
                continue;
            }

            usedMovementIds.Add(match.Movement.Id);
            lookup[balance.Id] = new BookBalanceMovementInfo(
                match.CounterpartyCode,
                BookDocumentSupport.EmptyToNull(match.Movement.Serie));
        }

        return lookup;
    }

    internal static string? ResolveOvineCounterpartyCode(Balance balance, BookBalanceMovementInfo? movementInfo)
    {
        if (!string.IsNullOrWhiteSpace(movementInfo?.CounterpartyCode))
        {
            return movementInfo.CounterpartyCode;
        }

        return IsExitCause(balance.ModificationCause)
            ? BookDocumentSupport.EmptyToNull(balance.DestinationLivestockCode)
            : BookDocumentSupport.EmptyToNull(balance.OriginLivestockCode);
    }

    internal static string? ResolveOvineHealthDocumentNumber(Balance balance, BookBalanceMovementInfo? movementInfo)
    {
        return !string.IsNullOrWhiteSpace(movementInfo?.GuideNumber)
            ? movementInfo.GuideNumber
            : BookDocumentSupport.EmptyToNull(balance.HealthDocumentNumber);
    }

    private static bool IsMovementForFarm(MovementCertificate movement, long farmId)
    {
        return movement.OriginLivestockId == farmId || movement.DestinationLivestockId == farmId;
    }

    private static bool IsMovementForSpecies(MovementCertificate movement, LivestockSpecies species)
    {
        return movement.Specie.Equals(species.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesBalance(Balance balance, BalanceMovementCandidate candidate)
    {
        if (balance.BalanceDate != candidate.MovementDate || balance.NumberOfAnimals != candidate.Movement.NumberOfAnimals)
        {
            return false;
        }

        if (candidate.IsExit != IsExitCause(balance.ModificationCause))
        {
            return false;
        }

        var expectedCounterpartyCode = candidate.IsExit
            ? BookDocumentSupport.EmptyToNull(balance.DestinationLivestockCode)
            : BookDocumentSupport.EmptyToNull(balance.OriginLivestockCode);

        return expectedCounterpartyCode is null ||
               candidate.CounterpartyCode is null ||
               expectedCounterpartyCode.Equals(candidate.CounterpartyCode, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExitCause(string cause)
    {
        return cause.Equals("Salida", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record BalanceMovementCandidate(
        MovementCertificate Movement,
        DateOnly MovementDate,
        bool IsExit,
        string? CounterpartyCode);
}
