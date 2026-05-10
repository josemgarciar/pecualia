using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Books;
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

    private IQueryable<LivestockFarm> BuildAccessibleFarmQuery(long userId, UserRole role)
    {
        return role == UserRole.Manager
            ? dbContext.Farms.Where(entity => entity.Farmer.ManagerId == userId)
            : dbContext.Farms.Where(entity => entity.FarmerId == userId);
    }
}
