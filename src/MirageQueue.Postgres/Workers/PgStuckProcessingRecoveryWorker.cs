using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MirageQueue.Postgres.Databases;
using MirageQueue.Workers;

namespace MirageQueue.Postgres.Workers;

public class PgStuckProcessingRecoveryWorker(
    IServiceProvider serviceProvider,
    ILogger<PgStuckProcessingRecoveryWorker> logger,
    MirageQueueConfiguration configuration)
    : StuckProcessingRecoveryWorker(serviceProvider, logger, configuration)
{
    public override DbContext GetContext(AsyncServiceScope scope)
    {
        return scope.ServiceProvider.GetRequiredService<MirageQueueDbContext>();
    }
}
