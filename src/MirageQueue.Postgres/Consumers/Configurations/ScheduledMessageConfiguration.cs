using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Postgres.Consumers.Configurations;

public class ScheduledMessageConfiguration : IEntityTypeConfiguration<ScheduledInboundMessage>
{
    public void Configure(EntityTypeBuilder<ScheduledInboundMessage> builder)
    {
        builder.ToTable(nameof(ScheduledInboundMessage), "mirage_queue");
        builder.Property(x => x.Content)
            .HasColumnType("jsonb");

        builder.Property(x => x.MessageContract)
            .HasMaxLength(200);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(200);

        builder.Property(x => x.TraceParent)
            .HasMaxLength(55)
            .IsRequired(false);

        builder.Property(x => x.TraceState)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        // Supports the retention cleanup predicate.
        builder.HasIndex(x => new { x.Status, x.UpdateAt });
    }
}