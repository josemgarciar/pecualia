namespace Pecualia.Api.Models.Entities;

public sealed class AnimalBirth
{
    public long Id { get; set; }

    public long LivestockFarmId { get; set; }

    public long? MotherAnimalId { get; set; }

    public long? FatherAnimalId { get; set; }

    public DateOnly BirthDate { get; set; }

    public decimal? BirthWeight { get; set; }

    public string? Observations { get; set; }

    public int OffspringNumber { get; set; }

    public long? BalanceId { get; set; }

    public LivestockFarm LivestockFarm { get; set; } = null!;

    public Balance? Balance { get; set; }

    public Animal? MotherAnimal { get; set; }

    public Animal? FatherAnimal { get; set; }
}
