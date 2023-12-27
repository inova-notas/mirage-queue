using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Consumers;
using MirageQueue.Messages.Repositories;
using MirageQueue.Postgres.Databases;
using MirageQueue.Postgres.Workers;
using MirageQueue.Publishers;
using MirageQueue.Publishers.Abstractions;

namespace MirageQueue.Postgres;

public static class MirageQueuePostgresExtensions
{
    public static void AddMirageQueuePostgres(this IServiceCollection services, Action<DbContextOptionsBuilder> options)
    {
        services.AddDbContext<MirageQueueDbContext>(options);
        services.AddHostedService<PgOutMessageWorker>();
        services.AddHostedService<PgInMessageWorker>();
        services.AddScoped<IInboundMessageRepository, InboundMessageRepository>();
        services.AddScoped<IOutboundMessageRepository, OutboundMessageRepository>();
        
    }
}