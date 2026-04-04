using GameServer.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.User;

public class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.HasKey(uc => uc.UserId);

        builder.Property(uc => uc.Email)
            .HasMaxLength(320)
            .IsRequired();
        builder.Property(uc => uc.PasswordHash)
            .IsRequired();

        builder.Property(uc => uc.RefreshToken)
            .IsRequired(false);
        builder.Property(uc => uc.RefreshTokenExpiresAt)
            .IsRequired(false);

        // 중복 이메일 안됨
        builder.HasIndex(uc => uc.Email)
            .IsUnique();
        
        builder.ToTable("user_credentials");
    }
}