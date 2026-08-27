using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Abilities;
using Shared.Infrastructure.Spawn;

namespace Server.Actors;

/// <summary>액터의 종족. <b>타입이 아니라 데이터</b> — 방 계층이 사망 후속을 분기할 때만 본다.</summary>
public enum ActorKind : byte
{
    Player = 0,
    Monster = 1,
}

/// <summary>AI 타깃 후보의 평면 좌표. 순수 계산층에 상태 타입을 새지 않게 하는 경량 입력.</summary>
public readonly record struct TargetPos(float X, float Z);

/// <summary>
/// 한 틱 동안 액터가 <b>무엇을 하기로 했는가</b>. 결정과 자기 상태 커밋은 액터가 끝냈고,
/// 이 결과를 <b>패킷·피해로 번역</b>하는 것은 방 계층의 일이다.
///
/// <para>예전에는 <c>int targetIdx</c> 만 돌려줘서 "누굴 노리는지"까지만 액터가 정하고
/// 어빌리티 선택·쿨다운 커밋은 방이 다시 판단했다 — 결정이 두 곳에 쪼개져 있었다.</para>
/// </summary>
/// <param name="TargetIndex">노린 타깃의 인덱스(<c>targets</c> 기준). 없으면 −1.</param>
/// <param name="Cast">이번 틱에 발동한 어빌리티. 없으면 null(쿨다운·사거리·차단 태그).</param>
/// <param name="ExpiredEffectIds">
/// 이번 틱에 <b>만료된</b> 지속 Effect 의 인스턴스 id. 없으면 null —
/// 만료는 드문 사건인데 매 틱·매 액터마다 빈 리스트를 만들면 10Hz 만큼 쓰레기가 된다.
/// </param>
public readonly record struct ActorTickResult(
    int TargetIndex, AbilityDef? Cast, IReadOnlyList<int>? ExpiredEffectIds = null)
{
    /// <summary>아무것도 하지 않음.</summary>
    public static readonly ActorTickResult None = new(-1, null, null);
}

/// <summary>
/// <b>캐릭터의 단일 표현.</b> 플레이어든 몬스터든 싸우는 것은 전부 Actor 다.
///
/// <para>Actor 는 <b>신원 · 공간 · 수명</b>만 맡는다. 전투 상태(HP·마나·스탯·태그·쿨다운)는
/// <see cref="AbilitySystemComponent"/> 가 갖고 Actor 는 그것을 <b>들고만 있다</b>.
/// UserId·닉네임·접속 여부·스폰 슬롯은 방 참가자의 속성이라 <see cref="Server.Room.RoomMember"/> 소유다.</para>
///
/// <para><b>역참조 금지</b>: Session 은 재접속마다 교체되므로 액터가 붙들면 유령 참조가 된다.
/// 방향은 항상 <c>Session → Room → RoomMember → Actor</c> 단방향.</para>
/// </summary>
public abstract class Actor
{
    protected Actor(long actorId) => ActorId = actorId;

    /// <summary>부호 규약(<see cref="ActorIds"/>): 양수=플레이어(UserId) / 음수=몬스터(−InstanceId).</summary>
    public long ActorId { get; }

    public abstract ActorKind Kind { get; }

    public float PosX;
    public float PosY;
    public float PosZ;
    public float RotY;

    /// <summary>전투 상태 일체(속성·태그·쿨다운). 종족과 무관하게 같은 타입.</summary>
    public AbilitySystemComponent Gas { get; } = new();

    /// <summary>지금 적대 액터의 표적이 될 수 있는가. <b>방 계층이 세팅</b> — 액터는 이유(미입장·끊김·다운)를 모른다.</summary>
    public bool IsTargetable { get; set; }

    /// <summary>
    /// 액터 내부 상태를 1틱 진행한다. <b>패킷을 만들지 않는다</b> — 무엇을 보낼지는 방 계층 책임.
    /// </summary>
    /// <param name="targets">노려도 되는 적대 액터 좌표(방 계층이 자격을 걸러 넣는다).</param>
    /// <returns>
    /// 이번 틱의 결정(<see cref="ActorTickResult"/>). 기본 구현은 <b>지속 Effect 만료</b>만 처리한다 —
    /// 만료는 종족과 무관한 공통 처리이고, <b>만료 권위가 서버에 있다는 것이 여기서 강제된다</b>
    /// (예전엔 서버가 CC 를 걸어놓고 언제 풀리는지는 클라에 맡겼다).
    /// </returns>
    public virtual ActorTickResult Tick(float dt, long nowMs, IReadOnlyList<TargetPos> targets, MapBounds bounds)
        => new(-1, null, Gas.TickEffects(nowMs));
}
