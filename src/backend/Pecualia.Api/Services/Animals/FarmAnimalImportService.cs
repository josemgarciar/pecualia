using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pecualia.Api.Contracts.Animals;
using Pecualia.Api.Contracts.Farms;
using Pecualia.Api.Data;
using Pecualia.Api.Models.Entities;
using Pecualia.Api.Models.Enums;

namespace Pecualia.Api.Services;

public interface IFarmAnimalImportService
{
    Task<FarmAnimalImportPreviewResponse> PreviewNewFarmAsync(
        long userId,
        UserRole role,
        PreviewNewFarmAnimalImportRequest request,
        CancellationToken cancellationToken);

    Task<FarmAnimalImportPreviewResponse> PreviewExistingFarmAsync(
        long userId,
        UserRole role,
        long farmId,
        FarmAnimalImportDocumentRequest request,
        CancellationToken cancellationToken);

    Task<FarmAnimalImportCommitResponse> CommitExistingFarmAsync(
        long userId,
        UserRole role,
        long farmId,
        FarmAnimalImportDocumentRequest request,
        CancellationToken cancellationToken);

    Task<CreateFarmWithAnimalImportResponse> CreateFarmWithImportAsync(
        long userId,
        UserRole role,
        CreateFarmWithAnimalImportRequest request,
        CancellationToken cancellationToken);
}

