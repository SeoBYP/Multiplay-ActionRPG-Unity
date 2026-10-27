using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Server.Diagnostics;

public enum CombatPath
{
    PlayerToMonster,
    MonsterToPlayer,
    PlayerToPlayer,
}

public enum CombatGate
{
    UnknownAbility,
    NoMana,
    ComboCadence,
    OnCooldown,
    Blocked,
}

// 기본 Off(Debug 레벨). 켜기: Serilog:MinimumLevel:Override:CombatTrace=Debug (Logging:LogLevel 은 무시된다).
public static class CombatTrace
{
    public const string Category = "CombatTrace";

    // 진실원은 Shared.Gameplay/Combat/StatCombatMath.MeleeDamage — 그 구현이 바뀌면 이 문자열도 바꾼다.
    public const string FormulaMelee = "max(1, base+AP-DEF)";

    // 산식을 경유하지 않는 플랫 피해(플레이어→플레이어) — 스탯 미반영.
    public const string FormulaFlat = "flat(base)";

    private static ILogger _logger = NullLogger.Instance;

    public static void Configure(ILogger logger) => _logger = logger ?? NullLogger.Instance;

    public static bool Enabled => _logger.IsEnabled(LogLevel.Debug);

    // recvMs=서버가 발동을 받은 시각, judgeMs=판정 시각, seq=대상 몬스터 상태 버전(클라 로그 조인키; 플레이어 대상이면 0).
    public static void Damage(
        CombatPath path, string formula,
        long actorId, long targetActorId,
        string abilityId, int networkId,
        int baseDamage, int attackPower, int defense, int finalDamage,
        int targetHpBefore, int targetHpAfter,
        long recvMs, long judgeMs, int seq)
    {
        if (!_logger.IsEnabled(LogLevel.Debug)) return;

        _logger.LogDebug(
            "[CombatTrace] dmg path={Path} formula={Formula} actor={ActorId} target={TargetActorId} " +
            "ability={AbilityId}({NetworkId}) base={BaseDamage} ap={AttackPower} def={Defense} final={FinalDamage} " +
            "hp={TargetHpBefore}->{TargetHpAfter} seq={Seq} recvMs={RecvMs} judgeMs={JudgeMs} serverMs={ServerMs}",
            path, formula, actorId, targetActorId,
            abilityId, networkId, baseDamage, attackPower, defense, finalDamage,
            targetHpBefore, targetHpAfter, seq, recvMs, judgeMs, judgeMs - recvMs);
    }

    public static void Gate(CombatGate gate, long actorId, int networkId, string abilityId, long nowMs)
    {
        if (!_logger.IsEnabled(LogLevel.Debug)) return;

        _logger.LogDebug(
            "[CombatTrace] gate={Gate} actor={ActorId} ability={AbilityId}({NetworkId}) nowMs={NowMs}",
            gate, actorId, abilityId, networkId, nowMs);
    }
}
