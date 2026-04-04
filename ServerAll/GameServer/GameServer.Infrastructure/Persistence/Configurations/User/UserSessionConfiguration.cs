using GameServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.User;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.HasKey(us => us.SessionId);
        
        builder.Property(us => us.SessionId)
            .HasMaxLength(64)
            .IsRequired();
        
        builder.Property(us => us.UserId)
            .IsRequired();
        builder.Property(us => us.LoginAt)
            .IsRequired();
        builder.Property(us => us.LastActiveAt)
            .IsRequired();
        
        // User 별 1개의 세션
        builder.HasIndex(us => us.UserId)
            .IsUnique();
        
        builder.ToTable("user_sessions");
    }
}