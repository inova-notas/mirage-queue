﻿using System.Threading.Channels;
using MassTransit;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;

namespace MirageQueue.Consumers;

public class MessageHandler(
    ILogger<MessageHandler> logger,
    IOutboundMessageRepository outboundMessageRepository,
    IInboundMessageRepository inboundMessageRepository,
    IScheduledMessageRepository scheduledMessageRepository,
    Dispatcher dispatcher,
    MirageQueueConfiguration configuration,
    Channel<OutboundMessage> outboundMessageChannel)
    : IMessageHandler
{
    private async Task<List<InboundMessage>> GetInboundMessages(IDbContextTransaction dbTransaction)
    {
        return await inboundMessageRepository.GetQueuedMessages(dbTransaction, configuration.WorkersQuantity);
    }

    private async Task<List<OutboundMessage>> GetOutboundMessage(IDbContextTransaction dbTransaction)
    {
        return await outboundMessageRepository.GetQueuedMessages(dbTransaction, configuration.WorkersQuantity);
    }
    
    private async Task<List<ScheduledInboundMessage>> GetScheduledMessages(IDbContextTransaction dbTransaction)
    {
        return await scheduledMessageRepository.GetScheduledMessages(dbTransaction, configuration.WorkersQuantity);
    }

    public async Task<List<OutboundMessage>> HandleQueuedOutboundMessages(IDbContextTransaction dbTransaction)
    {
        var messages = await GetOutboundMessage(dbTransaction);
        await outboundMessageRepository.SetTransaction(dbTransaction);

        foreach (var message in messages)
        {
            await outboundMessageRepository.UpdateMessageStatus(message.Id, OutboundMessageStatus.Processing, dbTransaction);
        }

        return messages;
    }
    
    public async Task AddOutboundMessageToChannel(OutboundMessage message)
    {
        try
        {
            await outboundMessageChannel.Writer.WriteAsync(message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while adding outbound message to channel {MessageId}", message.Id);
            throw;
        }
    }

    public async Task<(Guid MessageId, OutboundMessageStatus Status, Exception? Exception)> ProcessOutboundMessage(OutboundMessage message)
    {
        try
        {
            await dispatcher.ProcessOutboundMessage(message);
            return (message.Id, OutboundMessageStatus.Processed, null);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while processing outbound message {MessageId}", message.Id);
            return (message.Id, OutboundMessageStatus.Failed, e);
        }
    }

    public async Task HandleQueuedInboundMessages(IDbContextTransaction dbTransaction)
    {
        var inboundMessages = await GetInboundMessages(dbTransaction);

        try
        {
            foreach (var inboundMessage in inboundMessages)
            {
                await CovertInboundToOutboundMessage(inboundMessage, dbTransaction);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while processing inbound messages");
        }
    }
    
    public async Task HandleScheduledMessages(IDbContextTransaction dbTransaction)
    {
        var scheduledMessages = await GetScheduledMessages(dbTransaction);
        try
        {
            foreach (var message in scheduledMessages)
            {
                await ConvertScheduledToInboundMessage(message, dbTransaction);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while processing scheduled messages");
        }
    }
    
    private async Task ConvertScheduledToInboundMessage(ScheduledInboundMessage scheduledMessage, IDbContextTransaction dbTransaction)
    {
        try
        {
            var inboundMessage = new InboundMessage
            {
                Id = NewId.NextSequentialGuid(),
                MessageContract = scheduledMessage.MessageContract,
                Content = scheduledMessage.Content,
                Status = InboundMessageStatus.New,
                CreateAt = DateTime.UtcNow,
                UpdateAt = DateTime.UtcNow,
            };

            await scheduledMessageRepository.UpdateMessageStatus(scheduledMessage.Id, ScheduledInboundMessageStatus.Queued,
                dbTransaction);
            
            await inboundMessageRepository.InsertAsync(inboundMessage);
            await inboundMessageRepository.SaveChanges();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while converting scheduled message to inbound message {Message}", scheduledMessage);
            throw;
        }
    }

    private async Task CovertInboundToOutboundMessage(InboundMessage inboundMessage, IDbContextTransaction dbTransaction)
    {
        await CreateOutboundMessages(inboundMessage);
        await inboundMessageRepository.UpdateMessageStatus(inboundMessage.Id, InboundMessageStatus.Queued,
            dbTransaction);
    }

    private async Task CreateOutboundMessages(InboundMessage inboundMessage)
    {
        var consumers = dispatcher.Consumers.Where(x => x.MessageContract == inboundMessage.MessageContract);

        foreach (var consumer in consumers)
        {
            if (await outboundMessageRepository.Any(x => x.ConsumerEndpoint == consumer.ConsumerEndpoint
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

            await outboundMessageRepository.InsertAsync(outboundMessage);
        }
    }
}