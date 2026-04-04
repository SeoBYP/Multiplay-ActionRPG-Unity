using GameServer.Domain.Entities.GameSession;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.GameSession;

public class GameSessionPlayerConfiguration : IEntityTypeConfiguration<GameSessionPlayer>
{
    public void Configure(EntityTypeBuilder<GameSessionPlayer> builder)
    {
        builder.HasKey(gsp => new {gsp.GameSessionId, gsp.UserId});

        builder.Property(gsp => gsp.GameSessionId)
            .IsRequired();
        builder.Property(gsp => gsp.UserId)
            .IsRequired();
        builder.Property(gsp => gsp.JoinedAt)
            .IsRequired();
        
        builder.HasIndex(gsp => gsp.UserId);
        
        builder.ToTable("game_session_players");
    }
}