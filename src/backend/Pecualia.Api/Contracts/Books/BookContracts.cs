namespace Pecualia.Api.Contracts.Books;

public sealed record FarmBookPreviewResponse(
    long FarmId,
    string FarmName,
    string RegaCode,
    string LivestockSpecies,
    string Template,
    FarmBookPreviewSummaryResponse Summary,
    IReadOnlyList<FarmBookPreviewSectionResponse> Sections);

public sealed record FarmBookPreviewSummaryResponse(
    string FarmerName,
    string? FarmerIdentifier,
    string? Town,
    string? Province,
    int Animals,
    int Balances,
    int Censuses,
    int Incidents,
    int Inspections);

public sealed record FarmBookPreviewSectionResponse(
    string Id,
    string Title,
    int Items,
    string Description);
