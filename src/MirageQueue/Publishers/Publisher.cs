using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using MirageQueue.Messages.Entities;
using MirageQueue.Publishers.Abstractions;

namespace MirageQueue.Publishers;

public class Publisher : IPublisher
{
    private readonly DbContext _dbContext;

    public Publisher(DbContext dbContext) 
    {
        _dbContext = dbContext;
    }
    
    public async Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        var inboundMessage = new InboundMessage()
        {
            Id = NewId.NextSequentialGuid(),
            MessageContract = typeof(TMessage).FullName,
            Content = JsonSerializer.Serialize(message),
            Status = InboundMessageStatus.New,
            CreateAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow
        };
        
        await _dbContext.Set<InboundMessage>().AddAsync(inboundMessage, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}