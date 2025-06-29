using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;

namespace MirageQueue.Workers;

public abstract class OutboundMessageHandlerWorker(
    IServiceProvider serviceProvider,
    ILogger<OutboundMessageHandlerWorker> logger,
    Channel<OutboundMessage> channel,
    MirageQueueConfiguration configuration)
    : BackgroundService, IMessageHandlerWorker
{
    private readonly Random _random = new();
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(10, 300)), stoppingToken);
        logger.LogInformation("Started Inbound message worker");

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var messageHandler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
            var outboundRepository = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();
            
            var dbContext = GetContext(scope);
            
            await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

            try
            {
                var messages = await messageHandler.HandleQueuedOutboundMessages(transaction);
                await dbContext.SaveChangesAsync(stoppingToken);
                await transaction.CommitAsync(stoppingToken);

                foreach (var message in messages)
                {
                    try
                    {
                        await channel.Writer.WriteAsync(message, stoppingToken);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Error while adding outbound message to channel {MessageId}", message.Id);
                        await outboundRepository.UpdateMessageStatus(message.Id, OutboundMessageStatus.Failed);
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync(stoppingToken);
                logger.LogError(e, "Error processing outbound messages");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(configuration.PoolingOutboundTime), stoppingToken);
        }
        
        logger.LogInformation("Stopped Inbound message worker");
    }

    public abstract DbContext GetContext(AsyncServiceScope scope);
}