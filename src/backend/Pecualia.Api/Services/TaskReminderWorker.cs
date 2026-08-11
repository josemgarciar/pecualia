using Pecualia.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Pecualia.Api.Services;

public sealed class TaskReminderWorker(
    IServiceScopeFactory scopeFactory,
    DatabaseBootstrapState databaseBootstrapState,
    IOptions<TaskReminderWorkerOptions> options,
    ILogger<TaskReminderWorker> logger) : BackgroundService
{
    private readonly TaskReminderWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromMinutes(Math.Max(1, _options.PollIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!databaseBootstrapState.IsReady)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<ITaskReminderProcessor>();
                await processor.ProcessDueRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Task reminder worker failed while processing pending reminders.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
