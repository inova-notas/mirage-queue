using Microsoft.EntityFrameworkCore;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Databases;

public class MirageQueueDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}