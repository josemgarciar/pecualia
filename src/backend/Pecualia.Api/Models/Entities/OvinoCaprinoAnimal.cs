using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Models.Entities;

public sealed class OvinoCaprinoAnimal
{
    public long AnimalId { get; set; }

    public string? DominantAllele { get; set; }

    public string? Genotyping { get; set; }

    public string? LowAllele { get; set; }

    public LivestockSpecies SpeciesType { get; set; }

    public Animal Animal { get; set; } = null!;
}
