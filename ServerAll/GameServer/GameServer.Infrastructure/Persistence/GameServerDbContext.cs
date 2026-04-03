using GameServer.Domain.Entities;
using GameServer.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Persistence;

public class GameServerDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameServerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}