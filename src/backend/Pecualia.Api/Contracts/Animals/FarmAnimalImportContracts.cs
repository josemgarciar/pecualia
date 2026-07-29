using Pecualia.Api.Contracts.Farms;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Contracts.Animals;

public sealed record PreviewNewFarmAnimalImportRequest(
    LivestockSpecies LivestockSpecies,
    string RegaCode,
    string FileName,
    string Content);

public sealed record FarmAnimalImportDocumentRequest(
    string FileName,
    string Content);

public sealed record CreateFarmWithAnimalImportRequest(
    CreateFarmRequest Farm,
    string FileName,
    string Content);

public sealed record FarmAnimalImportRowResponse(
    int RowNumber,
    string? Identification,
    DateOnly? BirthDate,
    string? Breed,
    string? Sex,
    string? OriginCode,
    DateOnly? RegistrationDate,
    DateOnly? IdentificationDate,
    string Status,
    string Message,
    bool Processable);

public sealed record FarmAnimalImportSummaryResponse(
    int TotalRows,
    int ProcessableRows,
    int ValidRows,
    int WarningRows,
    int DuplicateRows,
    int ExistingRows,
    int ConflictRows,
    int FarmMismatchRows,
    int InvalidRows);

public sealed record FarmAnimalImportPreviewResponse(
    string LivestockSpecies,
    string TargetRegaCode,
    IReadOnlyList<FarmAnimalImportRowResponse> Rows,
    FarmAnimalImportSummaryResponse Summary);

public sealed record FarmAnimalImportCommitResponse(
    int CreatedAnimals,
    int RejectedRows,
    FarmAnimalImportSummaryResponse Summary);

public sealed record CreateFarmWithAnimalImportResponse(
    FarmListItemResponse Farm,
    FarmAnimalImportCommitResponse Import);
