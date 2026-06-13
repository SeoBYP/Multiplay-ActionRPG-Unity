namespace Shared.Infrastructure.Spawn;

/// <summary>패트롤 경로의 한 지점(XZ). 몬스터는 patrol 목록을 순서대로 순회한다.</summary>
public sealed record PatrolPoint(float X, float Z);

/// <summary>
/// 한 몬스터 스폰 정의. spawn-layouts.json 의 monsters[] 항목과 1:1.
/// - 던전(서버 권위): 서버 시뮬 입력. 클라 런타임 미사용(서버가 S_SpawnMonster 로 위치 통지).
/// - Main(B-lite): 클라가 슬롯 기반 로컬 스폰 + 서버가 ClaimKill 검증의 기준으로 사용
///   (`SlotId`=클레임 키, `RespawnCooldownMs`=재청구 쿨다운 = 파밍률 상한). 정본 = main-spawn-claim.md.
/// </summary>
public sealed record MonsterSpawnDef(
    string MonsterId,
    float X,
    float Y,
    float Z,
    float RotY,
    int Count,
    int Wave,
    IReadOnlyList<PatrolPoint> Patrol,
    int SlotId = 0,
    int RespawnCooldownMs = 0);
