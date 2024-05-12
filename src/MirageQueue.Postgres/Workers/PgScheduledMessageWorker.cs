using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MirageQueue.Postgres.Databases;
using MirageQueue.Workers;

namespace MirageQueue.Postgres.Workers;

public class PgScheduledMessageWorker(
    IServiceProvider serviceProvider,
    ILogger<PgScheduledMessageWorker> logger,
    MirageQueueConfiguration configuration)
    : ScheduledMessageHandlerWorker(serviceProvider, logger, configuration)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public override DbContext GetContext(AsyncServiceScope scope)
    {
        return scope.ServiceProvider.GetRequiredService<MirageQueueDbContext>();
    }
}