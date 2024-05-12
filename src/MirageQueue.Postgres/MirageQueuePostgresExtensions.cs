using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using MirageQueue.Postgres.Databases;
using MirageQueue.Postgres.Workers;

namespace MirageQueue.Postgres;

public static class MirageQueuePostgresExtensions
{
    public static void AddMirageQueuePostgres(this IServiceCollection services, Action<DbContextOptionsBuilder> options)
    {
        services.AddDbContext<MirageQueueDbContext>(options);
        services.AddHostedService<PgOutMessageWorker>();
        services.AddHostedService<PgInMessageWorker>();
        services.AddHostedService<PgScheduledMessageWorker>();
        services.AddScoped<IInboundMessageRepository, InboundMessageRepository>();
        services.AddScoped<IOutboundMessageRepository, OutboundMessageRepository>();
        services.AddScoped<IScheduledMessageRepository, ScheduledMessageRepository>();

    }
}