public sealed class FarmAnimalImportService(
    PecualiaDbContext dbContext,
    IFarmService farmService,
    IClock clock) : IFarmAnimalImportService
{
    public async Task<FarmAnimalImportPreviewResponse> PreviewNewFarmAsync(
        long userId,
        UserRole role,
        PreviewNewFarmAnimalImportRequest request,
        CancellationToken cancellationToken)
    {
        _ = userId;
        _ = role;
        EnsureSupportedSpecies(request.LivestockSpecies);
        var regaCode = NormalizeAndValidateRega(request.RegaCode);
        var rows = FarmAnimalImportParser.Parse(
            request.FileName,
            request.Content,
            request.LivestockSpecies,
            regaCode,
            Today());
        return await EvaluateAsync(request.LivestockSpecies, regaCode, rows, null, cancellationToken);
    }

    public async Task<FarmAnimalImportPreviewResponse> PreviewExistingFarmAsync(
        long userId,
        UserRole role,
        long farmId,
        FarmAnimalImportDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var farm = await GetAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        var rows = FarmAnimalImportParser.Parse(
            request.FileName,
            request.Content,
            farm.LivestockSpecies,
            farm.RegaCode,
            Today());
        return await EvaluateAsync(farm.LivestockSpecies, farm.RegaCode, rows, farm.Id, cancellationToken);
    }

    public async Task<FarmAnimalImportCommitResponse> CommitExistingFarmAsync(
        long userId,
        UserRole role,
        long farmId,
        FarmAnimalImportDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var farm = await GetAccessibleFarmAsync(userId, role, farmId, cancellationToken);
        var rows = FarmAnimalImportParser.Parse(
            request.FileName,
            request.Content,
            farm.LivestockSpecies,
            farm.RegaCode,
            Today());
        var preview = await EvaluateAsync(farm.LivestockSpecies, farm.RegaCode, rows, farm.Id, cancellationToken);
        EnsureCanCommit(preview);

        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);
        try
        {
            var result = await PersistAsync(farm, preview, cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return result;
        }
        catch (DbUpdateException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            dbContext.ChangeTracker.Clear();
            var reconciledFarm = await GetAccessibleFarmAsync(userId, role, farmId, cancellationToken);
            var reconciledPreview = await EvaluateAsync(
                reconciledFarm.LivestockSpecies,
                reconciledFarm.RegaCode,
                rows,
                reconciledFarm.Id,
                cancellationToken);
            if (IsCompletedByAnotherRequest(reconciledPreview))
            {
                return BuildNoOpCommitResponse(reconciledPreview);
            }

            throw new DomainException(
                "Otra solicitud está importando estos animales. Vuelve a intentarlo; el reintento es seguro.");
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
    }

    public async Task<CreateFarmWithAnimalImportResponse> CreateFarmWithImportAsync(
        long userId,
        UserRole role,
        CreateFarmWithAnimalImportRequest request,
        CancellationToken cancellationToken)
    {
        EnsureSupportedSpecies(request.Farm.LivestockSpecies);
        var regaCode = NormalizeAndValidateRega(request.Farm.RegaCode);
        var rows = FarmAnimalImportParser.Parse(
            request.FileName,
            request.Content,
            request.Farm.LivestockSpecies,
            regaCode,
            Today());
        var existingFarmId = await dbContext.Farms
            .AsNoTracking()
            .Where(entity => entity.RegaCode == regaCode)
            .Select(entity => (long?)entity.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var preview = await EvaluateAsync(
            request.Farm.LivestockSpecies,
            regaCode,
            rows,
            existingFarmId,
            cancellationToken);
        EnsureCanCommit(preview);

        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);
        try
        {
            var farmResponse = await farmService.CreateFarmAsync(userId, role, request.Farm, cancellationToken);
            var farm = await dbContext.Farms.SingleAsync(entity => entity.Id == farmResponse.Id, cancellationToken);
            var finalPreview = await EvaluateAsync(
                farm.LivestockSpecies,
                farm.RegaCode,
                rows,
                farm.Id,
                cancellationToken);
            EnsureCanCommit(finalPreview);
            var importResponse = await PersistAsync(farm, finalPreview, cancellationToken);
            var animalCount = await dbContext.Animals.CountAsync(
                entity => entity.LivestockFarmId == farm.Id && entity.DischargeDate == null,
                cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new CreateFarmWithAnimalImportResponse(
                farmResponse with { AnimalCount = animalCount },
                importResponse);
        }
        catch (DbUpdateException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            dbContext.ChangeTracker.Clear();
            var farmResponse = await farmService.CreateFarmAsync(userId, role, request.Farm, cancellationToken);
            var reconciledFarm = await GetAccessibleFarmAsync(userId, role, farmResponse.Id, cancellationToken);
            var reconciledPreview = await EvaluateAsync(
                reconciledFarm.LivestockSpecies,
                reconciledFarm.RegaCode,
                rows,
                reconciledFarm.Id,
                cancellationToken);
            if (IsCompletedByAnotherRequest(reconciledPreview))
            {
                var animalCount = await dbContext.Animals.CountAsync(
                    entity => entity.LivestockFarmId == reconciledFarm.Id && entity.DischargeDate == null,
                    cancellationToken);
                return new CreateFarmWithAnimalImportResponse(
                    farmResponse with { AnimalCount = animalCount },
                    BuildNoOpCommitResponse(reconciledPreview));
            }

            throw new DomainException(
                "Otra solicitud está creando la explotación o importando sus animales. Vuelve a intentarlo; el reintento es seguro.");
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
    }

    private async Task<FarmAnimalImportPreviewResponse> EvaluateAsync(
        LivestockSpecies species,
        string regaCode,
        IReadOnlyList<ParsedFarmAnimalImportRow> parsedRows,
        long? targetFarmId,
        CancellationToken cancellationToken)
    {
        var rows = parsedRows
            .Select(MapParsedRow)
            .ToList();

        var firstRowsByIdentification = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!row.Processable || string.IsNullOrWhiteSpace(row.Identification))
            {
                continue;
            }

            if (!firstRowsByIdentification.TryAdd(row.Identification, row.RowNumber))
            {
                rows[index] = row with
                {
                    Status = FarmAnimalImportStatuses.Duplicate,
                    Message = $"Crotal repetido dentro del documento (primera aparición en la fila {firstRowsByIdentification[row.Identification]}).",
                    Processable = false
                };
            }
        }

        var identifications = rows
            .Where(row => row.Processable && row.Identification is not null)
            .Select(row => row.Identification!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existing = identifications.Length == 0
            ? []
            : await dbContext.Animals
                .AsNoTracking()
                .Where(entity => identifications.Contains(entity.Identification))
                .Select(entity => new { entity.Identification, entity.LivestockFarmId })
                .ToListAsync(cancellationToken);
        var existingByIdentification = existing.ToDictionary(
            entity => entity.Identification,
            entity => entity.LivestockFarmId,
            StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!row.Processable ||
                row.Identification is null ||
                !existingByIdentification.TryGetValue(row.Identification, out var existingFarmId))
            {
                continue;
            }

            var existsInTargetFarm = targetFarmId.HasValue && existingFarmId == targetFarmId.Value;
            rows[index] = row with
            {
                Status = existsInTargetFarm ? FarmAnimalImportStatuses.Existing : FarmAnimalImportStatuses.Conflict,
                Message = existsInTargetFarm
                    ? "El animal ya existe en esta explotación."
                    : "El crotal ya está registrado en otra explotación.",
                Processable = false
            };
        }

        return new FarmAnimalImportPreviewResponse(
            species.ToString(),
            regaCode,
            rows,
            BuildSummary(rows));
    }

    private async Task<FarmAnimalImportCommitResponse> PersistAsync(
        LivestockFarm farm,
        FarmAnimalImportPreviewResponse preview,
        CancellationToken cancellationToken)
    {
        var processableRows = preview.Rows.Where(row => row.Processable).ToList();
        var animals = processableRows
            .Select(row => new Animal
            {
                LivestockFarmId = farm.Id,
                Identification = row.Identification!,
                BirthDate = row.BirthDate,
                BirthYear = row.BirthDate?.Year,
                Breed = row.Breed,
                Sex = row.Sex,
                OriginCode = row.OriginCode,
                RegistrationDate = row.RegistrationDate,
                RegistrationCause = AnimalRegistrationCause.Entrada
            })
            .ToList();

        dbContext.Animals.AddRange(animals);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.OvinoCaprinoAnimals.AddRange(animals.Select((animal, index) => new OvinoCaprinoAnimal
        {
            AnimalId = animal.Id,
            SpeciesType = farm.LivestockSpecies,
            IdentificationDate = processableRows[index].IdentificationDate
        }));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new FarmAnimalImportCommitResponse(
            animals.Count,
            preview.Summary.TotalRows - animals.Count - preview.Summary.ExistingRows,
            preview.Summary);
    }

    private async Task<LivestockFarm> GetAccessibleFarmAsync(
        long userId,
        UserRole role,
        long farmId,
        CancellationToken cancellationToken)
    {
        var query = role == UserRole.Manager
            ? dbContext.Farms.Where(entity => entity.Farmer.ManagerId == userId)
            : dbContext.Farms.Where(entity => entity.FarmerId == userId);
        var farm = await query.SingleOrDefaultAsync(entity => entity.Id == farmId, cancellationToken);
        if (farm is null)
        {
            throw new DomainException("Explotación no encontrada.");
        }

        EnsureSupportedSpecies(farm.LivestockSpecies);
        return farm;
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

    private DateOnly Today() => DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

    private static void EnsureSupportedSpecies(LivestockSpecies species)
    {
        if (species is not (LivestockSpecies.Ovine or LivestockSpecies.Caprine))
        {
            throw new DomainException("La importación de animales solo está disponible para explotaciones ovinas y caprinas.");
        }
    }

    private static string NormalizeAndValidateRega(string? regaCode)
    {
        var normalized = DomainValidators.NormalizeRegaCode(regaCode ?? string.Empty);
        if (!DomainValidators.IsValidRegaCode(normalized))
        {
            throw new DomainException("El código REGA no es válido. Debe seguir el formato ES seguido de 12 dígitos.");
        }

        return normalized;
    }

    private static void EnsureCanCommit(FarmAnimalImportPreviewResponse preview)
    {
        if (preview.Summary.ProcessableRows == 0 && preview.Summary.ExistingRows == 0)
        {
            throw new DomainException("El documento no contiene filas válidas que se puedan importar.");
        }
    }

    private static bool IsCompletedByAnotherRequest(FarmAnimalImportPreviewResponse preview) =>
        preview.Summary.ProcessableRows == 0 &&
        preview.Summary.ExistingRows > 0;

    private static FarmAnimalImportCommitResponse BuildNoOpCommitResponse(FarmAnimalImportPreviewResponse preview) =>
        new(
            0,
            preview.Summary.TotalRows - preview.Summary.ExistingRows,
            preview.Summary);

    private static FarmAnimalImportRowResponse MapParsedRow(ParsedFarmAnimalImportRow row) =>
        new(
            row.RowNumber,
            row.Identification,
            row.BirthDate,
            row.Breed,
            row.Sex,
            row.OriginCode,
            row.RegistrationDate,
            row.IdentificationDate,
            row.Status,
            row.Message,
            row.Processable);

    private static FarmAnimalImportSummaryResponse BuildSummary(IReadOnlyList<FarmAnimalImportRowResponse> rows) =>
        new(
            rows.Count,
            rows.Count(row => row.Processable),
            rows.Count(row => row.Status == FarmAnimalImportStatuses.Valid),
            rows.Count(row => row.Status == FarmAnimalImportStatuses.Warning),
            rows.Count(row => row.Status == FarmAnimalImportStatuses.Duplicate),
            rows.Count(row => row.Status == FarmAnimalImportStatuses.Existing),
            rows.Count(row => row.Status == FarmAnimalImportStatuses.Conflict),
            rows.Count(row => row.Status == FarmAnimalImportStatuses.FarmMismatch),
            rows.Count(row => row.Status == FarmAnimalImportStatuses.Invalid));
}

internal static class FarmAnimalImportStatuses
{
    internal const string Valid = "valid";
    internal const string Warning = "warning";
    internal const string Duplicate = "duplicate";
    internal const string Existing = "existing";
    internal const string Conflict = "conflict";
    internal const string FarmMismatch = "farm_mismatch";
    internal const string Invalid = "invalid";
}

internal sealed record ParsedFarmAnimalImportRow(
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

internal static partial class FarmAnimalImportParser
{
    private const int MaxDocumentBytes = 5 * 1024 * 1024;
    private const int MaxRows = 1000;
    private static readonly string[] RequiredHeaders =
    [
        "crotal",
        "codigo explotacion pertenencia",
        "codigo explotacion ubicacion",
        "codigo explotacion nacimiento",
        "raza",
        "sexo",
        "fecha nacimiento",
        "fecha inicio explotacion pertenencia",
        "fecha inicio explotacion ubicacion",
        "fecha crotalizacion",
        "fecha comunicacion crotalizacion",
        "fecha baja",
        "fecha comunicacion baja"
    ];

    internal static IReadOnlyList<ParsedFarmAnimalImportRow> Parse(
        string? fileName,
        string? content,
        LivestockSpecies species,
        string targetRegaCode,
        DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(Path.GetExtension(fileName), ".xls", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Selecciona un documento .xls exportado desde Animales pertenecientes.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new DomainException("El documento está vacío.");
        }

        if (Encoding.UTF8.GetByteCount(content) > MaxDocumentBytes)
        {
            throw new DomainException("El documento supera el tamaño máximo de 5 MB.");
        }

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxDocumentBytes
            };
            using var stringReader = new StringReader(content.TrimStart('\uFEFF'));
            using var xmlReader = XmlReader.Create(stringReader, settings);
            document = XDocument.Load(xmlReader, LoadOptions.None);
        }
        catch (XmlException)
        {
            throw new DomainException("El fichero no tiene el formato HTML/XML esperado del informe Animales pertenecientes.");
        }

        var table = FindAnimalTable(document);
        var tableRows = table.Descendants().Where(IsTableRow).ToList();
        if (tableRows.Count < 2)
        {
            throw new DomainException("El documento no contiene animales.");
        }

        var headers = ReadCells(tableRows[0])
            .Select((value, index) => new { Header = NormalizeHeader(value), Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Header))
            .GroupBy(item => item.Header)
            .ToDictionary(group => group.Key, group => group.First().Index);
        var missingHeaders = RequiredHeaders.Where(header => !headers.ContainsKey(header)).ToList();
        if (missingHeaders.Count > 0)
        {
            throw new DomainException($"El documento no contiene las columnas requeridas: {string.Join(", ", missingHeaders)}.");
        }

        var dataRows = tableRows.Skip(1).Where(row => ReadCells(row).Any(cell => !string.IsNullOrWhiteSpace(cell))).ToList();
        if (dataRows.Count > MaxRows)
        {
            throw new DomainException($"El documento contiene más de {MaxRows} animales.");
        }

        return dataRows
            .Select((row, index) => ParseRow(ReadCells(row), headers, index + 2, species, targetRegaCode, today))
            .ToList();
    }

    private static ParsedFarmAnimalImportRow ParseRow(
        IReadOnlyList<string> cells,
        IReadOnlyDictionary<string, int> headers,
        int rowNumber,
        LivestockSpecies species,
        string targetRegaCode,
        DateOnly today)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var identification = DomainValidators.NormalizeAnimalIdentification(Cell(cells, headers, "crotal"));
        if (string.IsNullOrWhiteSpace(identification) || !DomainValidators.IsValidAnimalIdentification(species, identification))
        {
            errors.Add("El crotal no tiene un formato válido.");
        }

        var belongingRega = DomainValidators.NormalizeRegaCode(Cell(cells, headers, "codigo explotacion pertenencia"));
        var locationRega = DomainValidators.NormalizeRegaCode(Cell(cells, headers, "codigo explotacion ubicacion"));
        if (!DomainValidators.IsValidRegaCode(belongingRega) || !DomainValidators.IsValidRegaCode(locationRega))
        {
            errors.Add("Los códigos REGA de pertenencia y ubicación deben tener formato ES seguido de 12 dígitos.");
        }

        var birthDate = ParseRequiredDate(cells, headers, "fecha nacimiento", "fecha de nacimiento", errors);
        var registrationDate = ParseRequiredDate(cells, headers, "fecha inicio explotacion pertenencia", "fecha de inicio de pertenencia", errors);
        var locationDate = ParseRequiredDate(cells, headers, "fecha inicio explotacion ubicacion", "fecha de inicio de ubicación", errors);
        var identificationDate = ParseOptionalDate(cells, headers, "fecha crotalizacion", "fecha de crotalización", errors);
        var identificationCommunicationDate = ParseOptionalDate(cells, headers, "fecha comunicacion crotalizacion", "fecha de comunicación de crotalización", errors);
        var dischargeDateText = Cell(cells, headers, "fecha baja");
        var dischargeCommunicationText = Cell(cells, headers, "fecha comunicacion baja");
        if (!string.IsNullOrWhiteSpace(dischargeDateText) || !string.IsNullOrWhiteSpace(dischargeCommunicationText))
        {
            errors.Add("La fila corresponde a un animal dado de baja y no se puede importar como censo actual.");
        }

        var rawBreed = Cell(cells, headers, "raza");
        if (!BookDocumentSupport.TryNormalizeBreed(species, rawBreed, out var breed))
        {
            errors.Add("La raza no pertenece al catálogo oficial de la especie.");
        }

        var sex = NormalizeSex(Cell(cells, headers, "sexo"));
        if (sex is null)
        {
            errors.Add("El sexo debe ser Hembra o Macho.");
        }

        var originCode = DomainValidators.NormalizeRegaCode(Cell(cells, headers, "codigo explotacion nacimiento"));
        if (string.IsNullOrWhiteSpace(originCode))
        {
            originCode = null;
            warnings.Add("No consta la explotación de nacimiento.");
        }
        else if (!DomainValidators.IsValidRegaCode(originCode))
        {
            errors.Add("El código REGA de nacimiento no es válido.");
        }

        if (identificationDate is null)
        {
            warnings.Add("No consta la fecha de crotalización.");
        }

        ValidateChronology(birthDate, registrationDate, locationDate, identificationDate, identificationCommunicationDate, today, errors);

        var farmMismatch = errors.Count == 0 &&
            (!string.Equals(belongingRega, targetRegaCode, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(locationRega, targetRegaCode, StringComparison.OrdinalIgnoreCase));
        if (farmMismatch)
        {
            return new ParsedFarmAnimalImportRow(
                rowNumber,
                identification,
                birthDate,
                breed,
                sex,
                originCode,
                registrationDate,
                identificationDate,
                FarmAnimalImportStatuses.FarmMismatch,
                $"Los códigos REGA de pertenencia y ubicación deben coincidir con {targetRegaCode}.",
                false);
        }

        if (errors.Count > 0)
        {
            return new ParsedFarmAnimalImportRow(
                rowNumber,
                identification,
                birthDate,
                breed,
                sex,
                originCode,
                registrationDate,
                identificationDate,
                FarmAnimalImportStatuses.Invalid,
                string.Join(" ", errors.Distinct()),
                false);
        }

        return new ParsedFarmAnimalImportRow(
            rowNumber,
            identification,
            birthDate,
            breed,
            sex,
            originCode,
            registrationDate,
            identificationDate,
            warnings.Count > 0 ? FarmAnimalImportStatuses.Warning : FarmAnimalImportStatuses.Valid,
            warnings.Count > 0 ? string.Join(" ", warnings) : "Fila válida.",
            true);
    }

    private static XElement FindAnimalTable(XDocument document)
    {
        foreach (var table in document.Descendants().Where(element => element.Name.LocalName.Equals("table", StringComparison.OrdinalIgnoreCase)))
        {
            var firstRow = table.Descendants().FirstOrDefault(IsTableRow);
            if (firstRow is null)
            {
                continue;
            }

            var headers = ReadCells(firstRow).Select(NormalizeHeader).ToHashSet();
            if (RequiredHeaders.All(headers.Contains))
            {
                return table;
            }
        }

        throw new DomainException("No se ha encontrado la tabla de Animales pertenecientes en el documento.");
    }

    private static IReadOnlyList<string> ReadCells(XElement row) =>
        row.Elements()
            .Where(element => element.Name.LocalName.Equals("td", StringComparison.OrdinalIgnoreCase) ||
                              element.Name.LocalName.Equals("th", StringComparison.OrdinalIgnoreCase))
            .Select(element => NormalizeCell(string.Concat(element.DescendantNodes().OfType<XText>().Select(text => text.Value))))
            .ToList();

    private static bool IsTableRow(XElement element) =>
        element.Name.LocalName.Equals("tr", StringComparison.OrdinalIgnoreCase);

    private static string Cell(IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> headers, string header) =>
        headers.TryGetValue(header, out var index) && index < cells.Count ? cells[index] : string.Empty;

    private static DateOnly? ParseRequiredDate(
        IReadOnlyList<string> cells,
        IReadOnlyDictionary<string, int> headers,
        string header,
        string label,
        ICollection<string> errors)
    {
        var text = Cell(cells, headers, header);
        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add($"Falta la {label}.");
            return null;
        }

        return TryParseDate(text, label, errors);
    }

    private static DateOnly? ParseOptionalDate(
        IReadOnlyList<string> cells,
        IReadOnlyDictionary<string, int> headers,
        string header,
        string label,
        ICollection<string> errors)
    {
        var text = Cell(cells, headers, header);
        return string.IsNullOrWhiteSpace(text) ? null : TryParseDate(text, label, errors);
    }

    private static DateOnly? TryParseDate(string text, string label, ICollection<string> errors)
    {
        if (DateOnly.TryParseExact(text.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        errors.Add($"La {label} debe usar el formato dd/MM/aaaa.");
        return null;
    }

    private static void ValidateChronology(
        DateOnly? birthDate,
        DateOnly? registrationDate,
        DateOnly? locationDate,
        DateOnly? identificationDate,
        DateOnly? identificationCommunicationDate,
        DateOnly today,
        ICollection<string> errors)
    {
        if (birthDate is { Year: < 1900 } || birthDate > today)
        {
            errors.Add("La fecha de nacimiento está fuera del rango permitido.");
        }

        foreach (var (date, label) in new[]
                 {
                     (registrationDate, "inicio de pertenencia"),
                     (locationDate, "inicio de ubicación"),
                     (identificationDate, "crotalización"),
                     (identificationCommunicationDate, "comunicación de crotalización")
                 })
        {
            if (date > today)
            {
                errors.Add($"La fecha de {label} no puede ser futura.");
            }

            if (date.HasValue && birthDate.HasValue && date < birthDate)
            {
                errors.Add($"La fecha de {label} no puede ser anterior al nacimiento.");
            }
        }

        if (identificationCommunicationDate.HasValue &&
            identificationDate.HasValue &&
            identificationCommunicationDate < identificationDate)
        {
            errors.Add("La comunicación de crotalización no puede ser anterior a la crotalización.");
        }
    }

    private static string? NormalizeSex(string value) => NormalizeHeader(value) switch
    {
        "hembra" or "female" or "h" or "f" => "Female",
        "macho" or "male" or "m" => "Male",
        _ => null
    };

    private static string NormalizeCell(string value) =>
        WhitespaceRegex().Replace(WebUtility.HtmlDecode(value), " ").Trim();

    private static string NormalizeHeader(string value)
    {
        var normalized = NormalizeCell(value).Normalize(NormalizationForm.FormD);
        var withoutDiacritics = new string(normalized
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return withoutDiacritics.Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
