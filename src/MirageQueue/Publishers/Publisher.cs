using System.Data.Common;
using System.Diagnostics;
using MassTransit;
using MirageQueue.Diagnostics;
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

        var contractName = ContractName<TMessage>();
        using var activity = StartProducerActivity(MirageQueueDiagnostics.OperationPublish, contractName);
        var startTs = Stopwatch.GetTimestamp();
        try
        {
            var inboundMessage = BuildInboundMessage(message);
            ApplyTraceContext(inboundMessage);

            await _inboundMessageRepository.InsertAsync(inboundMessage);
            await _inboundMessageRepository.SaveChanges();

            RecordPublishSuccess(activity, MirageQueueDiagnostics.OperationPublish, contractName, inboundMessage.Id, startTs);
        }
        catch (Exception ex)
        {
            FailActivity(activity, ex);
            throw;
        }
    }

    public async Task Publish<TMessage>(TMessage message, DbTransaction transaction, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(transaction);

        var contractName = ContractName<TMessage>();
        using var activity = StartProducerActivity(MirageQueueDiagnostics.OperationPublish, contractName);
        var startTs = Stopwatch.GetTimestamp();
        try
        {
            var inboundMessage = BuildInboundMessage(message);
            ApplyTraceContext(inboundMessage);

            await _inboundMessageRepository.InsertDirect(inboundMessage, transaction, cancellationToken);

            RecordPublishSuccess(activity, MirageQueueDiagnostics.OperationPublish, contractName, inboundMessage.Id, startTs);
        }
        catch (Exception ex)
        {
            FailActivity(activity, ex);
            throw;
        }
    }

    public async Task<PublishResult> Publish<TMessage>(TMessage message, string idempotencyKey, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);

        var contractName = ContractName<TMessage>();
        using var activity = StartProducerActivity(MirageQueueDiagnostics.OperationPublish, contractName);
        var startTs = Stopwatch.GetTimestamp();
        try
        {
            var inboundMessage = BuildInboundMessage(message, idempotencyKey);
            ApplyTraceContext(inboundMessage);

            var result = await _inboundMessageRepository.InsertIfNotExists(inboundMessage, cancellationToken);

            RecordPublishSuccess(activity, MirageQueueDiagnostics.OperationPublish, contractName, result.MessageId ?? inboundMessage.Id, startTs);
            return result;
        }
        catch (Exception ex)
        {
            FailActivity(activity, ex);
            throw;
        }
    }

    public async Task<PublishResult> Publish<TMessage>(TMessage message, string idempotencyKey, DbTransaction transaction, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);
        ArgumentNullException.ThrowIfNull(transaction);

        var contractName = ContractName<TMessage>();
        using var activity = StartProducerActivity(MirageQueueDiagnostics.OperationPublish, contractName);
        var startTs = Stopwatch.GetTimestamp();
        try
        {
            var inboundMessage = BuildInboundMessage(message, idempotencyKey);
            ApplyTraceContext(inboundMessage);

            var result = await _inboundMessageRepository.InsertDirectIfNotExists(inboundMessage, transaction, cancellationToken);

            RecordPublishSuccess(activity, MirageQueueDiagnostics.OperationPublish, contractName, result.MessageId ?? inboundMessage.Id, startTs);
            return result;
        }
        catch (Exception ex)
        {
            FailActivity(activity, ex);
            throw;
        }
    }

    public async Task Schedule<TMessage>(TMessage message, DateTime scheduledTime, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);

        var contractName = ContractName<TMessage>();
        using var activity = StartProducerActivity(MirageQueueDiagnostics.OperationSchedule, contractName);
        var startTs = Stopwatch.GetTimestamp();
        try
        {
            var scheduleMessage = BuildScheduledMessage(message, scheduledTime);
            ApplyTraceContext(scheduleMessage);

            await _scheduledMessageRepository.InsertAsync(scheduleMessage);
            await _scheduledMessageRepository.SaveChanges();

            RecordPublishSuccess(activity, MirageQueueDiagnostics.OperationSchedule, contractName, scheduleMessage.Id, startTs);
        }
        catch (Exception ex)
        {
            FailActivity(activity, ex);
            throw;
        }
    }

    public async Task Schedule<TMessage>(TMessage message, DateTime scheduledTime, DbTransaction transaction, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(transaction);

        var contractName = ContractName<TMessage>();
        using var activity = StartProducerActivity(MirageQueueDiagnostics.OperationSchedule, contractName);
        var startTs = Stopwatch.GetTimestamp();
        try
        {
            var scheduleMessage = BuildScheduledMessage(message, scheduledTime);
            ApplyTraceContext(scheduleMessage);

            await _scheduledMessageRepository.InsertDirect(scheduleMessage, transaction, cancellationToken);

            RecordPublishSuccess(activity, MirageQueueDiagnostics.OperationSchedule, contractName, scheduleMessage.Id, startTs);
        }
        catch (Exception ex)
        {
            FailActivity(activity, ex);
            throw;
        }
    }

    public async Task<PublishResult> Schedule<TMessage>(TMessage message, DateTime scheduledTime, string idempotencyKey, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);

        var contractName = ContractName<TMessage>();
        using var activity = StartProducerActivity(MirageQueueDiagnostics.OperationSchedule, contractName);
        var startTs = Stopwatch.GetTimestamp();
        try
        {
            var scheduleMessage = BuildScheduledMessage(message, scheduledTime, idempotencyKey);
            ApplyTraceContext(scheduleMessage);

            var result = await _scheduledMessageRepository.InsertIfNotExists(scheduleMessage, cancellationToken);

            RecordPublishSuccess(activity, MirageQueueDiagnostics.OperationSchedule, contractName, result.MessageId ?? scheduleMessage.Id, startTs);
            return result;
        }
        catch (Exception ex)
        {
            FailActivity(activity, ex);
            throw;
        }
    }

    public async Task<PublishResult> Schedule<TMessage>(TMessage message, DateTime scheduledTime, string idempotencyKey, DbTransaction transaction, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);
        ArgumentNullException.ThrowIfNull(transaction);

        var contractName = ContractName<TMessage>();
        using var activity = StartProducerActivity(MirageQueueDiagnostics.OperationSchedule, contractName);
        var startTs = Stopwatch.GetTimestamp();
        try
        {
            var scheduleMessage = BuildScheduledMessage(message, scheduledTime, idempotencyKey);
            ApplyTraceContext(scheduleMessage);

            var result = await _scheduledMessageRepository.InsertDirectIfNotExists(scheduleMessage, transaction, cancellationToken);

            RecordPublishSuccess(activity, MirageQueueDiagnostics.OperationSchedule, contractName, result.MessageId ?? scheduleMessage.Id, startTs);
            return result;
        }
        catch (Exception ex)
        {
            FailActivity(activity, ex);
            throw;
        }
    }

    private static string ContractName<TMessage>() =>
        typeof(TMessage).FullName ?? typeof(TMessage).Name;

    private static Activity? StartProducerActivity(string operation, string contractName)
    {
        var activity = MirageQueueDiagnostics.ActivitySource.StartActivity(
            $"{operation} {contractName}", ActivityKind.Producer);
        activity?.SetTag(MirageQueueDiagnostics.AttrMessagingSystem, MirageQueueDiagnostics.MessagingSystemValue);
        activity?.SetTag(MirageQueueDiagnostics.AttrMessagingOperation, operation);
        activity?.SetTag(MirageQueueDiagnostics.AttrMessagingDestination, contractName);
        return activity;
    }

    private static void ApplyTraceContext(BaseMessage message)
    {
        var current = Activity.Current;
        if (current is null || current.IdFormat != ActivityIdFormat.W3C)
            return;

        message.TraceParent = current.Id;
        message.TraceState = current.TraceStateString;
    }

    private static void RecordPublishSuccess(Activity? activity, string operation, string contractName, Guid messageId, long startTs)
    {
        activity?.SetTag(MirageQueueDiagnostics.AttrMessagingMessageId, messageId);

        var tags = new TagList
        {
            { MirageQueueDiagnostics.AttrMessagingSystem, MirageQueueDiagnostics.MessagingSystemValue },
            { MirageQueueDiagnostics.AttrMessagingOperation, operation },
            { MirageQueueDiagnostics.AttrMessagingDestination, contractName }
        };
        MirageQueueDiagnostics.PublishedCounter.Add(1, tags);
        MirageQueueDiagnostics.PublishDuration.Record(Stopwatch.GetElapsedTime(startTs).TotalSeconds, tags);
    }

    private static void FailActivity(Activity? activity, Exception ex)
    {
        if (activity is null) return;
        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.AddException(ex);
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
