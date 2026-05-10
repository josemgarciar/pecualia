namespace Pecualia.Api.Models.Entities;

public sealed class MovementCertificate
{
    public long Id { get; set; }

    public long? OriginLivestockId { get; set; }

    public long? DestinationLivestockId { get; set; }

    public DateTime? ArrivalDate { get; set; }

    public DateTime DepartureDate { get; set; }

    public Models.Enums.MovementStatus Status { get; set; }

    public string? MeansOfTransport { get; set; }

    public int NumberOfAnimals { get; set; }

    public string Specie { get; set; } = string.Empty;

    public string? CodRemo { get; set; }

    public string? Serie { get; set; }

    public DateTime? SolicitationDate { get; set; }

    public string? TransportName { get; set; }

    public string? VehicleRegistrationNumber { get; set; }

    public string? OriginExternalCode { get; set; }

    public string? OriginExternalName { get; set; }

    public string? DestinationExternalCode { get; set; }

    public string? DestinationExternalName { get; set; }

    public LivestockFarm? OriginFarm { get; set; }

    public LivestockFarm? DestinationFarm { get; set; }

    public ICollection<MovementCertificateAnimal> Animals { get; set; } = new List<MovementCertificateAnimal>();
}
