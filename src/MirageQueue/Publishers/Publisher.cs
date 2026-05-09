using System.Data.Common;
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

        var inboundMessage = BuildInboundMessage(message);

        await _inboundMessageRepository.InsertAsync(inboundMessage);
        await _inboundMessageRepository.SaveChanges();
    }

    public async Task Publish<TMessage>(TMessage message, DbTransaction transaction, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(transaction);

        var inboundMessage = BuildInboundMessage(message);

        await _inboundMessageRepository.InsertDirect(inboundMessage, transaction, cancellationToken);
    }

    public Task<PublishResult> Publish<TMessage>(TMessage message, string idempotencyKey, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);

        var inboundMessage = BuildInboundMessage(message, idempotencyKey);

        return _inboundMessageRepository.InsertIfNotExists(inboundMessage, cancellationToken);
    }

    public Task<PublishResult> Publish<TMessage>(TMessage message, string idempotencyKey, DbTransaction transaction, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);
        ArgumentNullException.ThrowIfNull(transaction);

        var inboundMessage = BuildInboundMessage(message, idempotencyKey);

        return _inboundMessageRepository.InsertDirectIfNotExists(inboundMessage, transaction, cancellationToken);
    }

    public async Task Schedule<TMessage>(TMessage message, DateTime scheduledTime, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);

        var scheduleMessage = BuildScheduledMessage(message, scheduledTime);

        await _scheduledMessageRepository.InsertAsync(scheduleMessage);
        await _scheduledMessageRepository.SaveChanges();
    }

    public async Task Schedule<TMessage>(TMessage message, DateTime scheduledTime, DbTransaction transaction, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(transaction);

        var scheduleMessage = BuildScheduledMessage(message, scheduledTime);

        await _scheduledMessageRepository.InsertDirect(scheduleMessage, transaction, cancellationToken);
    }

    public Task<PublishResult> Schedule<TMessage>(TMessage message, DateTime scheduledTime, string idempotencyKey, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);

        var scheduleMessage = BuildScheduledMessage(message, scheduledTime, idempotencyKey);

        return _scheduledMessageRepository.InsertIfNotExists(scheduleMessage, cancellationToken);
    }

    public Task<PublishResult> Schedule<TMessage>(TMessage message, DateTime scheduledTime, string idempotencyKey, DbTransaction transaction, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);
        ArgumentNullException.ThrowIfNull(transaction);

        var scheduleMessage = BuildScheduledMessage(message, scheduledTime, idempotencyKey);

        return _scheduledMessageRepository.InsertDirectIfNotExists(scheduleMessage, transaction, cancellationToken);
    }

    private static InboundMessage BuildInboundMessage<TMessage>(TMessage message, string? idempotencyKey = null) where TMessage : class
    {
        return new InboundMessage
        {
            Id = NewId.NextSequentialGuid(),
            MessageContract = typeof(TMessage).FullName ?? typeof(TMessage).Name,
            Content = JsonSerializer.Serialize(message),
            Status = InboundMessageStatus.New,
            CreateAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey
        };
    }

    private static ScheduledInboundMessage BuildScheduledMessage<TMessage>(TMessage message, DateTime scheduledTime, string? idempotencyKey = null) where TMessage : class
    {
        return new ScheduledInboundMessage
        {
            Id = NewId.NextSequentialGuid(),
            MessageContract = typeof(TMessage).FullName ?? typeof(TMessage).Name,
            Content = JsonSerializer.Serialize(message),
            Status = ScheduledInboundMessageStatus.WaitingScheduledTime,
            ExecuteAt = scheduledTime,
            CreateAt = DateTime.UtcNow,
            UpdateAt = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey
        };
    }
}