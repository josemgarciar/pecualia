using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pecualia.Api.Configuration;
using Pecualia.Api.Services;

namespace Pecualia.Test.Services;

public sealed class TaskReminderWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_ProcessesReminders_AndStopsWhenCancelled()
    {
        using var cancellationSource = new CancellationTokenSource();
        var processor = new RecordingTaskReminderProcessor(() => cancellationSource.Cancel());
        await using var provider = BuildProvider(processor);
        var worker = new TaskReminderWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new TaskReminderWorkerOptions
            {
                PollIntervalMinutes = 1
            }),
            NullLogger<TaskReminderWorker>.Instance);

        await worker.StartAsync(cancellationSource.Token);
        await worker.StopAsync(CancellationToken.None);

        processor.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_SwallowsProcessorExceptions_AndExitsOnCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        var processor = new FailingTaskReminderProcessor(() => cancellationSource.Cancel());
        await using var provider = BuildProvider(processor);
        var worker = new TaskReminderWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new TaskReminderWorkerOptions
            {
                PollIntervalMinutes = 1
            }),
            NullLogger<TaskReminderWorker>.Instance);

        var action = async () =>
        {
            await worker.StartAsync(cancellationSource.Token);
            await worker.StopAsync(CancellationToken.None);
        };

        await action.Should().NotThrowAsync();
        processor.InvocationCount.Should().Be(1);
    }

    private static ServiceProvider BuildProvider(ITaskReminderProcessor processor)
    {
        return new ServiceCollection()
            .AddScoped(_ => processor)
            .BuildServiceProvider();
    }

    private sealed class RecordingTaskReminderProcessor(Action onProcessed) : ITaskReminderProcessor
    {
        public int InvocationCount { get; private set; }

        public Task ProcessDueRemindersAsync(CancellationToken cancellationToken)
        {
            InvocationCount++;
            onProcessed();
            return Task.CompletedTask;
        }
    }

    private sealed class FailingTaskReminderProcessor(Action onProcessed) : ITaskReminderProcessor
    {
        public int InvocationCount { get; private set; }

        public Task ProcessDueRemindersAsync(CancellationToken cancellationToken)
        {
            InvocationCount++;
            onProcessed();
            throw new InvalidOperationException("boom");
        }
    }
}
