using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Pecualia.Api.Configuration;
using Pecualia.Api.Contracts.Dashboard;
using Pecualia.Api.Data;
using Pecualia.Api.Infrastructure.Email;
using Pecualia.Api.Models.Entities;

namespace Pecualia.Api.Services;

public interface ITaskReminderProcessor
{
    Task ProcessDueRemindersAsync(CancellationToken cancellationToken);
}

public sealed class TaskReminderProcessor(
    PecualiaDbContext dbContext,
    IPendingTaskQueryService pendingTaskQueryService,
    IEmailSender emailSender,
    IClock clock,
    IOptions<FrontendOptions> frontendOptions,
    IOptions<TaskReminderWorkerOptions> workerOptions,
    ILogger<TaskReminderProcessor> logger) : ITaskReminderProcessor
{
    private const long AdvisoryLockKey = 482_001_338;
    private readonly FrontendOptions _frontendOptions = frontendOptions.Value;
    private readonly TaskReminderWorkerOptions _workerOptions = workerOptions.Value;

    public async Task ProcessDueRemindersAsync(CancellationToken cancellationToken)
    {
        await using var advisoryLock = await TryAcquireAdvisoryLockAsync(cancellationToken);
        if (advisoryLock is { Acquired: false })
        {
            logger.LogInformation("Task reminder worker skipped because another instance already holds the advisory lock.");
            return;
        }

        var today = GetLocalToday();
        var users = await dbContext.Users
            .Where(entity => entity.IsActive && entity.TaskReminderEnabled)
            .OrderBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            await ProcessUserAsync(user, today, cancellationToken);
        }
    }

    private async Task ProcessUserAsync(AppUser user, DateOnly today, CancellationToken cancellationToken)
    {
        var recipientEmail = NormalizeOptional(user.TaskReminderEmail);
        var intervalDays = user.TaskReminderIntervalDays;
        var anchorDate = user.TaskReminderAnchorDate;

        if (recipientEmail is null || intervalDays is null || intervalDays <= 0 || anchorDate is null)
        {
            logger.LogWarning("Skipping task reminders for user {UserId} because the configuration is incomplete.", user.Id);
            return;
        }

        var scheduledDate = TaskReminderSchedule.GetLatestScheduledDate(anchorDate.Value, intervalDays.Value, today);
        if (scheduledDate is null || user.TaskReminderLastProcessedOn >= scheduledDate.Value)
        {
            return;
        }

        var tasks = await pendingTaskQueryService.GetPendingTasksAsync(
            user.Id,
            user.Role,
            today,
            clock.UtcNow.UtcDateTime,
            cancellationToken);

        if (tasks.Count > 0)
        {
            await emailSender.SendAsync(
                BuildReminderEmail(recipientEmail, user, tasks, today),
                cancellationToken);

            user.TaskReminderLastSentAt = clock.UtcNow;
        }

        user.TaskReminderLastProcessedOn = scheduledDate.Value;
        user.UpdatedAt = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private EmailMessage BuildReminderEmail(string recipientEmail, AppUser user, IReadOnlyList<DashboardTaskResponse> tasks, DateOnly today)
    {
        var dashboardUrl = $"{_frontendOptions.Origin.TrimEnd('/')}/app/dashboard";
        var greetingName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(user.Name) ? "usuario" : user.Name.Trim());
        var listItems = string.Join(string.Empty, tasks.Select(task =>
            $$"""
              <li style="margin:0 0 12px 0;">
                <strong style="display:block;color:#1e2a24;">{{WebUtility.HtmlEncode(task.Title)}}</strong>
                <span style="color:#526158;line-height:1.5;">{{WebUtility.HtmlEncode(task.Detail)}}</span>
              </li>
            """));
        var plainTextList = string.Join(Environment.NewLine, tasks.Select(task => $"- {task.Title}: {task.Detail}"));
        var htmlBody =
            $$"""
            <!DOCTYPE html>
            <html lang="es">
              <body style="margin:0;padding:24px;background:#f6f8f5;font-family:Inter,Segoe UI,Arial,sans-serif;color:#1e2a24;">
                <div style="max-width:640px;margin:0 auto;background:#ffffff;border:1px solid #d7ded8;border-radius:24px;padding:32px;">
                  <p style="margin:0 0 12px 0;">Hola {{greetingName}},</p>
                  <p style="margin:0 0 20px 0;color:#526158;line-height:1.6;">
                    Este es tu recordatorio de tareas pendientes en Pecualia a fecha de {{today:dd/MM/yyyy}}.
                  </p>
                  <ul style="padding-left:20px;margin:0 0 24px 0;">
                    {{listItems}}
                  </ul>
                  <a href="{{WebUtility.HtmlEncode(dashboardUrl)}}" style="display:inline-block;padding:12px 18px;border-radius:999px;background:#2f6b4f;color:#ffffff;text-decoration:none;font-weight:700;">
                    Abrir dashboard
                  </a>
                </div>
              </body>
            </html>
            """;
        var plainTextBody =
            $$"""
            Hola {{user.Name}},

            Este es tu recordatorio de tareas pendientes en Pecualia a fecha de {{today:dd/MM/yyyy}}.

            {{plainTextList}}

            Dashboard: {{dashboardUrl}}
            """;

        return new EmailMessage(
            recipientEmail,
            "Recordatorio de tareas pendientes en Pecualia",
            htmlBody,
            plainTextBody);
    }

    private DateOnly GetLocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_workerOptions.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(clock.UtcNow, timeZone);
        return DateOnly.FromDateTime(localNow.DateTime);
    }

    private async Task<AdvisoryLockHandle?> TryAcquireAdvisoryLockAsync(CancellationToken cancellationToken)
    {
        var providerName = dbContext.Database.ProviderName ?? string.Empty;
        if (!providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return AdvisoryLockHandle.Bypassed();
        }

        var connectionString = dbContext.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return AdvisoryLockHandle.Bypassed();
        }

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@lockKey);", connection);
            command.Parameters.AddWithValue("lockKey", AdvisoryLockKey);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is true)
            {
                return AdvisoryLockHandle.CreateAcquired(connection);
            }

            await connection.DisposeAsync();
            return AdvisoryLockHandle.CreateNotAcquired();
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class AdvisoryLockHandle : IAsyncDisposable
    {
        private readonly NpgsqlConnection? _connection;

        private AdvisoryLockHandle(bool acquired, NpgsqlConnection? connection)
        {
            Acquired = acquired;
            _connection = connection;
        }

        public bool Acquired { get; }

        public static AdvisoryLockHandle CreateAcquired(NpgsqlConnection connection) => new(true, connection);

        public static AdvisoryLockHandle CreateNotAcquired() => new(false, null);

        public static AdvisoryLockHandle Bypassed() => new(true, null);

        public async ValueTask DisposeAsync()
        {
            if (_connection is null)
            {
                return;
            }

            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@lockKey);", _connection);
                command.Parameters.AddWithValue("lockKey", AdvisoryLockKey);
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }
}

internal static class TaskReminderSchedule
{
    public static DateOnly? GetLatestScheduledDate(DateOnly anchorDate, int intervalDays, DateOnly today)
    {
        if (intervalDays <= 0 || anchorDate > today)
        {
            return null;
        }

        var elapsedDays = today.DayNumber - anchorDate.DayNumber;
        var completedIntervals = elapsedDays / intervalDays;
        return anchorDate.AddDays(completedIntervals * intervalDays);
    }
}
