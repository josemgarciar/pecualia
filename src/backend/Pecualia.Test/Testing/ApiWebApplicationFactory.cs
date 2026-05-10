using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Pecualia.Test.Testing;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
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
                ["Stripe:SecretKey"] = "sk_test_dummy",
                ["Stripe:WebhookSecret"] = "whsec_dummy",
                ["Stripe:ManagerProfessionalMonthlyPriceId"] = "price_manager_pro",
                ["Stripe:ManagerEnterpriseMonthlyPriceId"] = "price_manager_enterprise",
                ["Stripe:FarmerProfessionalMonthlyPriceId"] = "price_farmer_pro"
            });
        });
    }
}
