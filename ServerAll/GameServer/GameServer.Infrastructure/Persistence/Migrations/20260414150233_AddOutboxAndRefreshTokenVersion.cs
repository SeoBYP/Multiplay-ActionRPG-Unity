using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Persistence.Migrations
{
    public partial class AddOutboxAndRefreshTokenVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS user_credentials
                ADD COLUMN IF NOT EXISTS "RefreshTokenVersion" integer NOT NULL DEFAULT 0;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS outbox_messages (
                    "MessageId" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    "Topic" character varying(255) NOT NULL,
                    "Payload" text NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "ProcessedAt" timestamp with time zone NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_outbox_messages_ProcessedAt"
                ON outbox_messages ("ProcessedAt");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS outbox_messages;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS user_credentials
                DROP COLUMN IF EXISTS "RefreshTokenVersion";
                """);
        }
    }
}
