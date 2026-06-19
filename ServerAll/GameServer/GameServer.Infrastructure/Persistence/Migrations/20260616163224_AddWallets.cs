using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 멱등 raw SQL(AddInventoryItems/AddUserEquipments 동일 패턴) — 이미 있으면 무시.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS user_wallets (
                    "UserId"    bigint NOT NULL,
                    "Balance"   bigint NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_user_wallets" PRIMARY KEY ("UserId")
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_wallets");
        }
    }
}
