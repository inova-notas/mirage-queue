using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MirageQueue.Messages.Repositories;

namespace MirageQueue.Workers;

public abstract class MessageCleanupWorker(
    IServiceProvider serviceProvider,
    ILogger<MessageCleanupWorker> logger,
    MirageQueueConfiguration configuration)
    : BackgroundService
{
    private readonly Random _random = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.CleanupEnabled)
        {
            logger.LogInformation("Message cleanup is disabled (CleanupEnabled = false). Set the option to true to enable retention.");
            return;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(50, 500)), stoppingToken);
        logger.LogInformation("Started message cleanup (retention {Days}d, batch {Batch}, polling {PollingMs}ms)",
            configuration.MessageRetentionDays, configuration.CleanupBatchSize, configuration.CleanupPollingTime);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error during message cleanup sweep");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(configuration.CleanupPollingTime), stoppingToken);
        }

        logger.LogInformation("Stopped message cleanup");
    }

    internal async Task CleanupOnceAsync(CancellationToken stoppingToken)
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromDays(configuration.MessageRetentionDays);

        await using var scope = serviceProvider.CreateAsyncScope();
        var outbound = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();
        var inbound = scope.ServiceProvider.GetRequiredService<IInboundMessageRepository>();
        var scheduled = scope.ServiceProvider.GetRequiredService<IScheduledMessageRepository>();

        // Order: outbound first (frees children before parent), inbound second (with NOT EXISTS guard
        // so any non-terminal child still blocks cleanup), scheduled is independent.
        var outboundDeleted = await outbound.DeleteTerminalOlderThan(cutoff, configuration.CleanupBatchSize);
        var inboundDeleted = await inbound.DeleteQueuedOlderThanWithNoActiveOutbound(cutoff, configuration.CleanupBatchSize);
        var scheduledDeleted = await scheduled.DeleteQueuedOlderThan(cutoff, configuration.CleanupBatchSize);

        if (outboundDeleted + inboundDeleted + scheduledDeleted > 0)
        {
            logger.LogInformation("Cleanup swept: outbound={Outbound}, inbound={Inbound}, scheduled={Scheduled}",
                outboundDeleted, inboundDeleted, scheduledDeleted);
        }
    }
}
