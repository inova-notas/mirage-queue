using MassTransit;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using MirageQueue.Publishers.Abstractions;
using System.Text.Json;

namespace MirageQueue.Publishers;

public class Publisher : IPublisher
{
    private readonly IInboundMessageRepository _inboundMessageRepository;

    public Publisher(IInboundMessageRepository inboundMessageRepository)
    {
        _inboundMessageRepository = inboundMessageRepository;
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

        await _inboundMessageRepository.InsertAsync(inboundMessage);
        await _inboundMessageRepository.SaveChanges();
    }
}