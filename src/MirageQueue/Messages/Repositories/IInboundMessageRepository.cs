﻿using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Messages.Repositories;

public interface IInboundMessageRepository : IRepository<InboundMessage>
{
    public Task<List<InboundMessage>> GetQueuedMessages(IDbContextTransaction? transaction = null, int limit = 10);
    public Task UpdateMessageStatus(Guid id, InboundMessageStatus status, IDbContextTransaction? transaction = null);
}