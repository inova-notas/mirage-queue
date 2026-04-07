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
                var processingTokens = CreateProcessingTokens(configuration.WorkersQuantity);
                var messages = await messageHandler.HandleQueuedOutboundMessages(processingTokens, transaction);
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
                        if (message.ProcessingToken is null)
                            continue;

                        await outboundRepository.TryUpdateMessageStatus(
                            message.Id,
                            message.ProcessingToken.Value,
                            OutboundMessageStatus.Failed,
                            e.Message,
                            e.ToString(),
                            e.GetType().FullName);
                    }
                }

                if (messages.Count == 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(configuration.PoolingOutboundTime), stoppingToken);
            }
            catch (Exception e)
            {
                await transaction.RollbackAsync(stoppingToken);
                logger.LogError(e, "Error processing outbound messages");
                await Task.Delay(TimeSpan.FromMilliseconds(configuration.PoolingOutboundTime), stoppingToken);
            }
        }
        
        logger.LogInformation("Stopped Inbound message worker");
    }

    private static List<Guid> CreateProcessingTokens(int amount)
    {
        var tokens = new List<Guid>(amount);
        for (var i = 0; i < amount; i++)
            tokens.Add(Guid.NewGuid());

        return tokens;
    }

    public abstract DbContext GetContext(AsyncServiceScope scope);
}
