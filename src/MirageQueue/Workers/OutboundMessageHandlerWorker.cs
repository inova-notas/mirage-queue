using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;

namespace MirageQueue.Workers;

public abstract class OutboundMessageHandlerWorker(
    IServiceProvider serviceProvider,
    ILogger<OutboundMessageHandlerWorker> logger,
    MirageQueueConfiguration configuration)
    : BackgroundService, IMessageHandlerWorker
{
    private readonly Random _random = new();
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting {workerAmount} Outbound message workers...", configuration.WorkersAmount);

        var tasks = new List<Task>();

        for (var i = 0; i < configuration.WorkersAmount; i++)
        {
            tasks.Add(Worker(Guid.NewGuid(), stoppingToken));
        }

        await Task.WhenAll(tasks.ToArray());
    }

    private async Task Worker(Guid workerId, CancellationToken stoppingToken)
    {
        logger.LogInformation("Started Outbound message worker {WorkerId}", workerId);

        await using var scope = serviceProvider.CreateAsyncScope();
        var messageHandler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
        var dbContext = GetContext(scope);

        while (!stoppingToken.IsCancellationRequested)
        {
            
            var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);
            await messageHandler.HandleQueuedOutboundMessages(transaction);

            await dbContext.SaveChangesAsync(stoppingToken);
            await transaction.CommitAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(configuration.PoolingTime), stoppingToken);
        }
    }

    public abstract DbContext GetContext(AsyncServiceScope scope);
}