using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MirageQueue.Messages.Repositories;

namespace MirageQueue.Workers;

public abstract class StuckProcessingRecoveryWorker(
    IServiceProvider serviceProvider,
    ILogger<StuckProcessingRecoveryWorker> logger,
    MirageQueueConfiguration configuration)
    : BackgroundService, IMessageHandlerWorker
{
    private readonly Random _random = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(10, 300)), stoppingToken);
        logger.LogInformation("Started Stuck Processing Recovery worker");

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var outboundRepository = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

            var dbContext = GetContext(scope);

            await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

            try
            {
                var recoveredCount = await outboundRepository.ResetStuckProcessingMessages(
                    configuration.ProcessingRecoveryTimeInMinutes, transaction);

                await transaction.CommitAsync(stoppingToken);

                if (recoveredCount > 0)
                {
                    logger.LogWarning("Recovered {RecoveredCount} stuck processing messages", recoveredCount);
                }
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync(stoppingToken);
                logger.LogError(e, "Error recovering stuck processing messages");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(configuration.PoolingRecoveryTime), stoppingToken);
        }

        logger.LogInformation("Stopped Stuck Processing Recovery worker");
    }

    public abstract DbContext GetContext(AsyncServiceScope scope);
}
