using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(static options => !string.IsNullOrWhiteSpace(options.Issuer), "Falta la configuración obligatoria 'Jwt:Issuer'. Revísala en el entorno o en el fichero .env.")
    .Validate(static options => !string.IsNullOrWhiteSpace(options.Audience), "Falta la configuración obligatoria 'Jwt:Audience'. Revísala en el entorno o en el fichero .env.")
    .Validate(static options => !string.IsNullOrWhiteSpace(options.SigningKey), "Falta la configuración obligatoria 'Jwt:SigningKey'. Revísala en el entorno o en el fichero .env.")
    .ValidateOnStart();
builder.Services.Configure<ActivationOptions>(builder.Configuration.GetSection(ActivationOptions.SectionName));
builder.Services.Configure<PasswordResetOptions>(builder.Configuration.GetSection(PasswordResetOptions.SectionName));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<AuthCookieOptions>(builder.Configuration.GetSection(AuthCookieOptions.SectionName));
builder.Services.AddOptions<FrontendOptions>()
    .Bind(builder.Configuration.GetSection(FrontendOptions.SectionName))
    .Validate(static options => !string.IsNullOrWhiteSpace(options.Origin), "Falta la configuración obligatoria 'Frontend:Origin'. Revísala en el entorno o en el fichero .env.")
    .ValidateOnStart();
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
builder.Services.Configure<DatabaseBootstrapOptions>(builder.Configuration.GetSection(DatabaseBootstrapOptions.SectionName));
builder.Services.Configure<TaskReminderWorkerOptions>(builder.Configuration.GetSection(TaskReminderWorkerOptions.SectionName));

builder.Services.AddDbContext<PecualiaDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    options.UseNpgsql(PostgresConnectionStringResolver.RequireNormalized(configuration));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>, IOptions<AuthCookieOptions>>((options, jwtOptionsAccessor, authCookieOptionsAccessor) =>
    {
        var jwtOptions = jwtOptionsAccessor.Value;
        var authCookieOptions = authCookieOptionsAccessor.Value;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
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
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrWhiteSpace(context.Token) &&
                    context.Request.Cookies.TryGetValue(authCookieOptions.Name, out var cookieToken) &&
                    !string.IsNullOrWhiteSpace(cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            }
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

builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
    .Configure<IOptions<FrontendOptions>>((options, frontendOptionsAccessor) =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(frontendOptionsAccessor.Value.Origin)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Has superado temporalmente el límite de intentos. Inténtalo de nuevo en unos minutos." },
            cancellationToken);
    };
    options.AddPolicy("auth-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"auth-login:{GetClientIp(context)}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("auth-register", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"auth-register:{GetClientIp(context)}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("auth-recovery", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"auth-recovery:{GetClientIp(context)}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("auth-activation", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"auth-activation:{GetClientIp(context)}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
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
builder.Services.AddScoped<IAuthCookieService, AuthCookieService>();
builder.Services.AddScoped<IAccountActivationService, AccountActivationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITaskReminderSettingsService, TaskReminderSettingsService>();
builder.Services.AddScoped<IFarmerService, FarmerService>();
builder.Services.AddScoped<IFarmService, FarmService>();
builder.Services.AddScoped<IFarmOperationService, FarmOperationService>();
builder.Services.AddScoped<IFarmCensusProjectionService, FarmCensusProjectionService>();
builder.Services.AddScoped<IAnimalService, AnimalService>();
builder.Services.AddScoped<IMovementService, MovementService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IPendingTaskQueryService, PendingTaskQueryService>();
builder.Services.AddScoped<ITaskReminderProcessor, TaskReminderProcessor>();
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddSingleton<IDatabaseBootstrapper, DatabaseBootstrapper>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddHostedService<TaskReminderWorker>();

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

app.UseForwardedHeaders();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

var hasFrontendBuild = File.Exists(Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html"));
if (hasFrontendBuild)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "ok",
    service = "pecualia-api",
    utc = DateTimeOffset.UtcNow
}));
app.MapGet("/health/ready", async (PecualiaDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new
        {
            status = "ok",
            service = "pecualia-api",
            utc = DateTimeOffset.UtcNow
        })
        : Results.Json(
            new
            {
                status = "degraded",
                service = "pecualia-api",
                utc = DateTimeOffset.UtcNow
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
});
app.MapGet("/health", async (PecualiaDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new
        {
            status = "ok",
            service = "pecualia-api",
            utc = DateTimeOffset.UtcNow
        })
        : Results.Json(
            new
            {
                status = "degraded",
                service = "pecualia-api",
                utc = DateTimeOffset.UtcNow
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapAuthController();
app.MapFarmerController();
app.MapFarmController();
app.MapAnimalController();
app.MapMovementController();
app.MapDashboardController();
app.MapBillingController();

if (hasFrontendBuild)
{
    app.MapFallbackToFile("index.html");
}

app.Run();

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

    if (string.IsNullOrWhiteSpace(configuration["PasswordReset:BaseUrl"]))
    {
        configuration["PasswordReset:BaseUrl"] = $"{publicBaseUrl}/reset-password";
    }
}

static string GetClientIp(HttpContext context)
{
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public partial class Program
{
}
