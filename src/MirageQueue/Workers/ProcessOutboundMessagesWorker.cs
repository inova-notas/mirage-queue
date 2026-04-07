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

                if (message.ProcessingToken is null)
                {
                    _logger.LogWarning("Skipping outbound message {MessageId} because processing token is missing", message.Id);
                    continue;
                }

                using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var heartbeatTask = RunHeartbeat(message.Id, message.ProcessingToken.Value, heartbeatCts.Token);

                try
                {
                    var result = await messageHandler.ProcessOutboundMessage(message);

                    if (result.Exception is not null)
                    {
                        var updated = await outboundRepository.TryUpdateMessageStatus(
                            message.Id,
                            message.ProcessingToken.Value,
                            OutboundMessageStatus.Failed,
                            result.Exception?.InnerException?.Message ?? result.Exception?.Message,
                            result.Exception?.InnerException?.ToString() ?? result.Exception?.ToString(),
                            result.Exception?.InnerException?.GetType().FullName ?? result.Exception?.GetType().FullName);

                        if (!updated)
                            _logger.LogWarning("Message {MessageId} ownership lost before failure update", message.Id);
                    }
                    else
                    {
                        var updated = await outboundRepository.TryUpdateMessageStatus(
                            message.Id,
                            message.ProcessingToken.Value,
                            OutboundMessageStatus.Processed);

                        if (!updated)
                            _logger.LogWarning("Message {MessageId} ownership lost before processed update", message.Id);
                    }
                }
                finally
                {
                    await heartbeatCts.CancelAsync();
                    await heartbeatTask;
                }
            }
        }
    }

    async Task RunHeartbeat(Guid messageId, Guid processingToken, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_configuration.HeartbeatIntervalInMilliseconds), cancellationToken);

                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();
                var updated = await repository.TryUpdateHeartbeat(messageId, processingToken);

                if (!updated)
                {
                    _logger.LogWarning("Stopping heartbeat for message {MessageId} because ownership was lost", messageId);
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating heartbeat for message {MessageId}", messageId);
            }
        }
    }
}
