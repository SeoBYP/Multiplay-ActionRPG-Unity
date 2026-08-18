using GameServer.Domain.Entities.Codex;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.Codex;

public class UserCodexEntryConfiguration : IEntityTypeConfiguration<UserCodexEntry>
{
    public void Configure(EntityTypeBuilder<UserCodexEntry> builder)
    {
        // (UserId, ItemId) 복합키 — 발견 기록 append-only. UserId 는 users FK(identity 아님).
        builder.HasKey(e => new { e.UserId, e.ItemId });

        builder.Property(e => e.UserId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(e => e.ItemId)
            .IsRequired();

        builder.Property(e => e.DiscoveredAt)
            .IsRequired();

        builder.ToTable("user_codex");
    }
}
