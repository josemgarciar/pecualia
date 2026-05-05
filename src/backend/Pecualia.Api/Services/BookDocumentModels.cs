using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

internal sealed record BookAggregate(
    LivestockFarm Farm,
    IReadOnlyList<Animal> Animals,
    IReadOnlyList<Balance> Balances,
    IReadOnlyList<Census> Censuses,
    IReadOnlyList<Incident> Incidents,
    IReadOnlyList<Inspection> Inspections,
    IReadOnlyList<MovementCertificate> Movements);

internal sealed record BookRenderContext(BookAggregate Aggregate, IReadOnlySet<string> IncludedSections)
{
    public LivestockFarm Farm => Aggregate.Farm;

    public bool IsPorcine => Farm.LivestockSpecies == LivestockSpecies.Porcine;

    public bool IsOvineOrCaprine => BookDocumentSupport.IsOvineOrCaprine(Farm);
}

internal sealed record OvineAnimalRow(
    int Order,
    string Identification,
    string? BirthYear,
    string? IdentificationDate,
    string? BreedCode,
    string? SexCode,
    string? Genotyping,
    string? DominantAllele,
    string? LowAllele,
    string? RegistrationCause,
    string? RegistrationDate,
    string? OriginCode,
    string? DischargeCause,
    string? DischargeDate,
    string? DestinationCode,
    string? HealthDocumentNumber,
    string? IncidentPage,
    string? IncidentOrder)
{
    public static OvineAnimalRow Empty => new(0, string.Empty, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
}

internal sealed record OvineIncidentReference(string Page, string Order);

internal sealed record OvineIncidentReferenceLookup(
    IReadOnlyDictionary<long, OvineIncidentReference> ByAnimalId,
    IReadOnlyDictionary<string, OvineIncidentReference> ByIdentification);

internal sealed record PorcineAnimalRow(
    int Order,
    string Identification,
    string? BirthYear,
    string? IdentificationDate,
    string? BreedCode,
    string? SexCode,
    string? RegistrationCause,
    string? RegistrationDate,
    string? OriginCode,
    string? DischargeCause,
    string? DischargeDate,
    string? DestinationCode,
    string? HealthDocumentNumber,
    string? IncidentPage,
    string? IncidentOrder)
{
    public static PorcineAnimalRow Empty => new(0, string.Empty, null, null, null, null, null, null, null, null, null, null, null, null, null);
}

internal sealed record OvineBalanceRow(
    int Order,
    string? Date,
    string? NumberOfAnimals,
    string? CauseCode,
    string? OriginCode,
    string? DestinationCode,
    string? HealthDocumentNumber,
    string? TransporterName,
    string? TransportTicketNumber,
    string? Under4,
    string? From4To12,
    string? ReproductiveMales,
    string? ReproductiveFemales)
{
    public static OvineBalanceRow Empty => new(0, null, null, null, null, null, null, null, null, null, null, null, null);
}

internal sealed record PorcineBalanceRow(
    int Order,
    string? Date,
    string? NumberOfAnimals,
    string? Type,
    string? BreedCode,
    string? Tag,
    string? CauseCode,
    string? Route,
    string? Boars,
    string? SowsForLive,
    string? SowsReposition,
    string? MalesReposition,
    string? Piglets,
    string? Rears,
    string? Baits,
    string? HealthDocumentNumber)
{
    public static PorcineBalanceRow Empty => new(0, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
}

internal sealed record OvineCensusRow(
    int Order,
    string? Date,
    string? Under4,
    string? From4To12,
    string? ReproductiveMales,
    string? ReproductiveFemales)
{
    public static OvineCensusRow Empty => new(0, null, null, null, null, null);
}

internal sealed record PorcineCensusRow(
    int Order,
    string? Date,
    string? Boars,
    string? SowsForLive,
    string? MalesReposition,
    string? SowsReposition,
    string? Piglets,
    string? Rears,
    string? Baits)
{
    public static PorcineCensusRow Empty => new(0, null, null, null, null, null, null, null, null);
}

internal sealed record OvineIncidentRow(int Order, string? ReferencePage, string? ReferenceOrder, string? Date, string? Description)
{
    public static OvineIncidentRow Empty => new(0, null, null, null, null);
}

internal sealed record PorcineIncidentRow(int Order, string? ReferencePage, string? ReferenceOrder, string? Date, string? LastIdentification, string? NewIdentification, string? Cause, string? RemarkedAnimals)
{
    public static PorcineIncidentRow Empty => new(0, null, null, null, null, null, null, null);
}

internal sealed record InspectionRow(int Order, string? Reason, string? Observations, string? Signature)
{
    public static InspectionRow Empty => new(0, null, null, null);
}
