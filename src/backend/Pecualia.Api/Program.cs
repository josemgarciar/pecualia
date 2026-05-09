using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Pecualia.Api.Configuration;
using Pecualia.Api.Controllers;
using Pecualia.Api.Data;
using Pecualia.Api.Infrastructure.Email;
using Pecualia.Api.Infrastructure.Security;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;

var builder = WebApplication.CreateBuilder(args);
DotEnvLoader.LoadFromNearest(builder.Environment.ContentRootPath);
builder.Configuration.AddEnvironmentVariables();
ApplyRenderDerivedConfiguration(builder.Configuration);

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
QuestPDF.Settings.EnableDebugging = builder.Environment.IsDevelopment();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<ActivationOptions>(builder.Configuration.GetSection(ActivationOptions.SectionName));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<FrontendOptions>(builder.Configuration.GetSection(FrontendOptions.SectionName));
builder.Services.Configure<DatabaseBootstrapOptions>(builder.Configuration.GetSection(DatabaseBootstrapOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
ValidateRequiredSetting(jwtOptions.Issuer, "Jwt:Issuer");
ValidateRequiredSetting(jwtOptions.Audience, "Jwt:Audience");
ValidateRequiredSetting(jwtOptions.SigningKey, "Jwt:SigningKey");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
var postgresConnectionString = PostgresConnectionStringResolver.RequireNormalized(builder.Configuration);
var frontendOrigin = RequireConfigurationValue(builder.Configuration, "Frontend:Origin");

builder.Services.AddDbContext<PecualiaDbContext>(options =>
{
    options.UseNpgsql(postgresConnectionString);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.ManagerOnly, policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(claim =>
                (claim.Type == AuthClaimTypes.Role || claim.Type == ClaimTypes.Role) &&
                claim.Value == UserRole.Manager.ToString())));
    options.AddPolicy(AuthorizationPolicies.FarmerOrManager, policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim(claim =>
                (claim.Type == AuthClaimTypes.Role || claim.Type == ClaimTypes.Role) &&
                (claim.Value == UserRole.Manager.ToString() || claim.Value == UserRole.Farmer.ToString()))));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(frontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Pecualia API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAccountActivationService, AccountActivationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFarmerService, FarmerService>();
builder.Services.AddScoped<IFarmService, FarmService>();
builder.Services.AddScoped<IFarmOperationService, FarmOperationService>();
builder.Services.AddScoped<IAnimalService, AnimalService>();
builder.Services.AddScoped<IMovementService, MovementService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddSingleton<IDatabaseBootstrapper, DatabaseBootstrapper>();
builder.Services.AddSingleton<IClock, SystemClock>();

var emailOptions = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
var normalizedEmailMode = emailOptions.Mode.Trim();

if (normalizedEmailMode.Equals("Resend", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
    {
        client.BaseAddress = new Uri("https://api.resend.com/");
    });
}
else
{
    builder.Services.AddSingleton<IEmailSender, FileEmailSender>();
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<IDatabaseBootstrapper>();
    await bootstrapper.BootstrapAsync(CancellationToken.None);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

var hasFrontendBuild = File.Exists(Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html"));
if (hasFrontendBuild)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapGet("/health", async (PecualiaDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
    return Results.Ok(new
    {
        status = canConnect ? "ok" : "degraded",
        service = "pecualia-api",
        utc = DateTimeOffset.UtcNow
    });
});

app.MapAuthController();
app.MapFarmerController();
app.MapFarmController();
app.MapAnimalController();
app.MapMovementController();
app.MapDashboardController();

if (hasFrontendBuild)
{
    app.MapFallbackToFile("index.html");
}

app.Run();

static string RequireConfigurationValue(IConfiguration configuration, string key)
{
    var value = configuration[key];
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Falta la configuración obligatoria '{key}'. Revísala en el entorno o en el fichero .env.");
    }

    return value;
}

static void ValidateRequiredSetting(string value, string key)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Falta la configuración obligatoria '{key}'. Revísala en el entorno o en el fichero .env.");
    }
}

static void ApplyRenderDerivedConfiguration(IConfiguration configuration)
{
    var renderExternalHostname = configuration["RENDER_EXTERNAL_HOSTNAME"];
    if (string.IsNullOrWhiteSpace(renderExternalHostname))
    {
        return;
    }

    var publicBaseUrl = $"https://{renderExternalHostname}";
    if (string.IsNullOrWhiteSpace(configuration["Frontend:Origin"]))
    {
        configuration["Frontend:Origin"] = publicBaseUrl;
    }

    if (string.IsNullOrWhiteSpace(configuration["Activation:BaseUrl"]))
    {
        configuration["Activation:BaseUrl"] = $"{publicBaseUrl}/activate-account";
    }
}
