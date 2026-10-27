using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.User;
using User = Domain.Entities.User.User;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.UserId);
        
        builder.Property(u => u.UserId)
            .UseIdentityAlwaysColumn()
            .IsRequired();
        
        builder.Property(u => u.PublicId)
            .HasMaxLength(User.MaxPublicIdLength)
            .IsRequired();
        
        builder.Property(u => u.CreatedAt);
        
        builder.HasIndex(u => u.PublicId).IsUnique();
        
        builder.ToTable("users");
    }
}