using GameServer.Domain.Entities.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.Outbox;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(om => om.MessageId);

        builder.Property(om => om.MessageId)
            .UseIdentityAlwaysColumn();
        
        builder.Property(om => om.Topic)
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(om => om.Payload)
            .IsRequired();
        builder.Property(om => om.CreatedAt)
            .IsRequired();
        builder.Property(om => om.ProcessedAt)
            .IsRequired(false);
        
        builder.HasIndex(om => om.ProcessedAt);
        
        builder.ToTable("outbox_messages");
    }
}