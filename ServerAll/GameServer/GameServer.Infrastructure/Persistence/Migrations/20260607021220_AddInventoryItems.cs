using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryItems : Migration
    {
        // 인벤토리 소유(스택형) — (UserId, ItemId) 복합키. 정의는 ItemCatalog(코드), 여기엔 수량만.
        // 하우스 스타일(raw SQL · IF NOT EXISTS 멱등)에 맞춰 손으로 작성.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS inventory_items (
                    "UserId"    bigint NOT NULL,
                    "ItemId"    character varying(64) NOT NULL,
                    "Quantity"  integer NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_inventory_items" PRIMARY KEY ("UserId", "ItemId")
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS inventory_items;");
        }
    }
}
