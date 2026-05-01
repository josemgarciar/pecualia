namespace Pecualia.Api.Contracts.FarmOperations;

public sealed record FarmBirthResponse(
    long Id,
    long FarmId,
    DateOnly BirthDate,
    int OffspringNumber,
    decimal? BirthWeight,
    string? Observations);

public sealed record CreateFarmBirthRequest(
    DateOnly BirthDate,
    int OffspringNumber,
    decimal? BirthWeight,
    string? Observations);

public sealed record FarmDeathResponse(
    long AnimalId,
    long FarmId,
    string Identification,
    string? Breed,
    string? Sex,
    int? BirthYear,
    DateOnly DischargeDate,
    string DischargeCause,
    string? DestinationCode);

public sealed record CreateFarmDeathRequest(
    string Identification,
    DateOnly DischargeDate,
    string? DestinationCode);

public sealed record FarmCensusResponse(
    long? Id,
    long FarmId,
    int Year,
    string LivestockSpecies,
    int NonReproductiveUnder4Months,
    int NonReproductiveBetween4And12Months,
    int ReproductiveFemales,
    int ReproductiveMales,
    int Boars,
    int SowsForLive,
    int SowsReposition,
    int MalesReposition,
    int Piglets,
    int Rears,
    int Baits,
    int Total,
    IReadOnlyList<int> AvailableYears);

public sealed record UpdateFarmCensusRequest(
    int? NonReproductiveUnder4Months,
    int? NonReproductiveBetween4And12Months,
    int? ReproductiveFemales,
    int? ReproductiveMales,
    int? Boars,
    int? SowsForLive,
    int? SowsReposition,
    int? MalesReposition,
    int? Piglets,
    int? Rears,
    int? Baits);

public sealed record FarmBalanceResponse(
    long FarmId,
    int Year,
    int Registrations,
    int Births,
    int Deaths,
    int Departures,
    int MovementEntries,
    int MovementDepartures,
    int Balance,
    IReadOnlyList<FarmMonthlyBalanceResponse> Months);

public sealed record FarmMonthlyBalanceResponse(
    int Month,
    int Registrations,
    int Births,
    int Deaths,
    int Departures,
    int MovementEntries,
    int MovementDepartures,
    int Balance);

public sealed record FarmIncidentResponse(
    long Id,
    long FarmId,
    long? AnimalId,
    string? AnimalIdentification,
    DateOnly IncidentDate,
    string? ChangeReason,
    string? Description,
    string? LastIdentification,
    string? NewIdentification);

public sealed record CreateFarmIncidentRequest(
    string? AnimalIdentification,
    DateOnly IncidentDate,
    string? ChangeReason,
    string? Description,
    string? LastIdentification,
    string? NewIdentification);

public sealed record FarmInspectionResponse(
    long Id,
    long FarmId,
    DateOnly InspectionDate,
    string? Reason,
    string? Observations,
    string? Veterinary,
    int? TaggedAnimals);

public sealed record CreateFarmInspectionRequest(
    DateOnly InspectionDate,
    string? Reason,
    string? Observations,
    string? Veterinary,
    int? TaggedAnimals);
