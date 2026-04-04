using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.GameSession;
using GameSession = Domain.Entities.GameSession.GameSession;

public class GameSessionConfiguration : IEntityTypeConfiguration<GameSession>
{
    public void Configure(EntityTypeBuilder<GameSession> builder)
    {
        builder.HasKey(gs => gs.GameSessionId);
        
        builder.Property(gs => gs.GameSessionId)
            .UseIdentityAlwaysColumn()
            .IsRequired();
        
        builder.Property(gs => gs.RoomId)
            .IsRequired();
        builder.Property(gs => gs.SocketIp)
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(gs => gs.SocketPort)
            .IsRequired();
        builder.Property(gs => gs.StartedAt)
            .IsRequired();
        builder.Property(gs => gs.EndedAt);
        builder.Property(gs => gs.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(gs => gs.RoomId);
        
        builder.ToTable("game_sessions");
    }
}