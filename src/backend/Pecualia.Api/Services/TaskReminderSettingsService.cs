using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Pecualia.Api.Contracts.Auth;
using Pecualia.Api.Data;

namespace Pecualia.Api.Services;

public interface ITaskReminderSettingsService
{
    Task<TaskReminderSettingsResponse> GetCurrentUserSettingsAsync(long userId, CancellationToken cancellationToken);

    Task<TaskReminderSettingsResponse> UpdateCurrentUserSettingsAsync(long userId, UpdateTaskReminderSettingsRequest request, CancellationToken cancellationToken);
}

public sealed class TaskReminderSettingsService(PecualiaDbContext dbContext, IClock clock) : ITaskReminderSettingsService
{
    public async Task<TaskReminderSettingsResponse> GetCurrentUserSettingsAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new DomainException("Usuario no encontrado.");
        }

        return Map(user);
    }

    public async Task<TaskReminderSettingsResponse> UpdateCurrentUserSettingsAsync(long userId, UpdateTaskReminderSettingsRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new DomainException("Usuario no encontrado.");
        }

        var normalizedEmail = NormalizeOptionalEmail(request.Email);
        var normalizedIntervalDays = request.IntervalDays;

        if (normalizedEmail is not null && !IsValidEmail(normalizedEmail))
        {
            throw new DomainException("El correo de recordatorios no es válido.");
        }

        if (normalizedIntervalDays is not null && normalizedIntervalDays <= 0)
        {
            throw new DomainException("La frecuencia de recordatorios debe ser mayor que 0 días.");
        }

        if (request.Enabled)
        {
            if (normalizedEmail is null)
            {
                throw new DomainException("Debes indicar el correo al que quieres enviar los recordatorios.");
            }

            if (normalizedIntervalDays is null)
            {
                throw new DomainException("Debes indicar cada cuántos días quieres recibir recordatorios.");
            }
        }

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var shouldResetSchedule =
            request.Enabled &&
            (
                !user.TaskReminderEnabled ||
                user.TaskReminderIntervalDays != normalizedIntervalDays ||
                user.TaskReminderAnchorDate is null
            );

        user.TaskReminderEnabled = request.Enabled;
        user.TaskReminderEmail = request.Enabled ? normalizedEmail : normalizedEmail ?? user.TaskReminderEmail;
        user.TaskReminderIntervalDays = request.Enabled ? normalizedIntervalDays : normalizedIntervalDays ?? user.TaskReminderIntervalDays;
        user.UpdatedAt = clock.UtcNow;

        if (shouldResetSchedule)
        {
            user.TaskReminderAnchorDate = today;
            user.TaskReminderLastProcessedOn = null;
            user.TaskReminderLastSentAt = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    private static TaskReminderSettingsResponse Map(Models.Entities.AppUser user)
    {
        return new TaskReminderSettingsResponse(
            user.TaskReminderEnabled,
            user.TaskReminderEmail,
            user.TaskReminderIntervalDays);
    }

    private static string? NormalizeOptionalEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
