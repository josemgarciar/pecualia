using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;
using Pecualia.Api.Contracts.Dashboard;
using Pecualia.Api.Infrastructure.Email;
using Pecualia.Api.Models.Enums;
using Pecualia.Api.Services;
using Pecualia.Test.Testing;

namespace Pecualia.Test.Services;

public sealed class TaskReminderProcessorTests
{
    [Fact]
    public async Task ProcessDueRemindersAsync_SendsOneEmail_WhenAUserHasDueTasks()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 16, 09, 00, 00, TimeSpan.Zero));
        var user = ServiceTestData.CreateUser(20, UserRole.Farmer, "Ana", "Sierra", email: "ana@test.local");
        user.TaskReminderEnabled = true;
        user.TaskReminderEmail = "avisos@test.local";
        user.TaskReminderIntervalDays = 7;
        user.TaskReminderAnchorDate = new DateOnly(2026, 05, 01);
        user.TaskReminderLastProcessedOn = new DateOnly(2026, 05, 08);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var emailSender = new CapturingEmailSender();
        var processor = CreateProcessor(
            dbContext,
            clock,
            emailSender,
            new StubPendingTaskQueryService(new Dictionary<long, IReadOnlyList<DashboardTaskResponse>>
            {
                [user.Id] =
                [
                    new DashboardTaskResponse(
                        "Inspection",
                        "Inspección programada",
                        "Dehesa Norte · Hoy",
                        "info",
                        new DateOnly(2026, 05, 16))
                ]
            }));

        await processor.ProcessDueRemindersAsync(CancellationToken.None);

        emailSender.Messages.Should().ContainSingle();
        emailSender.Messages[0].To.Should().Be("avisos@test.local");
        emailSender.Messages[0].Subject.Should().Be("Recordatorio de tareas pendientes en Pecualia");
        user.TaskReminderLastProcessedOn.Should().Be(new DateOnly(2026, 05, 15));
        user.TaskReminderLastSentAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_MarksCycleAsProcessed_WithoutSendingEmail_WhenThereAreNoTasks()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 16, 09, 00, 00, TimeSpan.Zero));
        var user = ServiceTestData.CreateUser(21, UserRole.Manager, "Marta", "Gestora", email: "marta@test.local");
        user.TaskReminderEnabled = true;
        user.TaskReminderEmail = "marta-reminders@test.local";
        user.TaskReminderIntervalDays = 7;
        user.TaskReminderAnchorDate = new DateOnly(2026, 05, 01);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var emailSender = new CapturingEmailSender();
        var processor = CreateProcessor(
            dbContext,
            clock,
            emailSender,
            new StubPendingTaskQueryService(new Dictionary<long, IReadOnlyList<DashboardTaskResponse>>()));

        await processor.ProcessDueRemindersAsync(CancellationToken.None);

        emailSender.Messages.Should().BeEmpty();
        user.TaskReminderLastProcessedOn.Should().Be(new DateOnly(2026, 05, 15));
        user.TaskReminderLastSentAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_ProcessesOnlyTheLatestOverdueCycle()
    {
        await using var dbContext = ServiceTestDbFactory.CreateContext();
        var clock = new TestClock(new DateTimeOffset(2026, 05, 29, 09, 00, 00, TimeSpan.Zero));
        var user = ServiceTestData.CreateUser(22, UserRole.Farmer, "Luis", "Campo", email: "luis@test.local");
        user.TaskReminderEnabled = true;
        user.TaskReminderEmail = "luis-reminders@test.local";
        user.TaskReminderIntervalDays = 7;
        user.TaskReminderAnchorDate = new DateOnly(2026, 05, 01);
        user.TaskReminderLastProcessedOn = new DateOnly(2026, 05, 01);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var emailSender = new CapturingEmailSender();
        var processor = CreateProcessor(
            dbContext,
            clock,
            emailSender,
            new StubPendingTaskQueryService(new Dictionary<long, IReadOnlyList<DashboardTaskResponse>>
            {
                [user.Id] =
                [
                    new DashboardTaskResponse(
                        "Vaccination",
                        "Vacunación clostridios pendiente",
                        "Sierra Sur · En 2 días",
                        "warning",
                        new DateOnly(2026, 05, 31))
                ]
            }));

        await processor.ProcessDueRemindersAsync(CancellationToken.None);

        emailSender.Messages.Should().ContainSingle();
        user.TaskReminderLastProcessedOn.Should().Be(new DateOnly(2026, 05, 29));
    }

    private static TaskReminderProcessor CreateProcessor(
        Pecualia.Api.Data.PecualiaDbContext dbContext,
        TestClock clock,
        CapturingEmailSender emailSender,
        IPendingTaskQueryService pendingTaskQueryService)
    {
        return new TaskReminderProcessor(
            dbContext,
            pendingTaskQueryService,
            emailSender,
            clock,
            Options.Create(new FrontendOptions
            {
                Origin = "https://pecualia.test"
            }),
            Options.Create(new TaskReminderWorkerOptions
            {
                PollIntervalMinutes = 60,
                TimeZoneId = "Europe/Madrid"
            }),
            NullLogger<TaskReminderProcessor>.Instance);
    }

    private sealed class StubPendingTaskQueryService(IReadOnlyDictionary<long, IReadOnlyList<DashboardTaskResponse>> tasksByUser)
        : IPendingTaskQueryService
    {
        public Task<IReadOnlyList<DashboardTaskResponse>> GetPendingTasksAsync(
            long userId,
            UserRole role,
            DateOnly today,
            DateTime now,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(tasksByUser.TryGetValue(userId, out var tasks) ? tasks : Array.Empty<DashboardTaskResponse>());
        }
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
