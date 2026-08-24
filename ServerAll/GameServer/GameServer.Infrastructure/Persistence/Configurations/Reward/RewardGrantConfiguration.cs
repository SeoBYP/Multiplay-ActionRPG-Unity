using GameServer.Domain.Entities.Reward;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.Reward;

public class RewardGrantConfiguration : IEntityTypeConfiguration<RewardGrant>
{
    public void Configure(EntityTypeBuilder<RewardGrant> builder)
    {
        builder.HasKey(g => g.GrantId);

        builder.Property(g => g.GrantId)
            .ValueGeneratedOnAdd();

        builder.Property(g => g.GrantKey)
            .HasMaxLength(128)
            .IsRequired();

        // 멱등의 진짜 방어선. 동시 중복 시도는 여기서 UNIQUE 위반으로 걸린다.
        builder.HasIndex(g => g.GrantKey)
            .IsUnique();

        builder.Property(g => g.UserId)
            .IsRequired();

        builder.Property(g => g.Kind)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(g => g.RefId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(g => g.Amount)
            .IsRequired();

        builder.Property(g => g.GrantedAt)
            .IsRequired();

        builder.ToTable("reward_grants");
    }
}
