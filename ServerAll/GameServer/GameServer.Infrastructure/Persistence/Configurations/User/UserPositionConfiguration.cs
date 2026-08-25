using GameServer.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.User;

public class UserPositionConfiguration : IEntityTypeConfiguration<UserPosition>
{
    public void Configure(EntityTypeBuilder<UserPosition> builder)
    {
        // UserId 단일키 — 유저당 마지막 위치 1건(Progression·Wallet 과 동일 형태).
        builder.HasKey(p => p.UserId);

        builder.Property(p => p.UserId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(p => p.MapId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(p => p.X).IsRequired();
        builder.Property(p => p.Y).IsRequired();
        builder.Property(p => p.Z).IsRequired();
        builder.Property(p => p.RotY).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.ToTable("user_positions");
    }
}
