using GameServer.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // // 기본 키(PK) 지정
        // builder.HasKey(u => u.UserId);
        // // UserId 컬럼을 PostgreSQL의 identity column(자동 증가 컬럼)
        // builder.Property(u => u.UserId)
        //     .UseIdentityAlwaysColumn();
        //
        // builder.Property(u => u.Email) // 특정 속성 설정 시작
        //     .HasMaxLength(255) // 문자열 길이 제한
        //     .IsRequired(); //필수값, 즉 nullable 아님
        // builder.Property(u => u.NickName).HasMaxLength(50).IsRequired();
        // builder.Property(u => u.PublicId).HasMaxLength(20).IsRequired();
        // builder.Property(u => u.PasswordHash).IsRequired();
        // builder.Property(u => u.RefreshToken).HasMaxLength(500);
        //
        // // 인덱스
        // builder.HasIndex(u => u.Email).IsUnique();
        // builder.HasIndex(u => u.NickName).IsUnique();
        // builder.HasIndex(u => u.PublicId).IsUnique();
        //
        //
        // // 테이블명
        // builder.ToTable("users");
    }
}