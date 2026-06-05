namespace Shared.Infrastructure.Spawn;

/// <summary>패트롤 경로의 한 지점(XZ). 몬스터는 patrol 목록을 순서대로 순회한다.</summary>
public sealed record PatrolPoint(float X, float Z);

/// <summary>
/// 한 몬스터 스폰 정의(서버 권위 시뮬레이션 입력). spawn-layouts.json 의 monsters[] 항목과 1:1.
/// 클라 런타임은 사용하지 않는다 — 서버가 S_SpawnMonster 로 위치를 알려준다.
/// </summary>
public sealed record MonsterSpawnDef(
    string MonsterId,
    float X,
    float Y,
    float Z,
    float RotY,
    int Count,
    int Wave,
    IReadOnlyList<PatrolPoint> Patrol);
