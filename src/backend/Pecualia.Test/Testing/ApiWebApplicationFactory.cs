using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pecualia.Api.Data;
using Pecualia.Api.Infrastructure.Email;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Services;

namespace Pecualia.Test.Testing;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Port=5432;Database=pecualia_test;Username=postgres;Password=postgres",
                ["Jwt:Issuer"] = "pecualia-tests",
                ["Jwt:Audience"] = "pecualia-tests",
                ["Jwt:SigningKey"] = "pecualia-tests-signing-key-0123456789012345",
                ["Activation:BaseUrl"] = "http://localhost/activate-account",
                ["Frontend:Origin"] = "http://127.0.0.1:4173",
                ["Email:Mode"] = "File",
                ["Email:From"] = "tests@pecualia.local",
                ["Email:ReplyTo"] = "tests@pecualia.local",
                ["Database:BootstrapOnStartup"] = "false",
                ["Database:SeedDemoData"] = "false",
                ["Stripe:SecretKey"] = string.Empty,
                ["Stripe:WebhookSecret"] = string.Empty,
                ["Stripe:ManagerProfessionalMonthlyPriceId"] = "price_manager_pro",
                ["Stripe:ManagerEnterpriseMonthlyPriceId"] = "price_manager_enterprise",
                ["Stripe:FarmerProfessionalMonthlyPriceId"] = "price_farmer_pro"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<PecualiaDbContext>();
            services.RemoveAll<DbContextOptions<PecualiaDbContext>>();
            services.RemoveAll<IDatabaseBootstrapper>();
            services.RemoveAll<IClock>();
            services.RemoveAll<IEmailSender>();

            services.AddDbContext<PecualiaDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
                    .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            services.AddSingleton<IDatabaseBootstrapper, NoOpDatabaseBootstrapper>();
            services.AddSingleton<IClock>(new TestClock(new DateTimeOffset(2026, 05, 15, 10, 0, 0, TimeSpan.Zero)));
            services.AddSingleton<IEmailSender, NullEmailSender>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public async Task SeedAsync(Func<PecualiaDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PecualiaDbContext>();
        await seed(dbContext);
        await dbContext.SaveChangesAsync();
    }

    private sealed class NoOpDatabaseBootstrapper : IDatabaseBootstrapper
    {
        public Task BootstrapAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NullEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        internal const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var userId = Request.Headers["X-Test-UserId"].ToString();
            var role = Request.Headers["X-Test-Role"].ToString();

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing test auth headers."));
            }

            var claims = new[]
            {
                new Claim(AuthClaimTypes.UserId, userId),
                new Claim(AuthClaimTypes.Role, role),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
