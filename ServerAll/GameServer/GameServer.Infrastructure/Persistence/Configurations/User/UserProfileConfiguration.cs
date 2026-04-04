using GameServer.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.User;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasKey(up => up.UserId);
        
        builder.Property(up => up.UserId)
            .IsRequired();
        
        builder.Property(up => up.NickName)
            .HasMaxLength(UserProfile.MaxNickNameLength)
            .IsRequired();
        
        builder.ToTable("user_profiles");
    }
}