using Shared.Infrastructure.Spawn;

namespace Server.Monster;

/// <summary>몬스터 행동 페이즈. S_MonsterState.Phase(byte)와 1:1.</summary>
public enum MonsterPhase : byte
{
    Idle = 0,   // 타깃 없음·패트롤 없음 → 제자리
    Patrol = 1, // 패트롤 경로 순회
    Chase = 2,  // 최근접 플레이어 추격
    Attack = 3, // 사거리 진입 → 정지·공격
}

/// <summary>
/// 방 안 몬스터 1마리의 서버 권위 런타임 상태. 위치·HP·페이즈는 서버가 매 틱 갱신하고
/// S_MonsterState 로 브로드캐스트한다. 클라는 이 값을 받아 보간만 한다.
/// </summary>
public sealed class MonsterState
{
    public int InstanceId { get; init; }
    public string MonsterId { get; init; } = "";

    public float PosX;
    public float PosY;
    public float PosZ;
    public float RotY;

    public int Hp;
    public int MaxHp { get; init; }

    public MonsterPhase Phase;

    /// <summary>스폰 지점(대기·복귀 기준).</summary>
    public float SpawnX { get; init; }
    public float SpawnZ { get; init; }

    /// <summary>패트롤 경로(순서대로 순회). 비면 제자리 경비.</summary>
    public IReadOnlyList<PatrolPoint> Patrol { get; init; } = Array.Empty<PatrolPoint>();
    public int PatrolIndex;

    /// <summary>마지막 공격 시각(서버 ms). 공격 쿨다운 판정에 사용 — ④/⑤.</summary>
    public long LastAttackAt;

    public bool IsDead => Hp <= 0;
}
