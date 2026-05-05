using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;

namespace Pecualia.Api.Infrastructure.Email;

public sealed class ResendEmailSender(HttpClient httpClient, IOptions<EmailOptions> options, ILogger<ResendEmailSender> logger)
    : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ValidateConfiguration();

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new ResendSendEmailRequest(
                _options.From,
                [message.To],
                message.Subject,
                message.HtmlBody,
                message.PlainTextBody,
                string.IsNullOrWhiteSpace(_options.ReplyTo) ? null : _options.ReplyTo))
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Pecualia.Api", "1.0"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError(
            "Resend email send failed with status {StatusCode}. Response: {ResponseBody}",
            (int)response.StatusCode,
            errorBody);

        throw new InvalidOperationException("No se pudo enviar el correo electrónico mediante Resend.");
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Falta la configuración obligatoria 'Email:ApiKey' para usar Resend.");
        }

        if (string.IsNullOrWhiteSpace(_options.From))
        {
            throw new InvalidOperationException("Falta la configuración obligatoria 'Email:From' para usar Resend.");
        }
    }

    private sealed record ResendSendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("reply_to")] string? ReplyTo);
}
