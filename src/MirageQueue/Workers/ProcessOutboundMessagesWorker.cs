using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;

namespace MirageQueue.Workers;

public class ProcessOutboundMessagesWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboundMessageHandlerWorker> _logger;
    private readonly MirageQueueConfiguration _configuration;
    private readonly Channel<OutboundMessage> _channel;

    public ProcessOutboundMessagesWorker(
        IServiceProvider serviceProvider,
        ILogger<OutboundMessageHandlerWorker> logger,
        MirageQueueConfiguration configuration,
        Channel<OutboundMessage> channel)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _channel = channel;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting {workerAmount} 'Processing' workers...", _configuration.WorkersQuantity);

        var tasks = new List<Task>();

        for (var i = 0; i < _configuration.WorkersQuantity; i++)
        {
            tasks.Add(Worker(Guid.NewGuid(), stoppingToken));
        }

        await Task.WhenAll(tasks.ToArray());
        
        _logger.LogInformation("All Outbound message workers stopped");
    }

    async Task Worker(Guid id, CancellationToken stoppingToken)
    {
        while (await _channel.Reader.WaitToReadAsync(stoppingToken))
        {
            using var scope = _serviceProvider.CreateScope();
            var messageHandler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
            var outboundRepository = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();
            
            while (_channel.Reader.TryRead(out var message))
            {
                _logger.LogInformation("Processing outbound message {MessageId} in worker {WorkerId}", message.Id, id);
                var result = await messageHandler.ProcessOutboundMessage(message);

                if (result.Exception is not null)
                {
                    await outboundRepository.UpdateMessageStatus(message.Id, OutboundMessageStatus.Failed);
                }
                else
                {
                    await outboundRepository.UpdateMessageStatus(message.Id, OutboundMessageStatus.Processed);
                }
            }
        }
    }
}