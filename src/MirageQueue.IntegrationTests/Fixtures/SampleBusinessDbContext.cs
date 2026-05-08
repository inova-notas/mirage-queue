using Microsoft.EntityFrameworkCore;

namespace MirageQueue.IntegrationTests.Fixtures;

public class SampleBusinessDbContext(DbContextOptions<SampleBusinessDbContext> options) : DbContext(options)
{
    public const string SchemaName = "business_test";

    public DbSet<SampleBusinessEntity> SampleEntities => Set<SampleBusinessEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.Entity<SampleBusinessEntity>(b =>
        {
            b.ToTable("SampleEntity", SchemaName);
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        });
    }
}
