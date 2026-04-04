using GameServer.Domain.Entities.Chat;
using GameServer.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.Chat;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.HasKey(cm => cm.MessageId);
        
        builder.Property(cm => cm.MessageId)
            .UseIdentityAlwaysColumn()
            .IsRequired();

        builder.Property(cm => cm.ChatType)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(cm => cm.SenderUserNickName)
            .HasMaxLength(UserProfile.MaxNickNameLength)
            .IsRequired();
        builder.Property(cm => cm.Message)
            .HasMaxLength(ChatMessage.MaxMessageLength)
            .IsRequired();
        builder.Property(cm => cm.SentAt)
            .IsRequired();

        builder.Property(cm => cm.RoomId)
            .IsRequired(false);

        builder.Property(cm => cm.TargetUserNickName)
            .HasMaxLength(UserProfile.MaxNickNameLength)
            .IsRequired(false);
        
        builder.HasIndex(cm => cm.SentAt);
        builder.HasIndex(cm => cm.RoomId);
        builder.HasIndex(cm => cm.TargetUserNickName);
        
        builder.ToTable("chat_messages");
    }
}