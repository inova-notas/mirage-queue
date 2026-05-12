using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers;
using MirageQueue.Diagnostics;
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

        if (stuck.Count == 0)
        {
            await transaction.CommitAsync(stoppingToken);
            return;
        }

        // Gated span — only opens when there's real work, so quiet polling cycles stay silent.
        using var activity = MirageQueueDiagnostics.ActivitySource.StartActivity(
            MirageQueueDiagnostics.OperationReaper, ActivityKind.Internal);
        activity?.SetTag("mirage_queue.reaper.batch_size", stuck.Count);

        var retried = 0;
        var deadLettered = 0;
        var failed = 0;

        foreach (var message in stuck)
        {
            var (policy, hasExplicitPolicy) = RetryPolicy.Resolve(message.ConsumerEndpoint);
            var newAttempts = message.AttemptCount + 1;

            if (newAttempts < policy.MaxAttempts)
            {
                var nextRetryAt = DateTime.UtcNow + policy.Backoff.ComputeDelay(newAttempts);
                await repository.MarkForRetry(message.Id, newAttempts, nextRetryAt, StuckErrorMessage, stackTrace: null, exceptionType: null, source: "Reaper", transaction);
                retried++;
                logger.LogWarning("Reclaimed stuck message {MessageId} for retry (attempt {Attempt}/{Max})", message.Id, newAttempts, policy.MaxAttempts);
            }
            else if (hasExplicitPolicy)
            {
                await repository.MarkDeadLettered(message.Id, newAttempts, StuckErrorMessage, stackTrace: null, exceptionType: null, source: "Reaper", transaction);
                deadLettered++;
                logger.LogWarning("Dead-lettered stuck message {MessageId} after {Attempts} attempts", message.Id, newAttempts);
            }
            else
            {
                await repository.UpdateMessageStatus(message.Id, OutboundMessageStatus.Failed, newAttempts, StuckErrorMessage, stackTrace: null, exceptionType: null, source: "Reaper", transaction);
                failed++;
                logger.LogWarning("Failed stuck message {MessageId} (no explicit policy)", message.Id);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
        await transaction.CommitAsync(stoppingToken);

        if (retried > 0) MirageQueueDiagnostics.ReaperRowsReset.Add(retried, new TagList { { "disposition", "retry" } });
        if (deadLettered > 0) MirageQueueDiagnostics.ReaperRowsReset.Add(deadLettered, new TagList { { "disposition", "dead_lettered" } });
        if (failed > 0) MirageQueueDiagnostics.ReaperRowsReset.Add(failed, new TagList { { "disposition", "failed" } });
    }

    public abstract DbContext GetContext(AsyncServiceScope scope);
}
