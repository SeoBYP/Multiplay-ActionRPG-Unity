using GameServer.Domain.Entities.Equipment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameServer.Infrastructure.Persistence.Configurations.Equipment;

public class UserEquipmentConfiguration : IEntityTypeConfiguration<UserEquipment>
{
    public void Configure(EntityTypeBuilder<UserEquipment> builder)
    {
        // (UserId, Slot) 복합키 — 슬롯당 하나(교체형). UserId 는 users FK(identity 아님).
        builder.HasKey(e => new { e.UserId, e.Slot });

        builder.Property(e => e.UserId)
            .ValueGeneratedNever()
            .IsRequired();

        // enum → int 컬럼(기본 매핑). 슬롯 추가는 enum 값 추가만(마이그레이션 불필요).
        builder.Property(e => e.Slot)
            .IsRequired();

        builder.Property(e => e.ItemId)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        builder.ToTable("user_equipments");
    }
}
