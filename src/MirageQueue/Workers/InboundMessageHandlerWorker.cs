using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;

namespace MirageQueue.Workers;

public abstract class InboundMessageHandlerWorker(
    IServiceProvider serviceProvider,
    ILogger<InboundMessageHandlerWorker> logger,
    MirageQueueConfiguration configuration)
    : BackgroundService, IMessageHandlerWorker
{
    private readonly Random _random = new();
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(10, 300)), stoppingToken);
        logger.LogInformation("Started Inbound message worker");

        await using var scope = serviceProvider.CreateAsyncScope();
        var messageHandler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
        var dbContext = GetContext(scope);

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

            try
            {
                await messageHandler.HandleQueuedInboundMessages(transaction);
                await dbContext.SaveChangesAsync(stoppingToken);
                await transaction.CommitAsync(stoppingToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error processing inbound messages");
                await transaction.RollbackAsync(stoppingToken);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(configuration.PoolingInboundTime), stoppingToken);
        }
        
        logger.LogInformation("Stopped Inbound message worker");
    }

    public abstract DbContext GetContext(AsyncServiceScope scope);
}