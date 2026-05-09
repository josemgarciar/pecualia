using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Contracts.Farmers;

public sealed record CreateFarmerRequest(
    PersonType PersonType,
    string? Name,
    string? FirstSurname,
    string? SecondSurname,
    DateOnly? BirthDate,
    string? CompanyName,
    string? LegalRepresentative,
    string? Email,
    string NifCif,
    string PhoneNumber,
    string? Residence,
    string Town,
    string Province,
    string? ZipCode);

public sealed record UpdateFarmerRequest(
    PersonType PersonType,
    string? Name,
    string? FirstSurname,
    string? SecondSurname,
    DateOnly? BirthDate,
    string? CompanyName,
    string? LegalRepresentative,
    string? Email,
    string NifCif,
    string PhoneNumber,
    string? Residence,
    string Town,
    string Province,
    string? ZipCode);

public sealed record FarmerListItemResponse(
    long Id,
    string DisplayName,
    string? FullName,
    string? Email,
    string NifCif,
    string? PhoneNumber,
    string? Town,
    string? Province,
    string PersonType,
    string Status,
    bool CanResendActivation,
    int FarmCount);

public sealed record FarmerFarmItemResponse(
    long Id,
    string Name,
    string RegaCode,
    string LivestockSpecies,
    string Status,
    int AnimalCount);

public sealed record FarmerDetailResponse(
    long Id,
    string PersonType,
    string DisplayName,
    string? Name,
    string? FirstSurname,
    string? SecondSurname,
    DateOnly? BirthDate,
    string? CompanyName,
    string? LegalRepresentative,
    string? Email,
    string NifCif,
    string PhoneNumber,
    string? Residence,
    string Town,
    string Province,
    string? ZipCode,
    string Status,
    bool CanResendActivation,
    IReadOnlyList<FarmerFarmItemResponse> Farms);
