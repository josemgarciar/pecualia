using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Contracts.Animals;

public sealed record CreateAnimalRequest(
    long FarmId,
    string Identification,
    int? BirthYear,
    string? Breed,
    string? Sex,
    DateOnly? RegistrationDate,
    string? RegistrationCause,
    string? OriginCode,
    string? HealthDocumentNumber,
    OvinoCaprinoAnimalRequest? OvinoCaprino,
    PorcinoAnimalRequest? Porcino);

public sealed record DischargeAnimalRequest(
    DateOnly DischargeDate,
    string DischargeCause,
    string? DestinationCode);

public sealed record OvinoCaprinoAnimalRequest(
    LivestockSpecies SpeciesType,
    string? Genotyping,
    string? DominantAllele,
    string? LowAllele);

public sealed record PorcinoAnimalRequest(
    string AnimalType,
    DateOnly? IdentificationDate,
    string? PigRegistrationNumber,
    string? Tag);

public sealed record AnimalListItemResponse(
    long Id,
    long FarmId,
    string Identification,
    string LivestockSpecies,
    string FarmName,
    string? Breed,
    string? Sex,
    int? BirthYear,
    DateOnly? RegistrationDate,
    string? RegistrationCause,
    DateOnly? DischargeDate,
    string? DischargeCause,
    string Status);

public sealed record AnimalDetailResponse(
    long Id,
    long FarmId,
    string Identification,
    string LivestockSpecies,
    string FarmName,
    string? FarmRegaCode,
    string? Breed,
    string? Sex,
    int? BirthYear,
    DateOnly? RegistrationDate,
    string? RegistrationCause,
    string? OriginCode,
    string? HealthDocumentNumber,
    DateOnly? DischargeDate,
    string? DischargeCause,
    string? DestinationCode,
    string Status,
    OvinoCaprinoAnimalResponse? OvinoCaprino,
    PorcinoAnimalResponse? Porcino);

public sealed record OvinoCaprinoAnimalResponse(
    string SpeciesType,
    string? Genotyping,
    string? DominantAllele,
    string? LowAllele);

public sealed record PorcinoAnimalResponse(
    string AnimalType,
    DateOnly? IdentificationDate,
    string? PigRegistrationNumber,
    string? Tag);
