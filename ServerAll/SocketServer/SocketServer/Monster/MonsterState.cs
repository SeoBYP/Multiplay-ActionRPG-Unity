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

    /// <summary>
    /// 어빌리티별 마지막 발동 시각(서버 ms). AC-B B4: 몬스터가 여러 어빌리티를 가질 수 있으므로(보스)
    /// 쿨다운을 **어빌리티 단위**로 추적한다(구 단일 LastAttackAt 대체).
    /// </summary>
    private readonly Dictionary<string, long> _lastCastByAbility = new();

    /// <summary>이 어빌리티의 마지막 발동 시각. 한 번도 안 썼으면 0(=쿨다운 통과).</summary>
    public long GetLastCast(string abilityId)
        => _lastCastByAbility.TryGetValue(abilityId, out var t) ? t : 0L;

    /// <summary>발동 확정 시 기록(쿨다운 시작).</summary>
    public void MarkCast(string abilityId, long nowMs) => _lastCastByAbility[abilityId] = nowMs;

    public bool IsDead => Hp <= 0;

    // ── dirty-flag(§5.2): 직전에 S_MonsterState 로 보낸 값. 변화 없으면 송신 생략(idle 몬스터 트래픽 0). ──
    private float _sentPosX, _sentPosY, _sentPosZ, _sentRotY;
    private int _sentHp;
    private MonsterPhase _sentPhase;
    private bool _stateSent;

    /// <summary>직전 송신 이후 위치·회전·HP·페이즈가 바뀌었나(첫 송신은 항상 true). Chase/Patrol 은 매 틱 변함=항상 true, Idle=false.</summary>
    public bool StateDirty()
        => !_stateSent
           || _sentPosX != PosX || _sentPosY != PosY || _sentPosZ != PosZ
           || _sentRotY != RotY || _sentHp != Hp || _sentPhase != Phase;

    /// <summary>현재 상태를 "송신됨"으로 기록 → 다음 StateDirty 는 변화가 없으면 false. S_MonsterState 를 실제로 보낸 직후 호출.</summary>
    public void MarkStateSent()
    {
        _sentPosX = PosX; _sentPosY = PosY; _sentPosZ = PosZ; _sentRotY = RotY;
        _sentHp = Hp; _sentPhase = Phase; _stateSent = true;
    }
}
