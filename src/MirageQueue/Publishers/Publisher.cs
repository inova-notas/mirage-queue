using MassTransit;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using MirageQueue.Publishers.Abstractions;
using System.Text.Json;

namespace MirageQueue.Publishers;

public class Publisher : IPublisher
{
    readonly IInboundMessageRepository _inboundMessageRepository;
    readonly IScheduledMessageRepository _scheduledMessageRepository;

    public Publisher(IInboundMessageRepository inboundMessageRepository,
        IScheduledMessageRepository scheduledMessageRepository)
    {
        _inboundMessageRepository = inboundMessageRepository;
        _scheduledMessageRepository = scheduledMessageRepository;
    }

    public async Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);

        var inboundMessage = new InboundMessage()
        {
            Id = NewId.NextSequentialGuid(),
            MessageContract = typeof(TMessage).FullName ?? typeof(TMessage).Name,
            Content = JsonSerializer.Serialize(message),
            Status = InboundMessageStatus.New,
            CreateAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow
        };

        await _inboundMessageRepository.InsertAsync(inboundMessage);
        await _inboundMessageRepository.SaveChanges();
    }
    
    public async Task Schedule<TMessage>(TMessage message, DateTime scheduledTime, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);

        var scheduleMessage = new ScheduledInboundMessage()
        {
            Id = NewId.NextSequentialGuid(),
            MessageContract = typeof(TMessage).FullName ?? typeof(TMessage).Name,
            Content = JsonSerializer.Serialize(message),
            Status = ScheduledInboundMessageStatus.WaitingScheduledTime,
            ExecuteAt = scheduledTime,
            CreateAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow
        };

        await _scheduledMessageRepository.InsertAsync(scheduleMessage);
        await _scheduledMessageRepository.SaveChanges();
    }
}