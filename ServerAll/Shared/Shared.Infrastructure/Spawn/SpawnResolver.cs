namespace Shared.Infrastructure.Spawn;

/// <summary>
/// 맵 내 한 스폰 지점(위치 + Y축 회전).
/// 서버·클라가 동일 정의를 공유하며, 좌표는 spawn-layouts.json 에서 로드한다.
/// </summary>
public sealed record SpawnPoint(float X, float Y, float Z, float RotY);

/// <summary>
/// 한 맵의 스폰 레이아웃 — 플레이어 스폰 포인트 + 맵 경계 + 몬스터 스폰 정의 + 클리어 Exp 보상.
/// spawnIndex 가 Points 배열의 인덱스가 된다. Bounds/Monsters 는 서버 권위 몬스터 시뮬레이션이 사용한다.
/// ExpReward = 던전 클리어 시 참가자 전원에게 지급할 경험치(던전별 차등). 양 서버 공유 단일 소스.
/// </summary>
public sealed record MapSpawnLayout(
    string MapId,
    IReadOnlyList<SpawnPoint> Points,
    MapBounds Bounds,
    IReadOnlyList<MonsterSpawnDef> Monsters,
    long ExpReward,
    int MonsterLevel = 0)
{
    /// <summary>
    /// 이 스폰의 <b>유효 레벨</b>(AC-E2). <c>spawn.Level</c> 이 있으면 그것, 없으면 맵 기본, 둘 다 없으면 <b>1</b>.
    ///
    /// <para>규칙을 여기 <b>한 곳</b>에 둔다 — 서버 스폰과 (나중의) 클라 표시가 각자 구현하면 조용히 어긋난다.
    /// 0 = "미저작"이라 기존 JSON 이 그대로 L1 로 떨어진다(= 레벨 도입 전과 동일 동작).</para>
    /// </summary>
    public int ResolveLevel(MonsterSpawnDef def) => ResolveLevel(def.Level, MonsterLevel);

    /// <summary>
    /// 레벨 해석의 <b>단일 구현</b>. layout 인스턴스를 못 넘기는 호출부(<c>Room.SpawnMonsters</c>)도 이걸 쓴다 —
    /// 규칙을 두 번 구현하면 조용히 어긋난다.
    /// </summary>
    public static int ResolveLevel(int spawnLevel, int mapLevel)
    {
        if (spawnLevel > 0) return spawnLevel;  // 스폰별 override — 같은 맵의 엘리트/보스를 올릴 때
        if (mapLevel > 0) return mapLevel;      // 던전 기본 — 한 줄로 전체 대역 조절
        return 1;                               // 미저작 = 레벨 도입 전과 동일(항등)
    }
}

/// <summary>
/// 결정론적 스폰 리졸버 — 순수 함수.
/// 같은 (layout, spawnIndex) 입력에는 항상 같은 SpawnPoint 를 반환한다.
/// 서버·클라가 동일 코드를 미러링해 두므로 네트워크로 좌표를 전송하지 않고도 같은 결과를 얻는다.
/// </summary>
public static class SpawnResolver
{
    /// <summary>
    /// spawnIndex 가 포인트 수를 초과하면 모듈러로 순환 배치한다(음수도 안전).
    /// </summary>
    public static SpawnPoint Resolve(MapSpawnLayout layout, int spawnIndex)
    {
        if (layout is null) throw new ArgumentNullException(nameof(layout));
        if (layout.Points.Count == 0)
            throw new InvalidOperationException($"Spawn layout '{layout.MapId}' has no points.");

        var count = layout.Points.Count;
        var index = ((spawnIndex % count) + count) % count;
        return layout.Points[index];
    }
}
