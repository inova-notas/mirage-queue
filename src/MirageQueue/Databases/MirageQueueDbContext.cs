using Microsoft.EntityFrameworkCore;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Databases;

internal class MirageQueueDbContext : DbContext
{
    public DbSet<OutboundMessage> OutboundMessages { get; set; }
}