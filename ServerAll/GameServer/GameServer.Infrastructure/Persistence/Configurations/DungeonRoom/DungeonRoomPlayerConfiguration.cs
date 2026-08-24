using GameServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.DungeonRoom;

public class DungeonRoomPlayerConfiguration : IEntityTypeConfiguration<DungeonRoomPlayer>
{
    public void Configure(EntityTypeBuilder<DungeonRoomPlayer> builder)
    {
        builder.HasKey(x => new { x.RoomId, x.UserId });

        builder.Property(x => x.RoomId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.JoinedAt)
            .IsRequired();

        // UNIQUE — "한 유저는 한 방만"을 DB가 강제한다.
        // 서비스의 사전 검사는 경합에서 뚫리므로(check-then-act) 제약이 최종 방어선이다.
        builder.HasIndex(x => x.UserId).IsUnique();

        builder.ToTable("dungeon_room_players");
    }
}