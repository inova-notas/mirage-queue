using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Consumers;

public abstract class MessageHandler : IMessageHandler
{
    private readonly ILogger<MessageHandler> _logger;
    private readonly DbContext _dbContext;
    private readonly Dispatcher _dispatcher;

    protected MessageHandler(
        ILogger<MessageHandler> logger,
        DbContext dbContext, 
        Dispatcher dispatcher)
    {
        _logger = logger;
        _dbContext = dbContext;
        _dispatcher = dispatcher;
    }

    protected abstract Task<List<InboundMessage>> GetInboundMessages();
    
    protected abstract Task<List<OutboundMessage>> GetOutboundMessage();

    public async Task HandleQueuedOutboundMessages()
    {
        var dbTransaction = await _dbContext.Database.BeginTransactionAsync();
        var messages = await GetOutboundMessage();

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

            await _dbContext.SaveChangesAsync();
        }

        await dbTransaction.CommitAsync();
    }

    public async Task HandleQueuedInboundMessages()
    {
        var dbTransaction = await _dbContext.Database.BeginTransactionAsync();
        var inboundMessages = await GetInboundMessages();

        try
        {
            foreach (var inboundMessage in inboundMessages)
            {
                await CovertInboundToOutboundMessage(inboundMessage);
            }
            
            await dbTransaction.CommitAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while processing inbound messages");
            await dbTransaction.RollbackAsync();
        }
    }
    
    private async Task CovertInboundToOutboundMessage(InboundMessage inboundMessage)
    {
        await CreateOutboundMessages(inboundMessage);
        inboundMessage.Status = InboundMessageStatus.Queued;
        inboundMessage.UpdateAt = DateTime.UtcNow;
        _dbContext.Set<InboundMessage>().Update(inboundMessage);
        await _dbContext.SaveChangesAsync();
    }
    
    private async Task CreateOutboundMessages(InboundMessage inboundMessage)
    {
        var consumers = _dispatcher.Consumers.Where(x => x.MessageContract == inboundMessage.MessageContract);
        
        foreach (var consumer in consumers)
        {
            if (await _dbContext.Set<OutboundMessage>().AnyAsync(x => x.ConsumerEndpoint == consumer.ConsumerEndpoint
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

            _dbContext.Set<OutboundMessage>().Add(outboundMessage);
        }
        
        inboundMessage.Status = InboundMessageStatus.Queued;
        inboundMessage.UpdateAt = DateTime.UtcNow;
        _dbContext.Set<InboundMessage>().Update(inboundMessage);
        await _dbContext.SaveChangesAsync();
    }
}