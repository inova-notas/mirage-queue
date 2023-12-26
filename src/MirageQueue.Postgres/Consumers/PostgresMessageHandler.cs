using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers;
using MirageQueue.Databases;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Postgres.Consumers;

public class PostgresMessageHandler : MessageHandler
{
    private readonly MirageQueueDbContext _dbContext;

    public PostgresMessageHandler(ILogger<MessageHandler> logger, MirageQueueDbContext dbContext, Dispatcher dispatcher) : base(logger, dbContext, dispatcher)
    {
        _dbContext = dbContext;
    }

    protected override Task<List<InboundMessage>> GetInboundMessages()
    {
        return _dbContext.Set<InboundMessage>()
            .FromSql(
                $"SELECT * FROM {nameof(InboundMessage)} WHERE {nameof(InboundMessage.Status)} = {InboundMessageStatus.New} FOR UPDATE SKIP LOCKED LIMIT 10")
            .ToListAsync();
    }

    protected override Task<List<OutboundMessage>> GetOutboundMessage()
    {
        return _dbContext.Set<OutboundMessage>()
            .FromSql(
                $"SELECT * FROM {nameof(OutboundMessage)} WHERE {nameof(OutboundMessage.Status)} = {OutboundMessageStatus.New} FOR UPDATE SKIP LOCKED LIMIT 10")
            .ToListAsync();
    }
}