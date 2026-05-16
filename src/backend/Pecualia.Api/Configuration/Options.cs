namespace Pecualia.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public int ExpirationMinutes { get; init; } = 720;
}

public sealed class ActivationOptions
{
    public const string SectionName = "Activation";

    public string BaseUrl { get; init; } = string.Empty;

    public int TokenHours { get; init; } = 72;
}

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public string BaseUrl { get; init; } = string.Empty;

    public int TokenMinutes { get; init; } = 30;
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Mode { get; init; } = string.Empty;

    public string From { get; init; } = string.Empty;

    public string ReplyTo { get; init; } = string.Empty;

    public string SmtpHost { get; init; } = string.Empty;

    public int SmtpPort { get; init; } = 25;

    public string SmtpUsername { get; init; } = string.Empty;

    public string SmtpPassword { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string WebhookSecret { get; init; } = string.Empty;

    public string PickupDirectory { get; init; } = "App_Data/outbox";
}

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string Origin { get; init; } = string.Empty;
}

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; init; } = string.Empty;

    public string WebhookSecret { get; init; } = string.Empty;

    public string ManagerProfessionalMonthlyPriceId { get; init; } = string.Empty;

    public string ManagerEnterpriseMonthlyPriceId { get; init; } = string.Empty;

    public string FarmerProfessionalMonthlyPriceId { get; init; } = string.Empty;
}

public sealed class DatabaseBootstrapOptions
{
    public const string SectionName = "Database";

    public bool BootstrapOnStartup { get; init; }

    public bool SeedDemoData { get; init; }
}

public sealed class TaskReminderWorkerOptions
{
    public const string SectionName = "TaskReminderWorker";

    public int PollIntervalMinutes { get; init; } = 60;

    public string TimeZoneId { get; init; } = "Europe/Madrid";
}
