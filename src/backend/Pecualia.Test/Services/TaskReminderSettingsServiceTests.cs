using Pecualia.Api.Contracts.Auth;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class TaskReminderSettingsServiceTests
{
    [Fact]
    public async Task UpdateCurrentUserSettingsAsync_PersistsEnabledReminderSettings_AndAnchorsSchedule()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 16, 09, 00, 00, TimeSpan.Zero));
        var user = ServiceTestData.CreateUser(10, UserRole.Farmer, "Ana", "Sierra", email: "ana@test.local");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new TaskReminderSettingsService(dbContext, clock);

        var response = await service.UpdateCurrentUserSettingsAsync(
            user.Id,
            new UpdateTaskReminderSettingsRequest(true, "Avisos@Test.Local", 7),
            CancellationToken.None);

        response.Enabled.Should().BeTrue();
        response.Email.Should().Be("avisos@test.local");
        response.IntervalDays.Should().Be(7);
        user.TaskReminderEnabled.Should().BeTrue();
        user.TaskReminderEmail.Should().Be("avisos@test.local");
        user.TaskReminderIntervalDays.Should().Be(7);
        user.TaskReminderAnchorDate.Should().Be(new DateOnly(2026, 05, 16));
        user.TaskReminderLastProcessedOn.Should().BeNull();
        user.TaskReminderLastSentAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateCurrentUserSettingsAsync_RejectsEnabledReminderWithoutRecipientEmail()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 16, 09, 00, 00, TimeSpan.Zero));
        var user = ServiceTestData.CreateUser(11, UserRole.Manager, "Marta", "Gestora", email: "marta@test.local");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new TaskReminderSettingsService(dbContext, clock);
        var action = () => service.UpdateCurrentUserSettingsAsync(
            user.Id,
            new UpdateTaskReminderSettingsRequest(true, null, 5),
            CancellationToken.None);

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Debes indicar el correo al que quieres enviar los recordatorios.");
    }

    [Fact]
    public async Task UpdateCurrentUserSettingsAsync_ResetsSchedule_WhenFrequencyChanges()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 20, 10, 30, 00, TimeSpan.Zero));
        var user = ServiceTestData.CreateUser(12, UserRole.Farmer, "Luis", "Campo", email: "luis@test.local");
        user.TaskReminderEnabled = true;
        user.TaskReminderEmail = "avisos@old.test";
        user.TaskReminderIntervalDays = 7;
        user.TaskReminderAnchorDate = new DateOnly(2026, 05, 01);
        user.TaskReminderLastProcessedOn = new DateOnly(2026, 05, 15);
        user.TaskReminderLastSentAt = new DateTimeOffset(2026, 05, 15, 07, 00, 00, TimeSpan.Zero);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new TaskReminderSettingsService(dbContext, clock);

        await service.UpdateCurrentUserSettingsAsync(
            user.Id,
            new UpdateTaskReminderSettingsRequest(true, "avisos@new.test", 14),
            CancellationToken.None);

        user.TaskReminderEnabled.Should().BeTrue();
        user.TaskReminderEmail.Should().Be("avisos@new.test");
        user.TaskReminderIntervalDays.Should().Be(14);
        user.TaskReminderAnchorDate.Should().Be(new DateOnly(2026, 05, 20));
        user.TaskReminderLastProcessedOn.Should().BeNull();
        user.TaskReminderLastSentAt.Should().BeNull();
    }
}
