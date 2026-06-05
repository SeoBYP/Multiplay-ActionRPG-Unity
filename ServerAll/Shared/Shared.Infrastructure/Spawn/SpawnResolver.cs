namespace Shared.Infrastructure.Spawn;

/// <summary>
/// 맵 내 한 스폰 지점(위치 + Y축 회전).
/// 서버·클라가 동일 정의를 공유하며, 좌표는 spawn-layouts.json 에서 로드한다.
/// </summary>
public sealed record SpawnPoint(float X, float Y, float Z, float RotY);

/// <summary>
/// 한 맵의 스폰 레이아웃 — 플레이어 스폰 포인트 + 맵 경계 + 몬스터 스폰 정의.
/// spawnIndex 가 Points 배열의 인덱스가 된다. Bounds/Monsters 는 서버 권위 몬스터 시뮬레이션이 사용한다.
/// </summary>
public sealed record MapSpawnLayout(
    string MapId,
    IReadOnlyList<SpawnPoint> Points,
    MapBounds Bounds,
    IReadOnlyList<MonsterSpawnDef> Monsters);

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
