using Microsoft.EntityFrameworkCore;

namespace ExampleApi.Business;

public class BusinessDbContext(DbContextOptions<BusinessDbContext> options) : DbContext(options)
{
    public const string SchemaName = "business";

    public DbSet<BusinessEntity> BusinessEntities => Set<BusinessEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<BusinessEntity>(b =>
        {
            b.ToTable("BusinessEntity");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });
    }
}
