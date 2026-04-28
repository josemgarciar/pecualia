namespace Pecualia.Api.Models.Entities;

public sealed class MovementCertificate
{
    public long Id { get; set; }

    public long OriginLivestockId { get; set; }

    public long? DestinationLivestockId { get; set; }

    public DateOnly DepartureDate { get; set; }

    public int NumberOfAnimals { get; set; }

    public string Specie { get; set; } = string.Empty;

    public string? CodRemo { get; set; }

    public DateOnly? SolicitationDate { get; set; }

    public LivestockFarm OriginFarm { get; set; } = null!;

    public LivestockFarm? DestinationFarm { get; set; }
}
