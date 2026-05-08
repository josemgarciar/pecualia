using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Contracts.Farms;

public sealed record CreateFarmRequest(
    long FarmerId,
    string Name,
    string RegaCode,
    LivestockSpecies LivestockSpecies,
    FarmRegime Regime,
    string? Town,
    string? Province,
    string? Address,
    string? ZipCode,
    int? AuthorisedCapacity,
    string? PorcineRegistryNumber,
    string? Responsible,
    string? ZootechnicClassification,
    double? XCoordinate,
    double? YCoordinate);

public sealed record UpdateFarmRequest(
    string Name,
    string RegaCode,
    FarmRegime Regime,
    string? Town,
    string? Province,
    string? Address,
    string? ZipCode,
    int? AuthorisedCapacity,
    string? PorcineRegistryNumber,
    string? Responsible,
    string? ZootechnicClassification,
    double? XCoordinate,
    double? YCoordinate);

public sealed record FarmListItemResponse(
    long Id,
    long FarmerId,
    string Name,
    string RegaCode,
    string LivestockSpecies,
    string Status,
    string? Town,
    string? Province,
    string FarmerName,
    int AnimalCount,
    int? AuthorisedCapacity,
    string? PorcineRegistryNumber);

public sealed record FarmSummaryResponse(
    long Id,
    string Name,
    string RegaCode,
    string LivestockSpecies,
    string Status,
    string FarmerName,
    int AnimalCount,
    int? AuthorisedCapacity,
    string? PorcineRegistryNumber,
    string? Regime,
    string? Town,
    string? Province,
    string? Responsible);

public sealed record FarmDetailResponse(
    long Id,
    long FarmerId,
    string Name,
    string RegaCode,
    string LivestockSpecies,
    string Status,
    string FarmerName,
    int AnimalCount,
    int? AuthorisedCapacity,
    string? PorcineRegistryNumber,
    string? Regime,
    string? LivestockType,
    string? ProductionCapacity,
    string? Town,
    string? Province,
    string? Address,
    string? ZipCode,
    string? Responsible,
    string? ZootechnicClassification,
    int? Spindle,
    double? XCoordinate,
    double? YCoordinate);
