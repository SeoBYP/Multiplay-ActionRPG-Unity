using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Spawn;

namespace Server.Actors;

// UserId 가 필요하면 ActorId 를 쓴다(플레이어는 양수 UserId).
public sealed class PlayerActor(long userId) : Actor(ActorIds.FromPlayer(userId))
{
    public override ActorKind Kind => ActorKind.Player;

    // TODO : 추후 기본 공격 테이블을 따로 만들어서 관리
    public static readonly IReadOnlyList<string> BasicAbilityIds =
    [
        "basic_swing", "heavy_swing", "combo_a", "combo_b", "combo_c", "combo_d"
    ];

    // 무적은 태그가 아니라 만료 시각 — 틱 주기와 무관하게 정확하다.

    // Unix ms. 0 = 무적 아님.
    public long InvulnerableUntilMs { get; set; }

    private long _lastDodgeMs;

    // 쿨다운·마나 둘 다 통과해야 소모한다. 하나라도 모자라면 아무것도 소모하지 않고 false — C_Dodge 연사로 영구 무적 차단.
    public bool TryBeginDodge(long nowMs, int manaCost = 0)
    {
        if (_lastDodgeMs != 0 && nowMs - _lastDodgeMs < DodgeConfig.CooldownMs) return false;
        if (Gas[EGameplayAttribute.Mana] < manaCost) return false;

        if (manaCost > 0) Gas[EGameplayAttribute.Mana] -= manaCost;
        _lastDodgeMs = nowMs;
        InvulnerableUntilMs = nowMs + DodgeConfig.IframeMs;
        return true;
    }

    public bool IsInvulnerableAt(long nowMs) => nowMs < InvulnerableUntilMs;

    // 콤보는 단계마다 abilityId 가 달라 개별 쿨다운이 연타를 못 막는다 — 직전 스윙의 ComboChainMs 로 게이트한다.
    private long _lastComboAtMs;
    private int _lastComboChainMs;

    // toleranceMs = 네트워크 지터 허용치(없으면 정상 콤보가 거부돼 데미지 유실). 거부 시 무기록.
    public bool TryBeginComboAttack(long nowMs, int thisChainMs, int minFallbackMs, int toleranceMs = 0)
    {
        if (_lastComboChainMs > 0)
        {
            long required = Math.Max(0, _lastComboChainMs - toleranceMs);
            if (nowMs - _lastComboAtMs < required) return false;
        }

        _lastComboAtMs = nowMs;
        // 저작 실수로 0 이어도 버스트 구멍이 열리지 않도록 최소값 보장.
        _lastComboChainMs = thisChainMs > 0 ? thisChainMs : minFallbackMs;
        return true;
    }

    // 이동은 클라 권위 릴레이라 서버는 마나 회복만 진행.
    public override ActorTickResult Tick(float dt, long nowMs, IReadOnlyList<TargetPos> targets, MapBounds bounds)
    {
        Gas.RegenMana(dt);
        return base.Tick(dt, nowMs, targets, bounds); // 지속 Effect 만료(CC 해제)는 공통 처리
    }
}