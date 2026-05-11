using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using MirageQueue.Retry;

namespace MirageQueue.Workers;

public abstract class StuckProcessingReaperWorker(
    IServiceProvider serviceProvider,
    ILogger<StuckProcessingReaperWorker> logger,
    MirageQueueConfiguration configuration)
    : BackgroundService
{
    private const int BatchLimit = 50;
    private const string StuckErrorMessage = "Processing lease expired — worker assumed crashed";

    private readonly Random _random = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(_random.Next(50, 500)), stoppingToken);
        logger.LogInformation("Started stuck-Processing reaper (lease {Lease})", configuration.ProcessingLeaseDuration);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReapOnceAsync(stoppingToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error during stuck-Processing reaper sweep");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(configuration.StuckProcessingPollingTime), stoppingToken);
        }

        logger.LogInformation("Stopped stuck-Processing reaper");
    }

    internal async Task ReapOnceAsync(CancellationToken stoppingToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboundMessageRepository>();
        var dbContext = GetContext(scope);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

        var stuck = await repository.GetStuckProcessingMessages(configuration.ProcessingLeaseDuration, BatchLimit, transaction);

        foreach (var message in stuck)
        {
            var (policy, hasExplicitPolicy) = RetryPolicy.Resolve(message.ConsumerEndpoint);
            var newAttempts = message.AttemptCount + 1;

            if (newAttempts < policy.MaxAttempts)
            {
                var nextRetryAt = DateTime.UtcNow + policy.Backoff.ComputeDelay(newAttempts);
                await repository.MarkForRetry(message.Id, newAttempts, nextRetryAt, StuckErrorMessage, stackTrace: null, exceptionType: null, transaction);
                logger.LogWarning("Reclaimed stuck message {MessageId} for retry (attempt {Attempt}/{Max})", message.Id, newAttempts, policy.MaxAttempts);
            }
            else if (hasExplicitPolicy)
            {
                await repository.MarkDeadLettered(message.Id, StuckErrorMessage, stackTrace: null, exceptionType: null, transaction);
                logger.LogWarning("Dead-lettered stuck message {MessageId} after {Attempts} attempts", message.Id, newAttempts);
            }
            else
            {
                await repository.UpdateMessageStatus(message.Id, OutboundMessageStatus.Failed, StuckErrorMessage, stackTrace: null, exceptionType: null, transaction);
                logger.LogWarning("Failed stuck message {MessageId} (no explicit policy)", message.Id);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
        await transaction.CommitAsync(stoppingToken);
    }

    public abstract DbContext GetContext(AsyncServiceScope scope);
}
