using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProgressions : Migration
    {
        // 플레이어 진행(레벨·경험치) — users 와 1:1 (UserId PK=FK, identity 아님).
        // 하우스 스타일(raw SQL · IF NOT EXISTS 멱등)에 맞춰 손으로 작성.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS user_progressions (
                    "UserId"    bigint PRIMARY KEY,
                    "Level"     integer NOT NULL DEFAULT 1,
                    "Exp"       bigint NOT NULL DEFAULT 0,
                    "UpdatedAt" timestamp with time zone NOT NULL
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS user_progressions;");
        }
    }
}
