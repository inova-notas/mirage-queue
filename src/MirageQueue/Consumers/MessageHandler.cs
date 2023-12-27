using MassTransit;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;

namespace MirageQueue.Consumers;

public class MessageHandler : IMessageHandler
{
    private readonly ILogger<MessageHandler> _logger;
    private readonly IOutboundMessageRepository _outboundMessageRepository;
    private readonly IInboundMessageRepository _inboundMessageRepository;

    private readonly Dispatcher _dispatcher;

    public MessageHandler(
        ILogger<MessageHandler> logger,
        IOutboundMessageRepository outboundMessageRepository,
        IInboundMessageRepository inboundMessageRepository,
        Dispatcher dispatcher)
    {
        _logger = logger;
        _outboundMessageRepository = outboundMessageRepository;
        _inboundMessageRepository = inboundMessageRepository;
        _dispatcher = dispatcher;
    }

    private async Task<List<InboundMessage>> GetInboundMessages(IDbContextTransaction dbTransaction)
    {
        return await _inboundMessageRepository.GetQueuedMessages(10, dbTransaction);
    }

    private async Task<List<OutboundMessage>> GetOutboundMessage(IDbContextTransaction dbTransaction)
    {
        return await _outboundMessageRepository.GetQueuedMessages(10, dbTransaction);
    }

    public async Task HandleQueuedOutboundMessages(IDbContextTransaction dbTransaction)
    {
        var messages = await GetOutboundMessage(dbTransaction);
        await _outboundMessageRepository.SetTransaction(dbTransaction);

        foreach (var message in messages)
        {
            try
            {
                await _dispatcher.ProcessOutboundMessage(message);
                message.ChangeStatus(OutboundMessageStatus.Processing);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while processing outbound message {MessageId}", message.Id);
                message.ChangeStatus(OutboundMessageStatus.Failed);
            }
        }
    }

    public async Task HandleQueuedInboundMessages(IDbContextTransaction dbTransaction)
    {
        var inboundMessages = await GetInboundMessages(dbTransaction);

        try
        {
            foreach (var inboundMessage in inboundMessages)
            {
                await CovertInboundToOutboundMessage(inboundMessage);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while processing inbound messages");
        }
    }

    private async Task CovertInboundToOutboundMessage(InboundMessage inboundMessage)
    {
        await CreateOutboundMessages(inboundMessage);
        inboundMessage.Status = InboundMessageStatus.Queued;
        inboundMessage.UpdateAt = DateTime.UtcNow;
        await _inboundMessageRepository.Update(inboundMessage);
        await _inboundMessageRepository.SaveChanges();
    }

    private async Task CreateOutboundMessages(InboundMessage inboundMessage)
    {
        var consumers = _dispatcher.Consumers.Where(x => x.MessageContract == inboundMessage.MessageContract);

        foreach (var consumer in consumers)
        {
            if (await _outboundMessageRepository.Any(x => x.ConsumerEndpoint == consumer.ConsumerEndpoint
                                                                && x.InboundMessageId == inboundMessage.Id)) continue;
            var outboundMessage = new OutboundMessage
            {
                Id = NewId.NextSequentialGuid(),
                ConsumerEndpoint = consumer.ConsumerEndpoint,
                MessageContract = consumer.MessageContract,
                Content = inboundMessage.Content,
                CreateAt = DateTime.UtcNow,
                Status = OutboundMessageStatus.New,
                InboundMessageId = inboundMessage.Id
            };

            await _outboundMessageRepository.InsertAsync(outboundMessage);
        }

        inboundMessage.Status = InboundMessageStatus.Queued;
        inboundMessage.UpdateAt = DateTime.UtcNow;
        await _inboundMessageRepository.Update(inboundMessage);
    }
}