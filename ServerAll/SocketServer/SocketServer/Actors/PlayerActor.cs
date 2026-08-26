using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Spawn;

namespace Server.Actors;

/// <summary>
/// 플레이어 캐릭터. 공통 전투 상태는 <see cref="Actor.Gas"/> 가 갖고, 여기엔 <b>플레이어 고유 발동 cadence</b>만 남는다
/// (회피 무적 창 · 콤보 간격). UserId 가 필요하면 <c>ActorId</c> 를 쓴다 — 플레이어는 부호 규약상 양수 UserId 다.
/// </summary>
public sealed class PlayerActor(long userId) : Actor(ActorIds.FromPlayer(userId))
{
    public override ActorKind Kind => ActorKind.Player;

    // ── 회피(Dodge) 무적 ──────────────────────────────────────────────
    // 태그(State.Invulnerable)가 아니라 만료 시각으로 둔다: 태그로 하면 만료를 틱이 돌려야 하는데
    // 지금 서버는 Effect 만료를 소유하지 않는다. 시각 비교는 틱 없이 정확하다.

    /// <summary>이 시각(Unix ms)까지 피해를 무시한다. 0 = 무적 아님.</summary>
    public long InvulnerableUntilMs { get; set; }

    private long _lastDodgeMs;

    /// <summary>
    /// 회피 발동 게이트(쿨다운 + 마나). 둘 다 통과해야 마나를 차감하고 무적 창을 부여한다.
    /// 어느 하나라도 모자라면 <b>아무것도 소모하지 않고</b> false(원자적) — C_Dodge 연사 = 영구 무적 차단.
    /// </summary>
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

    // ── 콤보 cadence ─────────────────────────────────────────────────
    // 콤보는 단계마다 abilityId 가 달라 **개별 쿨다운으론 연타를 못 막는다**(각자 첫 발동이라 쿨다운이 빔).
    // 그래서 직전 스윙의 ComboChainMs 를 기억해 다음 콤보를 막는 별도 게이트가 필요하다.
    private long _lastComboAtMs;
    private int _lastComboChainMs;

    /// <summary>
    /// 콤보 cadence 게이트. 직전 콤보의 <c>ComboChainMs</c> 가 지났으면 true(이번 스윙 기록), 아니면 무기록 false.
    /// <paramref name="toleranceMs"/> = 네트워크 지터 허용치 — 없으면 정상 콤보가 거부돼 데미지가 유실된다.
    /// </summary>
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

    /// <summary>플레이어는 서버가 움직이지 않는다(이동은 클라 권위 릴레이). 마나 회복만 진행.</summary>
    public override ActorTickResult Tick(float dt, long nowMs, IReadOnlyList<TargetPos> targets, MapBounds bounds)
    {
        Gas.RegenMana(dt);
        return ActorTickResult.None;
    }
}
