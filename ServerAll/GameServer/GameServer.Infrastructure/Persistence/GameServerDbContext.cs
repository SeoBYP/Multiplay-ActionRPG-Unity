using GameServer.Domain.Entities;
using GameServer.Domain.Entities.Chat;
using GameServer.Domain.Entities.Codex;
using GameServer.Domain.Entities.Equipment;
using GameServer.Domain.Entities.GameSession;
using GameServer.Domain.Entities.Inventory;
using GameServer.Domain.Entities.Outbox;
using GameServer.Domain.Entities.Quest;
using GameServer.Domain.Entities.Reward;
using GameServer.Domain.Entities.User;
using GameServer.Domain.Entities.Wallet;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Persistence;

public class GameServerDbContext(DbContextOptions<GameServerDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<UserCredential> UserCredentials { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<UserProgression> UserProgressions { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }

    /// <summary>Main 마지막 위치(B7). 주기 보고는 Redis, 이탈 시점에 여기로 확정된다.</summary>
    public DbSet<UserPosition> UserPositions { get; set; }

    public DbSet<InventoryItem> InventoryItems { get; set; }

    public DbSet<UserEquipment> UserEquipments { get; set; }

    public DbSet<UserWallet> UserWallets { get; set; }

    public DbSet<UserCodexEntry> UserCodexEntries { get; set; }

    public DbSet<UserQuest> UserQuests { get; set; }

    public DbSet<DungeonRoom> DungeonRooms { get; set; }
    public DbSet<DungeonRoomPlayer> DungeonRoomPlayers { get; set; }

    public DbSet<GameSession> GameSessions { get; set; }
    public DbSet<GameSessionPlayer> GameSessionPlayers { get; set; }
    
    /// <summary>보상 지급 원장 — 지급 멱등의 단일 진실(GrantKey UNIQUE).</summary>
    public DbSet<RewardGrant> RewardGrants { get; set; }

    public DbSet<ChatMessage> ChatMessages { get; set; }
    
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameServerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
