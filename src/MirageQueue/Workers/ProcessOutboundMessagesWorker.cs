using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using MirageQueue.Retry;

namespace MirageQueue.Workers;

public class ProcessOutboundMessagesWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProcessOutboundMessagesWorker> _logger;
    private readonly MirageQueueConfiguration _configuration;
    private readonly Channel<OutboundMessage> _channel;
    private readonly OutboundChannelState _outboundChannelState;

    public ProcessOutboundMessagesWorker(
        IServiceProvider serviceProvider,
        ILogger<ProcessOutboundMessagesWorker> logger,
        MirageQueueConfiguration configuration,
        Channel<OutboundMessage> channel,
        OutboundChannelState outboundChannelState)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _channel = channel;
        _outboundChannelState = outboundChannelState;
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
            while (_channel.Reader.TryRead(out var message))
            {
                _outboundChannelState.DecrementPending();

                await using var scope = _serviceProvider.CreateAsyncScope();
                var messageHandler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
                var outboundRepository = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();

                _logger.LogDebug("Processing outbound message {MessageId} in worker {WorkerId}", message.Id, id);
                var result = await messageHandler.ProcessOutboundMessage(message);

                if (result.Exception is null)
                {
                    await outboundRepository.UpdateMessageStatus(message.Id, OutboundMessageStatus.Processed);
                    continue;
                }

                await HandleFailureAsync(message, result.Exception, outboundRepository);
            }
        }
    }

    internal static async Task HandleFailureAsync(OutboundMessage message, Exception exception, IOutboundMessageRepository outboundRepository)
    {
        var rootCause = exception.InnerException ?? exception;
        var errorMessage = rootCause.Message;
        var stackTrace = rootCause.ToString();
        var exceptionType = rootCause.GetType().FullName;

        var (policy, hasExplicitPolicy) = RetryPolicy.Resolve(message.ConsumerEndpoint);
        var newAttempts = message.AttemptCount + 1;

        if (newAttempts < policy.MaxAttempts)
        {
            var nextRetryAt = DateTime.UtcNow + policy.Backoff.ComputeDelay(newAttempts);
            await outboundRepository.MarkForRetry(message.Id, newAttempts, nextRetryAt, errorMessage, stackTrace, exceptionType);
            return;
        }

        if (hasExplicitPolicy)
        {
            await outboundRepository.MarkDeadLettered(message.Id, errorMessage, stackTrace, exceptionType);
        }
        else
        {
            await outboundRepository.UpdateMessageStatus(message.Id, OutboundMessageStatus.Failed, errorMessage, stackTrace, exceptionType);
        }
    }
}
