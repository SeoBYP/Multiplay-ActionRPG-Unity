using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEquipments : Migration
    {
        // 착용 상태 — (UserId, Slot) 복합키. Slot=enum→int, 정의(슬롯·스탯)는 EquipmentCatalog(코드).
        // 하우스 스타일(raw SQL · IF NOT EXISTS 멱등)에 맞춰 손으로 작성.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS user_equipments (
                    "UserId"    bigint NOT NULL,
                    "Slot"      integer NOT NULL,
                    "ItemId"    character varying(64) NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_user_equipments" PRIMARY KEY ("UserId", "Slot")
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS user_equipments;");
        }
    }
}
