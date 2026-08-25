namespace Shared.Infrastructure.Spawn;

/// <summary>
/// 맵 식별자 상수. spawn-layouts.json 의 키와 1:1 대응한다.
/// 던전 선택 UI가 생기기 전(M1)에는 게임 시작이 항상 Default 를 사용한다.
/// </summary>
public static class MapIds
{
    public const string Dungeon01 = "dungeon_01";

    /// <summary>Main(비던전) 필드. 클라 MainLifetimeScope.mainMapId 기본값과 같아야 한다.</summary>
    public const string MainField01 = "main_field_01";

    public const string Default = Dungeon01;
}
