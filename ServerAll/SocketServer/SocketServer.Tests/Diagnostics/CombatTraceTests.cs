using Microsoft.Extensions.Logging;
using Server.Diagnostics;

namespace Server.Tests.Diagnostics;

/// <summary>
/// AC-C1a: 전투 트레이스. 핵심 계약 2가지 —
/// ① **기본 Off**(상시 로그 금지): 꺼져 있으면 로그 호출이 아예 없어야 한다(인자 박싱·문자열 조립 0).
/// ② 켜면 판정 근거(경로·산식·base/AP/DEF·final)가 **구조적 필드**로 남아야 한다.
/// </summary>
public class CombatTraceTests
{
    /// <summary>IsEnabled 응답을 조작하고 Log 호출을 세는 최소 fake. (트레이스는 static 이라 Configure 로 주입)</summary>
    private sealed class FakeLogger : ILogger
    {
        private readonly bool _enabled;
        public int LogCalls;
        public int IsEnabledCalls;
        public string? LastMessage;

        public FakeLogger(bool enabled) => _enabled = enabled;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            IsEnabledCalls++;
            return _enabled;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogCalls++;
            LastMessage = formatter(state, exception);
        }
    }

    private static FakeLogger Install(bool enabled)
    {
        var fake = new FakeLogger(enabled);
        CombatTrace.Configure(fake);
        return fake;
    }

    private static void EmitDamage() => CombatTrace.Damage(
        CombatPath.PlayerToMonster, CombatTrace.FormulaMelee,
        actorId: 100, targetActorId: -7,
        abilityId: "basic_swing", networkId: 0,
        baseDamage: 10, attackPower: 5, defense: 0, finalDamage: 15,
        targetHpBefore: 30, targetHpAfter: 15,
        recvMs: 1_000, judgeMs: 1_003, seq: 4);

    [Fact]
    public void Off면_로그를_호출하지_않는다_상시로그_금지()
    {
        var fake = Install(enabled: false);

        EmitDamage();
        CombatTrace.Gate(CombatGate.OnCooldown, actorId: 100, networkId: 0, abilityId: "basic_swing", nowMs: 1_000);

        // Off 면 IsEnabled 로 즉시 반환 → Log 는 0 건(= 인자 조립 비용도 발생하지 않는다).
        Assert.Equal(0, fake.LogCalls);
        Assert.False(CombatTrace.Enabled);
    }

    [Fact]
    public void On이면_판정_근거가_기록된다()
    {
        var fake = Install(enabled: true);

        EmitDamage();

        Assert.Equal(1, fake.LogCalls);
        Assert.True(CombatTrace.Enabled);

        // "어떤 공격이 어떤 공식으로 이 숫자를 냈나" 가 그대로 읽혀야 한다.
        var msg = fake.LastMessage!;
        Assert.Contains("PlayerToMonster", msg);
        Assert.Contains("max(1, base+AP-DEF)", msg);
        Assert.Contains("basic_swing", msg);
        Assert.Contains("base=10", msg);
        Assert.Contains("ap=5", msg);
        Assert.Contains("final=15", msg);
        Assert.Contains("hp=30->15", msg);
        Assert.Contains("seq=4", msg);   // 클라 로그와 조인하는 상관키(AC-C3)
        Assert.Contains("serverMs=3", msg); // judge - recv = 서버 처리 구간(타임라인 축 A)
    }

    [Fact]
    public void Gate는_발동_거부_사유를_남긴다()
    {
        var fake = Install(enabled: true);

        CombatTrace.Gate(CombatGate.NoMana, actorId: 100, networkId: 1, abilityId: "heavy_swing", nowMs: 2_000);

        Assert.Equal(1, fake.LogCalls);
        Assert.Contains("gate=NoMana", fake.LastMessage!);
        Assert.Contains("heavy_swing", fake.LastMessage!);
    }

    [Fact]
    public void 플레이어간_공격은_산식_미경유가_표기로_드러난다_AC_D2()
    {
        // 이 트레이스의 존재 이유 중 하나: P→P 만 스탯 스케일을 안 탄다는 **비대칭이 데이터로 보이게** 하는 것.
        var fake = Install(enabled: true);

        CombatTrace.Damage(
            CombatPath.PlayerToPlayer, CombatTrace.FormulaFlat,
            actorId: 100, targetActorId: 200,
            abilityId: "basic_swing", networkId: 0,
            baseDamage: 10, attackPower: 0, defense: 0, finalDamage: 10,
            targetHpBefore: 0, targetHpAfter: 0,
            recvMs: 1_000, judgeMs: 1_000, seq: 0);

        Assert.Contains("flat(base)", fake.LastMessage!);
        Assert.Contains("PlayerToPlayer", fake.LastMessage!);
        Assert.DoesNotContain("max(1,", fake.LastMessage!); // 산식을 경유했다고 오독되면 안 된다
    }
}
