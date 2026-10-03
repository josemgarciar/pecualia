using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.FarmOperations;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

internal sealed record OvineBalanceMetadata(string? TransporterName, string? TransportTicketNumber);

internal sealed record PorcineBalanceMetadata(string? Type, string? Breed, string? Tag);

internal static class BalanceSnapshotSupport
{
    internal static async Task UpsertBalanceSnapshotAsync(
        PecualiaDbContext dbContext,
        Balance balance,
        LivestockFarm farm,
        FarmCensusResponse snapshot,
        CancellationToken cancellationToken,
        OvineBalanceMetadata? ovineMetadata = null,
        PorcineBalanceMetadata? porcineMetadata = null)
    {
        if (farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            var detail = await dbContext.BalancePorcino
                .SingleOrDefaultAsync(entity => entity.BalanceId == balance.Id, cancellationToken);

            if (detail is null)
            {
                detail = new BalancePorcino
                {
                    BalanceId = balance.Id
                };
                dbContext.BalancePorcino.Add(detail);
            }

            detail.Boars = snapshot.Boars;
            detail.SowsForLive = snapshot.SowsForLive;
            detail.SowsReposition = snapshot.SowsReposition;
            detail.PigsReposition = snapshot.MalesReposition;
            detail.Piglets = snapshot.Piglets;
            detail.Rear = snapshot.Rears;
            detail.Baits = snapshot.Baits;

            if (porcineMetadata is not null)
            {
                detail.Type = NormalizeNullable(porcineMetadata.Type);
                detail.Breed = NormalizeNullable(porcineMetadata.Breed);
                detail.Tag = NormalizeNullable(porcineMetadata.Tag);
            }

            return;
        }

        var ovineDetail = await dbContext.BalanceOvinoCaprino
            .SingleOrDefaultAsync(entity => entity.BalanceId == balance.Id, cancellationToken);

        if (ovineDetail is null)
        {
            ovineDetail = new BalanceOvinoCaprino
            {
                BalanceId = balance.Id
            };
            dbContext.BalanceOvinoCaprino.Add(ovineDetail);
        }

        ovineDetail.NonReproductiveUnder4Months = snapshot.NonReproductiveUnder4Months;
        ovineDetail.NonReproductiveBetween4And12Months = snapshot.NonReproductiveBetween4And12Months;
        ovineDetail.ReproductiveFemales = snapshot.ReproductiveFemales;
        ovineDetail.ReproductiveMales = snapshot.ReproductiveMales;

        if (ovineMetadata is not null)
        {
            ovineDetail.TransporterName = NormalizeNullable(ovineMetadata.TransporterName);
            ovineDetail.TransportTicketNumber = NormalizeNullable(ovineMetadata.TransportTicketNumber);
        }
    }

    internal static async Task UpsertCensusSnapshotAsync(
        PecualiaDbContext dbContext,
        Census census,
        LivestockFarm farm,
        FarmCensusResponse snapshot,
        CancellationToken cancellationToken)
    {
        if (farm.LivestockSpecies == LivestockSpecies.Porcine)
        {
            var detail = await dbContext.CensusPorcino
                .SingleOrDefaultAsync(entity => entity.CensusId == census.Id, cancellationToken);

            if (detail is null)
            {
                detail = new CensusPorcino
                {
                    CensusId = census.Id
                };
                dbContext.CensusPorcino.Add(detail);
            }

            detail.Boars = snapshot.Boars;
            detail.Sow = snapshot.SowsForLive;
            detail.SowsReposition = snapshot.SowsReposition;
            detail.PigsReposition = snapshot.MalesReposition;
            detail.Piglets = snapshot.Piglets;
            detail.Rears = snapshot.Rears;
            detail.Baits = snapshot.Baits;
            return;
        }

        var ovineDetail = await dbContext.CensusOvinoCaprino
            .SingleOrDefaultAsync(entity => entity.CensusId == census.Id, cancellationToken);

        if (ovineDetail is null)
        {
            ovineDetail = new CensusOvinoCaprino
            {
                CensusId = census.Id
            };
            dbContext.CensusOvinoCaprino.Add(ovineDetail);
        }

        ovineDetail.NonReproductiveUnder4Months = snapshot.NonReproductiveUnder4Months;
        ovineDetail.NonReproductiveBetween4And12Months = snapshot.NonReproductiveBetween4And12Months;
        ovineDetail.ReproductiveFemale = snapshot.ReproductiveFemales;
        ovineDetail.ReproductiveMale = snapshot.ReproductiveMales;
    }

    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
