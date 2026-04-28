namespace Pecualia.Api.Models.Entities;

public class Animal
{
    public long Id { get; set; }

    public long LivestockFarmId { get; set; }

    public int? BirthYear { get; set; }

    public string? Breed { get; set; }

    public string? DestinationCode { get; set; }

    public string? DischargeCause { get; set; }

    public string? HealthDocumentNumber { get; set; }

    public string Identification { get; set; } = string.Empty;

    public string? OriginCode { get; set; }

    public string? RegistrationCause { get; set; }

    public DateOnly? RegistrationDate { get; set; }

    public DateOnly? DischargeDate { get; set; }

    public string? Sex { get; set; }

    public LivestockFarm LivestockFarm { get; set; } = null!;

    public OvinoCaprinoAnimal? OvinoCaprino { get; set; }

    public PorcinoAnimal? Porcino { get; set; }
}
