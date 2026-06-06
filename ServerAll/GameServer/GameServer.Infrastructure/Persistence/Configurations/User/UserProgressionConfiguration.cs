using GameServer.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.User;

public class UserProgressionConfiguration : IEntityTypeConfiguration<UserProgression>
{
    public void Configure(EntityTypeBuilder<UserProgression> builder)
    {
        builder.HasKey(p => p.UserId);

        // user_id 는 users 의 PK 를 그대로 쓰는 FK — identity 아님(서비스가 직접 지정).
        builder.Property(p => p.UserId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(p => p.Level)
            .IsRequired();

        builder.Property(p => p.Exp)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        builder.ToTable("user_progressions");
    }
}
