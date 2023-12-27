using Microsoft.EntityFrameworkCore;

namespace MirageQueue.Postgres.Databases;

public class MirageQueueDbContext(DbContextOptions<MirageQueueDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}