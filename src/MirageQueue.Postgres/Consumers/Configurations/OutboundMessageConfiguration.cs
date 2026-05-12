using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Postgres.Consumers.Configurations;

public class OutboundMessageConfiguration : IEntityTypeConfiguration<OutboundMessage>
{
    private static readonly JsonSerializerOptions ErrorHistoryJsonOptions = JsonSerializerOptions.Web;

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

        builder.Property(x => x.TraceParent)
            .HasMaxLength(55)
            .IsRequired(false);

        builder.Property(x => x.TraceState)
            .HasMaxLength(256)
            .IsRequired(false);

        // ErrorHistory is jsonb, append-only via raw SQL in the repository. The conversion
        // here only needs to support EF *reads* (the dashboard surfaces the typed list) —
        // writes never go through EF tracking, so the ValueComparer can be minimal.
        builder.Property(x => x.ErrorHistory)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null || v.Count == 0 ? null : JsonSerializer.Serialize(v, ErrorHistoryJsonOptions),
                v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<List<OutboundMessageError>>(v, ErrorHistoryJsonOptions),
                new ValueComparer<List<OutboundMessageError>?>(
                    (a, b) => ReferenceEquals(a, b),
                    v => v == null ? 0 : v.Count,
                    v => v))
            .IsRequired(false);

        builder.HasIndex(x => new { x.InboundMessageId, x.ConsumerEndpoint })
            .IsUnique();

        builder.HasIndex(x => new { x.Status, x.NextRetryAt });

        builder.HasIndex(x => new { x.Status, x.ProcessingStartedAt });

        // Supports the retention cleanup predicate.
        builder.HasIndex(x => new { x.Status, x.UpdateAt });
    }
}