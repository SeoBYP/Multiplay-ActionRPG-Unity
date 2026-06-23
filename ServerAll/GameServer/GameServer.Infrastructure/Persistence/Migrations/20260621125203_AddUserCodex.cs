using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCodex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 멱등 raw SQL(AddWallets/AddInventoryItems 동일 패턴) — 이미 있으면 무시.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS user_codex (
                    "UserId"       bigint NOT NULL,
                    "ItemId"       character varying(64) NOT NULL,
                    "DiscoveredAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_user_codex" PRIMARY KEY ("UserId", "ItemId")
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_codex");
        }
    }
}
