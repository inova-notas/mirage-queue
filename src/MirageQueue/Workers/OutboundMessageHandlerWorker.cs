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
        var waitingForChannelCapacity = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var messageHandler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
            var outboundRepository = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();
            var outboundChannelState = scope.ServiceProvider.GetRequiredService<OutboundChannelState>();
            
            var dbContext = GetContext(scope);

            var channelCapacity = Math.Max(1, configuration.OutboundChannelCapacity);
            var availableSlots = channelCapacity - outboundChannelState.PendingCount;
            if (availableSlots <= 0)
            {
                if (!waitingForChannelCapacity)
                {
                    waitingForChannelCapacity = true;
                    logger.LogInformation(
                        "Outbound channel is full ({PendingCount}/{ChannelCapacity}). Waiting for available capacity",
                        outboundChannelState.PendingCount,
                        channelCapacity);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(configuration.PoolingOutboundTime), stoppingToken);
                continue;
            }

            if (waitingForChannelCapacity)
            {
                waitingForChannelCapacity = false;
                logger.LogInformation(
                    "Outbound channel has available capacity again ({PendingCount}/{ChannelCapacity})",
                    outboundChannelState.PendingCount,
                    channelCapacity);
            }

            var fetchLimit = Math.Min(configuration.WorkersQuantity, availableSlots);
            
            await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

            try
            {
                var messages = await messageHandler.HandleQueuedOutboundMessages(transaction, fetchLimit);
                await dbContext.SaveChangesAsync(stoppingToken);
                await transaction.CommitAsync(stoppingToken);

                foreach (var message in messages)
                {
                    try
                    {
                        await channel.Writer.WriteAsync(message, stoppingToken);
                        outboundChannelState.IncrementPending();
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Error while adding outbound message to channel {MessageId}", message.Id);
                        await outboundRepository.UpdateMessageStatus(
                            message.Id,
                            OutboundMessageStatus.Failed,
                            message.AttemptCount + 1,
                            e.Message,
                            e.ToString(),
                            e.GetType().FullName);
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
