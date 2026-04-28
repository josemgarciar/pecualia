using System.Text;
using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;

namespace Pecualia.Api.Infrastructure.Email;

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string PlainTextBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed class FileEmailSender(IHostEnvironment environment, IOptions<EmailOptions> options, ILogger<FileEmailSender> logger)
    : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var pickupDirectory = Path.Combine(environment.ContentRootPath, _options.PickupDirectory);
        Directory.CreateDirectory(pickupDirectory);

        var safeFileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Sanitize(message.To)}.txt";
        var filePath = Path.Combine(pickupDirectory, safeFileName);
        var content = new StringBuilder()
            .AppendLine($"To: {message.To}")
            .AppendLine($"From: {_options.From}")
            .AppendLine($"Subject: {message.Subject}")
            .AppendLine()
            .AppendLine(message.PlainTextBody)
            .AppendLine()
            .AppendLine("HTML")
            .AppendLine(message.HtmlBody)
            .ToString();

        await File.WriteAllTextAsync(filePath, content, cancellationToken);
        logger.LogInformation("Activation email written to {FilePath}", filePath);
    }

    private static string Sanitize(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
    }
}
