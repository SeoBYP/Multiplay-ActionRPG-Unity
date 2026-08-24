using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeDungeonRoomPlayerUserIdUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_dungeon_room_players_UserId",
                table: "dungeon_room_players");

            // UNIQUE 를 걸기 전에 기존 중복(= 이 제약이 없어서 생길 수 있었던 다중 방 소속)을 정리한다.
            // 정리 없이 CreateIndex 를 하면 마이그레이션이 실패해 서버가 뜨지 않는다.
            // 남기는 행 = 가장 먼저 들어간 방(JoinedAt 최소, 동률이면 RoomId 최소).
            migrationBuilder.Sql("""
                DELETE FROM dungeon_room_players d
                USING (
                    SELECT "RoomId", "UserId"
                    FROM (
                        SELECT "RoomId",
                               "UserId",
                               ROW_NUMBER() OVER (PARTITION BY "UserId" ORDER BY "JoinedAt", "RoomId") AS rn
                        FROM dungeon_room_players
                    ) ranked
                    WHERE rn > 1
                ) dup
                WHERE d."RoomId" = dup."RoomId"
                  AND d."UserId" = dup."UserId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_dungeon_room_players_UserId",
                table: "dungeon_room_players",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_dungeon_room_players_UserId",
                table: "dungeon_room_players");

            migrationBuilder.CreateIndex(
                name: "IX_dungeon_room_players_UserId",
                table: "dungeon_room_players",
                column: "UserId");
        }
    }
}
