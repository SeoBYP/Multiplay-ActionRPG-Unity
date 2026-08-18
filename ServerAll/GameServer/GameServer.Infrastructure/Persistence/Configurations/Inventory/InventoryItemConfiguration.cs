using GameServer.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.Inventory;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        // (UserId, ItemId) 복합키 — 스택형 소유. UserId 는 users FK(identity 아님).
        builder.HasKey(i => new { i.UserId, i.ItemId });

        builder.Property(i => i.UserId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(i => i.ItemId)
            .IsRequired();

        builder.Property(i => i.Quantity)
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .IsRequired();

        builder.ToTable("inventory_items");
    }
}
