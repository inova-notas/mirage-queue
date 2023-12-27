using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MirageQueue.Postgres.Databases;
using MirageQueue.Workers;

namespace MirageQueue.Postgres.Workers;

public class PgInMessageWorker(
    IServiceProvider serviceProvider,
    ILogger<InboundMessageHandlerWorker> logger,
    MirageQueueConfiguration configuration)
    : InboundMessageHandlerWorker(serviceProvider, logger, configuration)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public override DbContext GetContext(AsyncServiceScope scope)
    {
        return scope.ServiceProvider.GetRequiredService<MirageQueueDbContext>();
    }
}