namespace Pecualia.Api.Models.Entities;

public sealed class PorcinoAnimal
{
    public long AnimalId { get; set; }

    public string AnimalType { get; set; } = string.Empty;

    public DateOnly? IdentificationDate { get; set; }

    public string? PigRegistrationNumber { get; set; }

    public string? Tag { get; set; }

    public Animal Animal { get; set; } = null!;
}
