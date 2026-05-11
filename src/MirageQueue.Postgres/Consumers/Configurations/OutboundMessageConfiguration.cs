using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Postgres.Consumers.Configurations;

public class OutboundMessageConfiguration : IEntityTypeConfiguration<OutboundMessage>
{
    public void Configure(EntityTypeBuilder<OutboundMessage> builder)
    {
        builder.ToTable(nameof(OutboundMessage), "mirage_queue");
        builder.Property(x => x.Content)
            .HasColumnType("jsonb");

        builder.Property(x => x.MessageContract)
            .HasMaxLength(200);

        builder.Property(x => x.ConsumerEndpoint)
            .HasMaxLength(300);

        builder.Property(x => x.ErrorMessage)
            .IsRequired(false);

        builder.Property(x => x.StackTrace)
            .IsRequired(false);

        builder.Property(x => x.ExceptionType)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(x => x.AttemptCount)
            .HasDefaultValue(0);

        builder.Property(x => x.NextRetryAt)
            .IsRequired(false);

        builder.Property(x => x.ProcessingStartedAt)
            .IsRequired(false);

        builder.HasIndex(x => new { x.InboundMessageId, x.ConsumerEndpoint })
            .IsUnique();

        builder.HasIndex(x => new { x.Status, x.NextRetryAt });

        builder.HasIndex(x => new { x.Status, x.ProcessingStartedAt });
    }
}