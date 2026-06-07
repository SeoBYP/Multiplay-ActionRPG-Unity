using GameServer.Domain.Entities;
using GameServer.Domain.Entities.Chat;
using GameServer.Domain.Entities.GameSession;
using GameServer.Domain.Entities.Inventory;
using GameServer.Domain.Entities.Outbox;
using GameServer.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Persistence;

public class GameServerDbContext(DbContextOptions<GameServerDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<UserCredential> UserCredentials { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<UserProgression> UserProgressions { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }

    public DbSet<InventoryItem> InventoryItems { get; set; }
    
    public DbSet<DungeonRoom> DungeonRooms { get; set; }
    public DbSet<DungeonRoomPlayer> DungeonRoomPlayers { get; set; }

    public DbSet<GameSession> GameSessions { get; set; }
    public DbSet<GameSessionPlayer> GameSessionPlayers { get; set; }
    
    public DbSet<ChatMessage> ChatMessages { get; set; }
    
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameServerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
