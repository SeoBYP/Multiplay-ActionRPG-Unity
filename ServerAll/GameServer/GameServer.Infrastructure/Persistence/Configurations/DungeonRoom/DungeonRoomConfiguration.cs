using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
        builder.Property(dr => dr.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dr => dr.CreatedAt)
            .IsRequired();
        
        builder.ToTable("dungeon_rooms");
    }
}