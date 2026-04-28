namespace Pecualia.Api.Models.Entities;

public sealed class AnimalBirth
{
    public long Id { get; set; }

    public long MotherAnimalId { get; set; }

    public long? FatherAnimalId { get; set; }

    public DateOnly BirthDate { get; set; }

    public int OffspringNumber { get; set; }

    public Animal MotherAnimal { get; set; } = null!;

    public Animal? FatherAnimal { get; set; }
}
