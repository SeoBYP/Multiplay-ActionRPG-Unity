using GameServer.Domain.Entities.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.Wallet;

public class UserWalletConfiguration : IEntityTypeConfiguration<UserWallet>
{
    public void Configure(EntityTypeBuilder<UserWallet> builder)
    {
        // UserId 단일키 — 유저당 잔액 1행. UserId 는 users FK(identity 아님).
        builder.HasKey(w => w.UserId);

        builder.Property(w => w.UserId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(w => w.Balance)
            .IsRequired();

        builder.Property(w => w.UpdatedAt)
            .IsRequired();

        builder.ToTable("user_wallets");
    }
}
