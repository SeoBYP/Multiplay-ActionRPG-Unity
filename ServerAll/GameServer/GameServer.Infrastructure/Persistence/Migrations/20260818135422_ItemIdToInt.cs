using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ItemIdToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ItemId 가 문자열("potion_hp_small")에서 numericId(1001)로 바뀐다. Postgres 는 varchar→int 를
            // 암시적으로 캐스팅하지 못해 기존 행이 남아 있으면 ALTER 가 실패한다.
            //
            // 기존 행은 **버린다**(사용자 결정 2026-08-18). 대부분 E2E 가 매 실행마다 만든 임시 계정
            // 데이터이고(실측: users 4,273 / inventory 250 / codex 315 / equipment 3), 보존해도 다시
            // 플레이하면 재획득된다. 매핑 변환을 택하면 아이템 10종 매핑이 이 마이그레이션에 영구히
            // 하드코딩된다 — 얻는 것에 비해 비용이 크다.
            //
            // ⚠ 운영 데이터가 생긴 뒤에는 이 방식을 쓰면 안 된다. 그때는 신규 컬럼 추가 → 매핑 UPDATE →
            //   PK 재구성 → 구 컬럼 제거의 4단계로 무중단 전환한다.
            migrationBuilder.Sql("DELETE FROM inventory_items;");
            migrationBuilder.Sql("DELETE FROM user_codex;");
            migrationBuilder.Sql("DELETE FROM user_equipments;");

            // AlterColumn<int> 로는 안 된다 — Postgres 는 varchar→integer 에 암시적 캐스팅이 없어
            // **테이블이 비어 있어도** USING 절을 요구한다(42804: cannot be cast automatically).
            // 위에서 행을 비웠으므로 USING 0 이 안전하다(변환할 값 자체가 없다).
            migrationBuilder.Sql(@"ALTER TABLE inventory_items  ALTER COLUMN ""ItemId"" TYPE integer USING 0;");
            migrationBuilder.Sql(@"ALTER TABLE user_codex       ALTER COLUMN ""ItemId"" TYPE integer USING 0;");
            migrationBuilder.Sql(@"ALTER TABLE user_equipments  ALTER COLUMN ""ItemId"" TYPE integer USING 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ItemId",
                table: "user_equipments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ItemId",
                table: "user_codex",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ItemId",
                table: "inventory_items",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
