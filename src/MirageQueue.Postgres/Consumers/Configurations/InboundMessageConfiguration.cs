using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MirageQueue.Messages.Entities;

namespace MirageQueue.Postgres.Consumers.Configurations;

public class InboundMessageConfiguration : IEntityTypeConfiguration<InboundMessage>
{
    public void Configure(EntityTypeBuilder<InboundMessage> builder)
    {
        builder.Property(x => x.Content)
            .HasColumnType("jsonb");

        builder.Property(x => x.MessageContract)
            .HasMaxLength(200);
    }
}