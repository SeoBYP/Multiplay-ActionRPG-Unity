using Script.System.GamePlayAbilitySystem;
using Server.Monster;
using Shared.Infrastructure.Abilities;
using Shared.Infrastructure.Monsters;
using Shared.Infrastructure.Spawn;

namespace Server.Actors;

public sealed class MonsterActor(int instanceId) : Actor(ActorIds.FromMonster(instanceId))
{
    public override ActorKind Kind => ActorKind.Monster;

    public int InstanceId { get; } = instanceId;

    public string MonsterId { get; init; } = "";

    public int Level { get; init; } = 1;

    public MonsterTier Tier { get; init; } = MonsterTier.Normal;

    public MonsterPhase Phase;

    public float SpawnX { get; init; }
    public float SpawnZ { get; init; }

    public IReadOnlyList<PatrolPoint> Patrol { get; init; } = Array.Empty<PatrolPoint>();
    public int PatrolIndex;

    private float _sentPosX, _sentPosY, _sentPosZ, _sentRotY;
    private int _sentHp;
    private MonsterPhase _sentPhase;
    private bool _stateSent;

    public bool StateDirty()
        => !_stateSent
           || _sentPosX != PosX || _sentPosY != PosY || _sentPosZ != PosZ
           || _sentRotY != RotY || _sentHp != Gas[EGameplayAttribute.Health] || _sentPhase != Phase;

    public void MarkStateSent()
    {
        _sentPosX = PosX; _sentPosY = PosY; _sentPosZ = PosZ; _sentRotY = RotY;
        _sentHp = Gas[EGameplayAttribute.Health]; _sentPhase = Phase; _stateSent = true;
    }

    private int _seq;

    // 생성 순서가 곧 상태 순서 — 송신 시점이 아니라 S_MonsterState 를 만드는 자리에서 호출. 두 생산자(틱/데미지)라 Interlocked.
    public int NextSeq() => Interlocked.Increment(ref _seq);

    public override ActorTickResult Tick(float dt, long nowMs, IReadOnlyList<TargetPos> targets, MapBounds bounds)
    {
        // 만료를 먼저 걷는다 — 이번 틱에 스턴이 풀렸다면 이번 AI 판단부터 풀린 상태여야 한다.
        var expired = Gas.TickEffects(nowMs);

        int targetIdx = MonsterAiMath.Step(this, targets, bounds, Server.Monster.MonsterCatalog.Get(MonsterId), dt);
        if (Phase != MonsterPhase.Attack || targetIdx < 0)
            return new ActorTickResult(targetIdx, null, expired);

        var chosen = SelectAbility(targets[targetIdx], nowMs);
        if (chosen is not null)
            Gas.MarkCast(chosen.Id, nowMs);

        return new ActorTickResult(targetIdx, chosen, expired);
    }

    // 저작 순서(abilityIds) = 우선순위 → 사거리 안 + 발동 가능한 첫 어빌리티.
    private AbilityDef? SelectAbility(TargetPos target, long nowMs)
    {
        float dx = target.X - PosX;
        float dz = target.Z - PosZ;
        float distSq = dx * dx + dz * dz;

        foreach (var ability in Server.Monster.MonsterCatalog.GetAbilities(MonsterId))
        {
            if (distSq > ability.ActivationRange * ability.ActivationRange)
                continue;

            if (!AbilityActivationMath.CanActivate(
                    nowMs, Gas.LastCast(ability.Id), ability.Timeline.CooldownMs,
                    manaCost: 0,
                    currentMana: Gas[EGameplayAttribute.Mana],
                    blocked: Gas.IsActivationBlocked))
                continue;
            return ability;
        }
        return null;
    }
}
