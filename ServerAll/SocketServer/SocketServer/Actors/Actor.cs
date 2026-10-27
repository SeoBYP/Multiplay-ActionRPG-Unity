using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Abilities;
using Shared.Infrastructure.Spawn;

namespace Server.Actors;

public enum ActorKind : byte
{
    Player = 0,
    Monster = 1,
}

public readonly record struct TargetPos(float X, float Z);

// TargetIndex −1 = 없음. ExpiredEffectIds null = 없음(빈 리스트 할당 회피).
public readonly record struct ActorTickResult(
    int TargetIndex, AbilityDef? Cast, IReadOnlyList<int>? ExpiredEffectIds = null)
{
    public static readonly ActorTickResult None = new(-1, null, null);
}

// 전투 상태는 ASC 가 소유. 역참조 금지: Session 은 재접속마다 교체되므로 Session → Room → RoomMember → Actor 단방향.
public abstract class Actor
{
    protected Actor(long actorId) => ActorId = actorId;

    // 양수=플레이어(UserId) / 음수=몬스터(−InstanceId) — ActorIds 규약.
    public long ActorId { get; }

    public abstract ActorKind Kind { get; }

    public float PosX;
    public float PosY;
    public float PosZ;
    public float RotY;

    public AbilitySystemComponent Gas { get; } = new();

    // 방 계층이 세팅 — 액터는 이유(미입장·끊김·다운)를 모른다.
    public bool IsTargetable { get; set; }

    // 기본 구현은 지속 Effect 만료만 처리 — 만료 권위가 서버에 있음을 여기서 강제한다.
    public virtual ActorTickResult Tick(float dt, long nowMs, IReadOnlyList<TargetPos> targets, MapBounds bounds)
        => new(-1, null, Gas.TickEffects(nowMs));
}
