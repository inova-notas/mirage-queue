using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MirageQueue.Messages.Entities;
using MirageQueue.Postgres.Databases;
using MirageQueue.Workers;

namespace MirageQueue.Postgres.Workers;

public class PgOutMessageWorker(
    IServiceProvider serviceProvider,
    ILogger<PgOutMessageWorker> logger,
    Channel<OutboundMessage> channel,
    MirageQueueConfiguration configuration)
    : OutboundMessageHandlerWorker(serviceProvider, logger, channel, configuration)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public override DbContext GetContext(AsyncServiceScope scope)
    {
        return scope.ServiceProvider.GetRequiredService<MirageQueueDbContext>();
    }
}