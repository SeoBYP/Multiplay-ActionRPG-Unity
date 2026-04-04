using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.User;
using User = Domain.Entities.User.User;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // 기본 키(PK) 지정
        builder.HasKey(u => u.UserId);
        
        // UserId 컬럼을 PostgreSQL의 identity column(자동 증가 컬럼)
        builder.Property(u => u.UserId)
            .UseIdentityAlwaysColumn()
            .IsRequired();
        
        builder.Property(u => u.PublicId)
            .HasMaxLength(User.MaxPublicIdLength)
            .IsRequired();
        
        builder.Property(u => u.CreatedAt);
        
        // 인덱스
        builder.HasIndex(u => u.PublicId).IsUnique();
        
        // 테이블명
        builder.ToTable("users");
    }
}