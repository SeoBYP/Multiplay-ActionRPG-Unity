using GameServer.Domain.Entities.Quest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.Quest;

public class UserQuestConfiguration : IEntityTypeConfiguration<UserQuest>
{
    public void Configure(EntityTypeBuilder<UserQuest> builder)
    {
        // (UserId, QuestId) 복합키 — 수주/진행 1행. UserId 는 users FK(identity 아님).
        builder.HasKey(q => new { q.UserId, q.QuestId });

        builder.Property(q => q.UserId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(q => q.QuestId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(q => q.Status)
            .HasConversion<int>() // enum → int 영속
            .IsRequired();

        builder.Property(q => q.Progress)
            .IsRequired();

        builder.Property(q => q.UpdatedAt)
            .IsRequired();

        builder.ToTable("user_quests");
    }
}
