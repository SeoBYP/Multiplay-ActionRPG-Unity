using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserQuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 멱등 raw SQL(AddWallets/AddUserCodex 동일 패턴) — 이미 있으면 무시.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS user_quests (
                    "UserId"    bigint NOT NULL,
                    "QuestId"   character varying(64) NOT NULL,
                    "Status"    integer NOT NULL,
                    "Progress"  integer NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_user_quests" PRIMARY KEY ("UserId", "QuestId")
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_quests");
        }
    }
}
