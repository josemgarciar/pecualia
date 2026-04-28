namespace Pecualia.Api.Models.Entities;

public sealed class Vaccination
{
    public long Id { get; set; }

    public long AnimalId { get; set; }

    public DateOnly VaccinationDate { get; set; }

    public DateOnly? NextDose { get; set; }

    public string VaccinationType { get; set; } = string.Empty;

    public string? Observations { get; set; }

    public Animal Animal { get; set; } = null!;
}
