using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Infrastructure.Spawn;

namespace GameServer.Infrastructure.Persistence.Configurations.DungeonRoom;
using DungeonRoom = Domain.Entities.DungeonRoom;

public class DungeonRoomConfiguration : IEntityTypeConfiguration<DungeonRoom>
{
    public void Configure(EntityTypeBuilder<DungeonRoom> builder)
    {
        builder.HasKey(dr => dr.RoomId);
        
        builder.Property(dr => dr.RoomId)
            .UseIdentityAlwaysColumn()
            .IsRequired();

        builder.Property(dr => dr.RoomName)
            .IsRequired();
        builder.Property(dr => dr.HostUserId)
            .IsRequired();
        builder.Property(dr => dr.MaxPlayers)
            .IsRequired();
        // 던전 식별자(spawn-layouts.json 키). 기존 행은 마이그레이션 시 기본 맵으로 백필.
        builder.Property(dr => dr.MapId)
            .IsRequired()
            .HasMaxLength(64)
            .HasDefaultValue(MapIds.Default);
        builder.Property(dr => dr.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dr => dr.CreatedAt)
            .IsRequired();
        
        builder.ToTable("dungeon_rooms");
    }
}