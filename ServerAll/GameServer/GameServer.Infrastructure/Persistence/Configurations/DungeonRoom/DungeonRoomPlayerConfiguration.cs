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

        builder.HasIndex(x => x.UserId);

        builder.ToTable("dungeon_room_players");
    }
}