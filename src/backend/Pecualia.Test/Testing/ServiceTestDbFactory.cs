using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pecualia.Api.Data;

namespace Pecualia.Test.Testing;

public static class ServiceTestDbFactory
{
    public static PecualiaDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<PecualiaDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .EnableSensitiveDataLogging()
            .Options;

        return new PecualiaDbContext(options);
    }
}
