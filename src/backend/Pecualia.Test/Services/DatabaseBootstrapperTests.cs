using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;
using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class DatabaseBootstrapperTests
{
    [Fact]
    public async Task BootstrapAsync_ReturnsImmediately_WhenBootstrapIsDisabled()
    {
        var service = new DatabaseBootstrapper(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Database=test;Username=test;Password=test"
            }).Build(),
            new StubHostEnvironment(Directory.GetCurrentDirectory()),
            Options.Create(new DatabaseBootstrapOptions
            {
                BootstrapOnStartup = false,
                SeedDemoData = false
            }),
            NullLogger<DatabaseBootstrapper>.Instance);

        var action = () => service.BootstrapAsync(CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public void ResolveDbDirectory_FindsRepositoryDbFolder()
    {
        var contentRoot = Path.Combine(Directory.GetCurrentDirectory(), "src", "backend", "Pecualia.Test");

        var dbDirectory = InvokePrivateStatic<string>("ResolveDbDirectory", contentRoot);

        var expectedLocations = new[]
        {
            Path.Combine("pecualia", "db"),
            Path.Combine("Pecualia.Test", "bin", "Release", "net8.0", "db")
        };

        expectedLocations.Any(expected => dbDirectory.EndsWith(expected, StringComparison.Ordinal))
            .Should()
            .BeTrue($"expected '{dbDirectory}' to resolve either the repository db folder or the published test output copy");
        File.Exists(Path.Combine(dbDirectory, "init", "001_schema.sql")).Should().BeTrue();
    }

    [Fact]
    public void BuildExecutableScripts_OrdersScripts_AndCanExcludeSeedData()
    {
        var dbDirectory = InvokePrivateStatic<string>("ResolveDbDirectory", AppContext.BaseDirectory);

        var withoutSeed = InvokePrivateStatic<IReadOnlyList<object>>("BuildExecutableScripts", dbDirectory, false);
        var withSeed = InvokePrivateStatic<IReadOnlyList<object>>("BuildExecutableScripts", dbDirectory, true);
        var idsWithoutSeed = withoutSeed.Select(GetScriptId).ToList();
        var idsWithSeed = withSeed.Select(GetScriptId).ToList();

        idsWithoutSeed.Should().Contain("001_schema.sql");
        idsWithoutSeed.Should().NotContain("002_seed_demo.sql");
        idsWithoutSeed.Should().NotContain("005_seed_demo_porcine.sql");
        idsWithSeed.Should().Contain("002_seed_demo.sql");
        idsWithSeed.Should().Contain("005_seed_demo_porcine.sql");
        idsWithSeed.Should().ContainInOrder("001_schema.sql", "002_seed_demo.sql", "003_animal_birth_farm_id.sql");
    }

    [Fact]
    public void BuildMigrationScripts_ReturnsSortedSqlFiles()
    {
        var dbDirectory = InvokePrivateStatic<string>("ResolveDbDirectory", AppContext.BaseDirectory);

        var scripts = InvokePrivateStatic<IReadOnlyList<object>>("BuildMigrationScripts", dbDirectory);
        var ids = scripts.Select(GetScriptId).ToList();

        ids.First().Should().Be("003_animal_birth_farm_id.sql");
        ids.Last().Should().Be("022_drop_farmer_second_surname.sql");
    }

    [Fact]
    public void IsSeedScript_DetectsSeedRelatedScripts()
    {
        InvokePrivateStatic<bool>("IsSeedScript", "002_seed_demo.sql").Should().BeTrue();
        InvokePrivateStatic<bool>("IsSeedScript", "005_seed_demo_porcine.sql").Should().BeTrue();
        InvokePrivateStatic<bool>("IsSeedScript", "010_password_reset_token.sql").Should().BeFalse();
    }

    private static T InvokePrivateStatic<T>(string methodName, params object[] arguments)
    {
        var method = typeof(DatabaseBootstrapper).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();
        return (T)method!.Invoke(null, arguments)!;
    }

    private static string GetScriptId(object script)
    {
        return (string)script.GetType().GetProperty("Id")!.GetValue(script)!;
    }

    private sealed class StubHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Pecualia.Test";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
