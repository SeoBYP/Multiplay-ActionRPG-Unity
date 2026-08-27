using Script.System.GamePlayAbilitySystem;
using Server.Monster;
using Shared.Infrastructure.Abilities;
using Shared.Infrastructure.Monsters;
using Shared.Infrastructure.Spawn;

namespace Server.Actors;

/// <summary>
/// 몬스터 캐릭터. 서버가 위치·페이즈를 매 틱 갱신하고 S_MonsterState 로 브로드캐스트한다(클라는 보간만).
/// 공통 전투 상태(위치·HP·태그·쿨다운)는 <see cref="Actor"/> 소유 — 여기엔 몬스터 고유만 남는다.
/// </summary>
public sealed class MonsterActor(int instanceId) : Actor(ActorIds.FromMonster(instanceId))
{
    public override ActorKind Kind => ActorKind.Monster;

    /// <summary>방 내 고유 번호(≥1). <c>ActorId</c> = −InstanceId.</summary>
    public int InstanceId { get; } = instanceId;

    public string MonsterId { get; init; } = "";

    /// <summary>스폰 시 <b>1회 확정</b>되는 레벨(매 틱 재계산하지 않는다 — 레벨업하는 몬스터는 없다).</summary>
    public int Level { get; init; } = 1;

    /// <summary>등급 분류. 카탈로그(monsterId 행)에서 온다 — 스탯에 곱해지지 않고 표시·연출 분기용.</summary>
    public MonsterTier Tier { get; init; } = MonsterTier.Normal;

    public MonsterPhase Phase;

    /// <summary>스폰 지점(대기·복귀 기준).</summary>
    public float SpawnX { get; init; }
    public float SpawnZ { get; init; }

    /// <summary>패트롤 경로(순서대로 순회). 비면 제자리 경비.</summary>
    public IReadOnlyList<PatrolPoint> Patrol { get; init; } = Array.Empty<PatrolPoint>();
    public int PatrolIndex;

    // ── dirty-flag: 직전에 보낸 값과 같으면 송신 생략(Idle 경비 몬스터 트래픽 0) ──
    private float _sentPosX, _sentPosY, _sentPosZ, _sentRotY;
    private int _sentHp;
    private MonsterPhase _sentPhase;
    private bool _stateSent;

    /// <summary>직전 송신 이후 위치·회전·HP·페이즈가 바뀌었나(첫 송신은 항상 true).</summary>
    public bool StateDirty()
        => !_stateSent
           || _sentPosX != PosX || _sentPosY != PosY || _sentPosZ != PosZ
           || _sentRotY != RotY || _sentHp != Gas[EGameplayAttribute.Health] || _sentPhase != Phase;

    /// <summary>현재 상태를 "송신됨"으로 기록. S_MonsterState 를 실제로 보낸 직후 호출.</summary>
    public void MarkStateSent()
    {
        _sentPosX = PosX; _sentPosY = PosY; _sentPosZ = PosZ; _sentRotY = RotY;
        _sentHp = Gas[EGameplayAttribute.Health]; _sentPhase = Phase; _stateSent = true;
    }

    // ── 상태 시퀀스: 클라가 순서 역전을 무효화하기 위한 몬스터별 단조 증가 버전 ──
    private int _seq;

    /// <summary>
    /// 다음 상태 버전을 발급한다. <b>S_MonsterState 를 만드는 그 자리에서</b> 호출한다(송신 시점 아님 —
    /// 생성 순서가 곧 상태 순서이고, 막으려는 것이 생성≠송신 순서이기 때문).
    /// 두 생산자(틱 / 데미지)가 다른 컨텍스트에서 부르므로 <c>Interlocked</c> 로 발급한다.
    /// </summary>
    public int NextSeq() => Interlocked.Increment(ref _seq);

    /// <summary>
    /// AI 1틱 — <b>이동·페이즈 결정 + 어빌리티 선택 + 쿨다운 커밋</b>까지 몬스터가 스스로 한다.
    /// 계산은 <see cref="MonsterAiMath"/>(순수)에 맡기고 여기선 상태만 넘긴다.
    ///
    /// <para>패킷·피해는 만들지 않는다 — 대상의 방어력을 읽어야 하는데 액터가 다른 액터를 뒤지기 시작하면
    /// 경계가 무너진다. 결과(<see cref="ActorTickResult"/>)를 방 계층이 번역한다.</para>
    /// </summary>
    public override ActorTickResult Tick(float dt, long nowMs, IReadOnlyList<TargetPos> targets, MapBounds bounds)
    {
        // 만료를 먼저 걷는다 — 스턴이 이 틱에 풀렸다면 이번 AI 판단부터 풀린 상태로 가야 한다.
        var expired = Gas.TickEffects(nowMs);

        int targetIdx = MonsterAiMath.Step(this, targets, bounds, Server.Monster.MonsterCatalog.Get(MonsterId), dt);
        if (Phase != MonsterPhase.Attack || targetIdx < 0)
            return new ActorTickResult(targetIdx, null, expired);

        var chosen = SelectAbility(targets[targetIdx], nowMs);
        if (chosen is not null)
            Gas.MarkCast(chosen.Id, nowMs); // 발동 확정 = 쿨다운 시작(자기 상태는 자기가 커밋)

        return new ActorTickResult(targetIdx, chosen, expired);
    }

    /// <summary>
    /// 지금 이 대상에게 쓸 수 있는 어빌리티를 고른다. 없으면 null.
    /// 규칙: 저작 순서(MonsterDefinition.abilityIds) = <b>우선순위</b> → 사거리 안 + 발동 가능한 첫 어빌리티.
    /// 게이트는 <see cref="AbilityActivationMath"/>(플레이어와 같은 Shared 규칙) — 쿨다운·마나·차단 태그.
    /// </summary>
    private AbilityDef? SelectAbility(TargetPos target, long nowMs)
    {
        float dx = target.X - PosX;
        float dz = target.Z - PosZ;
        float distSq = dx * dx + dz * dz;

        foreach (var ability in Server.Monster.MonsterCatalog.GetAbilities(MonsterId))
        {
            if (distSq > ability.ActivationRange * ability.ActivationRange)
                continue; // 이 스킬 사거리 밖

            if (!AbilityActivationMath.CanActivate(
                    nowMs, Gas.LastCast(ability.Id), ability.Timeline.CooldownMs,
                    manaCost: 0,
                    currentMana: Gas[EGameplayAttribute.Mana],
                    blocked: Gas.IsActivationBlocked))
                continue; // 쿨다운/차단
            return ability;
        }
        return null;
    }
}
