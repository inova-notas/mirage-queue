using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;

namespace MirageQueue.Workers;

public abstract class ScheduledMessageHandlerWorker(
    IServiceProvider serviceProvider,
    ILogger<ScheduledMessageHandlerWorker> logger,
    MirageQueueConfiguration configuration)
    : BackgroundService, IMessageHandlerWorker
{
    private readonly Random _random = new();
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(10, 300)), stoppingToken);
        logger.LogInformation("Started Scheduled message worker");
        
        await using var scope = serviceProvider.CreateAsyncScope();
        var messageHandler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
        var dbContext = GetContext(scope);

        while (!stoppingToken.IsCancellationRequested)
        {
            
            await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

            try
            {
                await messageHandler.HandleScheduledMessages(transaction);
                await dbContext.SaveChangesAsync(stoppingToken);
                await transaction.CommitAsync(stoppingToken);
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync(stoppingToken);
                logger.LogError(e, "Error processing scheduled messages");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(configuration.PoolingScheduleTime), stoppingToken);
        }
        
        logger.LogInformation("Stopped Scheduled message worker");
    }

    public abstract DbContext GetContext(AsyncServiceScope scope);
}