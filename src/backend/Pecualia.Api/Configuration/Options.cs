namespace Pecualia.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "Pecualia.Api";

    public string Audience { get; init; } = "Pecualia.Frontend";

    public string SigningKey { get; init; } = string.Empty;

    public int ExpirationMinutes { get; init; } = 720;
}

public sealed class ActivationOptions
{
    public const string SectionName = "Activation";

    public string BaseUrl { get; init; } = "http://127.0.0.1:5173/activate-account";

    public int TokenHours { get; init; } = 72;
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Mode { get; init; } = "File";

    public string From { get; init; } = "no-reply@pecualia.local";

    public string SmtpHost { get; init; } = string.Empty;

    public int SmtpPort { get; init; } = 25;

    public string SmtpUsername { get; init; } = string.Empty;

    public string SmtpPassword { get; init; } = string.Empty;

    public string PickupDirectory { get; init; } = "App_Data/outbox";
}

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string Origin { get; init; } = "http://127.0.0.1:5173";
}
