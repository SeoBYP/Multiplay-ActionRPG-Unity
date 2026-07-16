using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Server.Diagnostics;

/// <summary>데미지가 흐른 방향. 경로마다 산식 입력이 다르다(combat-diagnostics.md §2.2) — 그 비대칭이 보여야 밸런스 결정이 가능하다.</summary>
public enum CombatPath
{
    PlayerToMonster,
    MonsterToPlayer,
    PlayerToPlayer,
}

/// <summary>발동이 거부된 사유. "왜 공격이 안 나갔나"가 체감 버그의 절반이라 데미지만큼 중요하다.</summary>
public enum CombatGate
{
    UnknownAbility, // 미등록 SkillId(조작된 패킷 포함)
    NoMana,
    ComboCadence,   // 콤보 연타 간격 미달(버스트 차단)
    OnCooldown,
}

/// <summary>
/// 전투 진단 트레이스(AC-C1a). 서버가 <b>무엇을·왜 그 숫자로</b> 판정했는지를 구조적 로그로 남긴다.
/// 설계 = <c>docs/wiki/combat-diagnostics.md</c> §2.
///
/// <para><b>기본 Off.</b> 트레이스는 <c>Debug</c> 레벨로 남기고, 이 서버의 최소 레벨 기본값이 <c>Information</c> 이라 꺼져 있다.
/// Off 면 각 메서드가 첫 줄에서 반환하므로 <b>인자 박싱·문자열 조립이 일어나지 않는다</b> — 상시 로그 금지 요건.</para>
///
/// <para><b>켜는 법 — Serilog 다(⚠ <c>Logging:LogLevel</c> 이 아니다).</b> 이 호스트는 <c>UseSerilog</c> +
/// <c>ReadFrom.Configuration</c> 이라 <c>Serilog:</c> 섹션만 읽는다. MEL 의 <c>Logging:LogLevel</c> 은 **무시된다**.
/// <code>
/// appsettings : "Serilog": { "MinimumLevel": { "Override": { "CombatTrace": "Debug" } } }
/// 환경변수    : Serilog__MinimumLevel__Override__CombatTrace=Debug
/// </code>
/// (Serilog 의 Override 키는 <c>SourceContext</c> = 여기 <see cref="Category"/> 와 일치한다.)</para>
///
/// <para><b>왜 static 인가</b>: 패킷 핸들러(<c>CombatHandler</c>)가 static 이라 DI 가 닿지 않는다.
/// 대신 <see cref="Configure"/> 로 주입 가능하게 두어 테스트가 fake 로거를 꽂을 수 있다.</para>
/// </summary>
public static class CombatTrace
{
    /// <summary>로그 카테고리. appsettings 에서 이 이름으로 레벨을 켠다.</summary>
    public const string Category = "CombatTrace";

    /// <summary>
    /// 스탯 스케일 경로의 산식 표기. 진실원은 <c>Shared.Gameplay/Combat/StatCombatMath.MeleeDamage</c> —
    /// <b>그 구현이 바뀌면 이 문자열도 같이 바꿔야 한다</b>(리뷰 대상). 트레이스가 거짓말하면 진단이 아니라 오도다.
    /// </summary>
    public const string FormulaMelee = "max(1, base+AP-DEF)";

    /// <summary>
    /// 산식을 <b>경유하지 않는</b> 플랫 피해 표기. 플레이어→플레이어가 여기 해당한다(AC-D2 미해결 비대칭).
    /// 이 문자열이 트레이스에 뜨는 것 자체가 "스탯이 반영되지 않았다"는 증거다.
    /// </summary>
    public const string FormulaFlat = "flat(base)";

    private static ILogger _logger = NullLogger.Instance;

    /// <summary>기동 시 1회 배선(Program.cs). 테스트는 fake 로거를 직접 넣는다.</summary>
    public static void Configure(ILogger logger) => _logger = logger ?? NullLogger.Instance;

    /// <summary>켜져 있나. 호출부가 트레이스 전용 계산을 하기 전에 확인할 수 있다.</summary>
    public static bool Enabled => _logger.IsEnabled(LogLevel.Debug);

    /// <summary>
    /// 적중·데미지 1건. <b>어떤 공격이 어떤 공식·입력으로 이 숫자를 냈는지</b>를 전부 싣는다.
    /// </summary>
    /// <param name="recvMs">서버가 발동을 받은 시각(틱 경로는 틱 시각) — 타임라인 축 A.</param>
    /// <param name="judgeMs">판정·데미지 확정 시각. <c>judgeMs - recvMs</c> = 서버 처리 구간.</param>
    /// <param name="seq">대상 몬스터의 상태 버전(AC-C3). 클라 로그와 조인하는 상관키. 대상이 플레이어면 0.</param>
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

    /// <summary>발동 거부 1건. 데미지가 0인 이유를 남긴다.</summary>
    public static void Gate(CombatGate gate, long actorId, int networkId, string abilityId, long nowMs)
    {
        if (!_logger.IsEnabled(LogLevel.Debug)) return;

        _logger.LogDebug(
            "[CombatTrace] gate={Gate} actor={ActorId} ability={AbilityId}({NetworkId}) nowMs={NowMs}",
            gate, actorId, abilityId, networkId, nowMs);
    }
}
