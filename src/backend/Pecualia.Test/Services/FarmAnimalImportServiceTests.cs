using Pecualia.Api.Contracts.Animals;
using Pecualia.Api.Contracts.Farms;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class FarmAnimalImportServiceTests
{
    [Fact]
    public async Task PreviewExistingFarmAsync_ReportsValidWarningMismatchAndDuplicateRows()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero));
        var farmService = PerformanceTestSupport.CreateFarmService(dbContext, clock);
        var service = new FarmAnimalImportService(dbContext, farmService, clock);
        var farmerUser = ServiceTestData.CreateUser(800, UserRole.Farmer, "Ana", "Pastora", email: "ana-import@test.local");
        var farmer = ServiceTestData.CreateFarmer(800, farmerUser, nifCif: "12345678Z");
        var farm = ServiceTestData.CreateFarm(801, farmer.UserId, LivestockSpecies.Ovine, "Ovina", "ES061600000720");
        dbContext.Users.Add(farmerUser);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();

        var document = BuildDocument(
            Row("ES060000000001", farm.RegaCode, farm.RegaCode, farm.RegaCode, "Merina", "Hembra", "01/02/2024", "01/03/2024", "01/03/2024", "10/02/2024", "11/02/2024"),
            Row("ES060000000002", farm.RegaCode, farm.RegaCode, "", "Cruzada", "Macho", "02/02/2024", "02/03/2024", "02/03/2024", "", ""),
            Row("ES060000000003", "ES061600000721", farm.RegaCode, farm.RegaCode, "Merina", "Hembra", "03/02/2024", "03/03/2024", "03/03/2024", "10/02/2024", ""),
            Row("ES060000000001", farm.RegaCode, farm.RegaCode, farm.RegaCode, "Merina", "Hembra", "01/02/2024", "01/03/2024", "01/03/2024", "10/02/2024", ""));

        var preview = await service.PreviewExistingFarmAsync(
            farmer.UserId,
            UserRole.Farmer,
            farm.Id,
            new FarmAnimalImportDocumentRequest("AnimalesPertenecientes.xls", document),
            CancellationToken.None);

        preview.Summary.TotalRows.Should().Be(4);
        preview.Summary.ProcessableRows.Should().Be(2);
        preview.Summary.ValidRows.Should().Be(1);
        preview.Summary.WarningRows.Should().Be(1);
        preview.Summary.FarmMismatchRows.Should().Be(1);
        preview.Summary.DuplicateRows.Should().Be(1);
    }

    [Fact]
    public async Task CommitExistingFarmAsync_CreatesOnlyProcessableRows_WithExactDates()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero));
        var farmService = PerformanceTestSupport.CreateFarmService(dbContext, clock);
        var service = new FarmAnimalImportService(dbContext, farmService, clock);
        var farmerUser = ServiceTestData.CreateUser(810, UserRole.Farmer, "Luis", "Pastor", email: "luis-import@test.local");
        var farmer = ServiceTestData.CreateFarmer(810, farmerUser, nifCif: "87654321X");
        var farm = ServiceTestData.CreateFarm(811, farmer.UserId, LivestockSpecies.Caprine, "Caprina", "ES061600000730");
        dbContext.Users.Add(farmerUser);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();
        var document = BuildDocument(
            Row("ES060000000011", farm.RegaCode, farm.RegaCode, farm.RegaCode, "Verata", "Hembra", "05/01/2023", "06/02/2024", "06/02/2024", "20/01/2023", "21/01/2023"),
            Row("incorrecto", farm.RegaCode, farm.RegaCode, farm.RegaCode, "Verata", "Hembra", "05/01/2023", "06/02/2024", "06/02/2024", "20/01/2023", ""));

        var result = await service.CommitExistingFarmAsync(
            farmer.UserId,
            UserRole.Farmer,
            farm.Id,
            new FarmAnimalImportDocumentRequest("AnimalesPertenecientes.xls", document),
            CancellationToken.None);

        result.CreatedAnimals.Should().Be(1);
        result.RejectedRows.Should().Be(1);
        var animal = await dbContext.Animals.FindAsync(1L);
        animal.Should().NotBeNull();
        animal!.BirthDate.Should().Be(new DateOnly(2023, 1, 5));
        animal.BirthYear.Should().Be(2023);
        animal.Sex.Should().Be("Female");
        animal.RegistrationCause.Should().Be(AnimalRegistrationCause.Entrada);
        var subtype = await dbContext.OvinoCaprinoAnimals.FindAsync(animal.Id);
        subtype!.SpeciesType.Should().Be(LivestockSpecies.Caprine);
        subtype.IdentificationDate.Should().Be(new DateOnly(2023, 1, 20));
    }

    [Fact]
    public async Task CommitExistingFarmAsync_IsIdempotentWhenSameDocumentIsRetried()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero));
        var farmService = PerformanceTestSupport.CreateFarmService(dbContext, clock);
        var service = new FarmAnimalImportService(dbContext, farmService, clock);
        var user = ServiceTestData.CreateUser(815, UserRole.Farmer, "Rebeca", "Pastora", email: "retry-import@test.local");
        var farmer = ServiceTestData.CreateFarmer(user.Id, user, nifCif: "00000017G");
        var farm = ServiceTestData.CreateFarm(816, farmer.UserId, LivestockSpecies.Ovine, "Ovina reintentable", "ES061600000735");
        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();
        var document = BuildDocument(
            Row("ES060000000015", farm.RegaCode, farm.RegaCode, farm.RegaCode, "Merina", "Hembra", "05/01/2023", "06/02/2024", "06/02/2024", "20/01/2023", ""));
        var request = new FarmAnimalImportDocumentRequest("AnimalesPertenecientes.xls", document);

        var first = await service.CommitExistingFarmAsync(
            user.Id,
            UserRole.Farmer,
            farm.Id,
            request,
            CancellationToken.None);
        var retry = await service.CommitExistingFarmAsync(
            user.Id,
            UserRole.Farmer,
            farm.Id,
            request,
            CancellationToken.None);

        first.CreatedAnimals.Should().Be(1);
        retry.CreatedAnimals.Should().Be(0);
        retry.RejectedRows.Should().Be(0);
        retry.Summary.ExistingRows.Should().Be(1);
        dbContext.Animals.Should().ContainSingle(entity => entity.Identification == "ES060000000015");
    }

    [Fact]
    public async Task PreviewNewFarmAsync_RejectsPorcineImports()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero));
        var service = new FarmAnimalImportService(dbContext, PerformanceTestSupport.CreateFarmService(dbContext, clock), clock);

        var action = () => service.PreviewNewFarmAsync(
            1,
            UserRole.Farmer,
            new PreviewNewFarmAnimalImportRequest(LivestockSpecies.Porcine, "ES061600000720", "animals.xls", BuildDocument()),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("*solo está disponible para explotaciones ovinas y caprinas*");
    }

    [Fact]
    public async Task CreateFarmWithImportAsync_CreatesFarmAndAnimalsTogether()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero));
        var farmService = PerformanceTestSupport.CreateFarmService(dbContext, clock);
        var service = new FarmAnimalImportService(dbContext, farmService, clock);
        var user = ServiceTestData.CreateUser(820, UserRole.Farmer, "María", "Cabra", email: "create-import@test.local");
        var farmer = ServiceTestData.CreateFarmer(820, user, nifCif: "00000000T");
        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        await dbContext.SaveChangesAsync();
        const string regaCode = "ES061600000740";
        var farmRequest = new CreateFarmRequest(
            farmer.UserId,
            "Caprina importada",
            regaCode,
            LivestockSpecies.Caprine,
            FarmRegime.Extensive,
            "Mérida",
            "Badajoz",
            null,
            null,
            null,
            null,
            "Reproducción",
            null,
            null,
            "María",
            "Reproducción",
            30,
            null,
            null);
        var document = BuildDocument(
            Row("ES060000000031", regaCode, regaCode, regaCode, "Verata", "Hembra", "01/01/2024", "01/02/2024", "01/02/2024", "10/01/2024", ""));

        var result = await service.CreateFarmWithImportAsync(
            user.Id,
            UserRole.Farmer,
            new CreateFarmWithAnimalImportRequest(farmRequest, "animals.xls", document),
            CancellationToken.None);

        result.Import.CreatedAnimals.Should().Be(1);
        result.Farm.AnimalCount.Should().Be(1);
        dbContext.Farms.Should().ContainSingle(entity => entity.Name == "Caprina importada");
        dbContext.Animals.Should().ContainSingle(entity => entity.Identification == "ES060000000031");
    }

    [Fact]
    public async Task CreateFarmWithImportAsync_IsIdempotentWhenSameOperationIsRetried()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero));
        var farmService = PerformanceTestSupport.CreateFarmService(dbContext, clock);
        var service = new FarmAnimalImportService(dbContext, farmService, clock);
        var user = ServiceTestData.CreateUser(825, UserRole.Farmer, "Inés", "Pastora", email: "retry-create-import@test.local");
        var farmer = ServiceTestData.CreateFarmer(user.Id, user, nifCif: "00000018H");
        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        await dbContext.SaveChangesAsync();
        const string regaCode = "ES061600000745";
        var farmRequest = new CreateFarmRequest(
            farmer.UserId,
            "Ovina idempotente",
            regaCode,
            LivestockSpecies.Ovine,
            FarmRegime.Extensive,
            "Mérida",
            "Badajoz",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            30,
            null,
            null);
        var document = BuildDocument(
            Row("ES060000000035", regaCode, regaCode, regaCode, "Merina", "Hembra", "01/01/2024", "01/02/2024", "01/02/2024", "10/01/2024", ""));
        var request = new CreateFarmWithAnimalImportRequest(farmRequest, "animals.xls", document);

        var first = await service.CreateFarmWithImportAsync(user.Id, UserRole.Farmer, request, CancellationToken.None);
        var retry = await service.CreateFarmWithImportAsync(user.Id, UserRole.Farmer, request, CancellationToken.None);

        retry.Farm.Id.Should().Be(first.Farm.Id);
        retry.Farm.AnimalCount.Should().Be(1);
        retry.Import.CreatedAnimals.Should().Be(0);
        retry.Import.RejectedRows.Should().Be(0);
        retry.Import.Summary.ExistingRows.Should().Be(1);
        dbContext.Farms.Should().ContainSingle(entity => entity.RegaCode == regaCode);
        dbContext.Animals.Should().ContainSingle(entity => entity.Identification == "ES060000000035");
    }

    [Fact]
    public async Task CreateFarmWithImportAsync_DoesNotCreateFarm_WhenDocumentHasNoProcessableRows()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero));
        var farmService = PerformanceTestSupport.CreateFarmService(dbContext, clock);
        var service = new FarmAnimalImportService(dbContext, farmService, clock);
        var user = ServiceTestData.CreateUser(830, UserRole.Farmer, "Pedro", "Ovino", email: "rollback-import@test.local");
        var farmer = ServiceTestData.CreateFarmer(830, user, nifCif: "00000001R");
        dbContext.Users.Add(user);
        dbContext.Farmers.Add(farmer);
        await dbContext.SaveChangesAsync();
        const string regaCode = "ES061600000750";
        var farmRequest = new CreateFarmRequest(
            farmer.UserId,
            "No debe crearse",
            regaCode,
            LivestockSpecies.Ovine,
            FarmRegime.Extensive,
            "Mérida",
            "Badajoz",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            30,
            null,
            null);
        var document = BuildDocument(
            Row("crotal-invalido", regaCode, regaCode, regaCode, "Merina", "Hembra", "01/01/2024", "01/02/2024", "01/02/2024", "", ""));

        var action = () => service.CreateFarmWithImportAsync(
            user.Id,
            UserRole.Farmer,
            new CreateFarmWithAnimalImportRequest(farmRequest, "animals.xls", document),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("*no contiene filas válidas*");
        dbContext.Farms.Should().BeEmpty();
    }

    [Fact]
    public void Parser_RejectsBinaryOrWrongExtensionDocuments()
    {
        var action = () => FarmAnimalImportParser.Parse(
            "animals.xlsx",
            "not-a-document",
            LivestockSpecies.Ovine,
            "ES061600000720",
            new DateOnly(2026, 7, 29));

        action.Should().Throw<DomainException>()
            .WithMessage("*documento .xls*");
    }

    [Fact]
    public void Parser_RejectsDocumentsOverMaximumRowCount()
    {
        const string regaCode = "ES061600000720";
        var rows = Enumerable.Range(1, 1001)
            .Select(index => Row(
                $"ES06{index:D10}",
                regaCode,
                regaCode,
                regaCode,
                "Merina",
                "Hembra",
                "01/02/2024",
                "01/03/2024",
                "01/03/2024",
                "10/02/2024",
                "11/02/2024"))
            .ToArray();

        var action = () => FarmAnimalImportParser.Parse(
            "animals.xls",
            BuildDocument(rows),
            LivestockSpecies.Ovine,
            regaCode,
            new DateOnly(2026, 7, 29));

        action.Should().Throw<DomainException>()
            .WithMessage("*más de 1000 animales*");
    }

    [Fact]
    public void Parser_RejectsInvalidDatesAndDischargedAnimals()
    {
        const string regaCode = "ES061600000720";
        var rows = FarmAnimalImportParser.Parse(
            "animals.xls",
            BuildDocument(
                Row("ES060000000021", regaCode, regaCode, regaCode, "Merina", "Hembra", "2024-01-01", "01/03/2024", "01/03/2024", "", ""),
                Row("ES060000000022", regaCode, regaCode, regaCode, "Merina", "Hembra", "01/01/2024", "01/03/2024", "01/03/2024", "", "", "10/03/2024")),
            LivestockSpecies.Ovine,
            regaCode,
            new DateOnly(2026, 7, 29));

        rows.Should().OnlyContain(row => row.Status == "invalid" && !row.Processable);
        rows[0].Message.Should().Contain("dd/MM/aaaa");
        rows[1].Message.Should().Contain("dado de baja");
    }

    [Fact]
    public void Parser_NormalizesSpanishSexValues_ToApplicationCanonicalValues()
    {
        const string regaCode = "ES061600000720";
        var rows = FarmAnimalImportParser.Parse(
            "animals.xls",
            BuildDocument(
                Row("ES060000000041", regaCode, regaCode, regaCode, "Merina", " HEMBRA ", "01/01/2024", "01/03/2024", "01/03/2024", "", ""),
                Row("ES060000000042", regaCode, regaCode, regaCode, "Merina", "Macho", "01/01/2024", "01/03/2024", "01/03/2024", "", ""),
                Row("ES060000000043", regaCode, regaCode, regaCode, "Merina", "H", "01/01/2024", "01/03/2024", "01/03/2024", "", ""),
                Row("ES060000000044", regaCode, regaCode, regaCode, "Merina", "m", "01/01/2024", "01/03/2024", "01/03/2024", "", "")),
            LivestockSpecies.Ovine,
            regaCode,
            new DateOnly(2026, 7, 29));

        rows.Select(row => row.Sex).Should().Equal("Female", "Male", "Female", "Male");
        rows.Should().OnlyContain(row => row.Processable);
    }

    [Fact]
    public void Parser_ParsesProvidedAnimalesPertenecientesDocument()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        string? filePath = null;
        while (directory is not null && filePath is null)
        {
            var candidate = Path.Combine(directory.FullName, "info", "AnimalesPertenecientes(112).xls");
            if (File.Exists(candidate))
            {
                filePath = candidate;
            }

            directory = directory.Parent;
        }

        if (filePath is null)
        {
            return;
        }

        var content = File.ReadAllText(filePath);

        var rows = FarmAnimalImportParser.Parse(
            Path.GetFileName(filePath),
            content,
            LivestockSpecies.Ovine,
            "ES061600000720",
            new DateOnly(2026, 7, 29));

        rows.Should().HaveCount(499);
        rows.Should().OnlyContain(row => row.Processable);
        rows.Should().Contain(row => row.Status == "warning");
        rows.Count(row => row.Sex == "Female").Should().Be(484);
        rows.Count(row => row.Sex == "Male").Should().Be(15);
    }

    private static string BuildDocument(params string[] rows) =>
        $$"""
          <?xml version="1.0" encoding="UTF-8"?>
          <html xmlns="http://www.w3.org/1999/xhtml">
            <body>
              <table>
                <tr>
                  <th></th>
                  <th>Crotal</th>
                  <th>Código Explotación Pertenencia</th>
                  <th>Código Explotación Ubicación</th>
                  <th>Código Explotación Nacimiento</th>
                  <th>Raza</th>
                  <th>Sexo</th>
                  <th>Fecha Nacimiento</th>
                  <th>Fecha Inicio Explotación Pertenencia</th>
                  <th>Fecha Inicio Explotación Ubicación</th>
                  <th>Fecha Crotalización</th>
                  <th>Fecha Comunicación Crotalización</th>
                  <th>Fecha Baja</th>
                  <th>Fecha Comunicación Baja</th>
                </tr>
                {{string.Join(Environment.NewLine, rows)}}
              </table>
            </body>
          </html>
          """;

    private static string Row(
        string identification,
        string belongingRega,
        string locationRega,
        string originRega,
        string breed,
        string sex,
        string birthDate,
        string registrationDate,
        string locationDate,
        string identificationDate,
        string communicationDate,
        string dischargeDate = "",
        string dischargeCommunicationDate = "") =>
        $"""
         <tr>
           <td></td><td>{identification}</td><td>{belongingRega}</td><td>{locationRega}</td>
           <td>{originRega}</td><td>{breed}</td><td>{sex}</td><td>{birthDate}</td>
           <td>{registrationDate}</td><td>{locationDate}</td><td>{identificationDate}</td>
           <td>{communicationDate}</td><td>{dischargeDate}</td><td>{dischargeCommunicationDate}</td>
         </tr>
         """;
}
