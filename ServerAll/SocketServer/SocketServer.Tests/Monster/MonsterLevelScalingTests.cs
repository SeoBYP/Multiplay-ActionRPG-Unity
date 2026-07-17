using Shared.Infrastructure.Monsters;

namespace Server.Tests.Monster;

/// <summary>
/// AC-E1: 몬스터 레벨·등급 스케일. 설계 = docs/wiki/monster-leveling.md §2.
///
/// 이 공식이 존재하는 이유는 <b>역할 보존</b>이다 — 레벨이 올라도 bat 은 긁는 피해, slam 은 치명타여야 한다.
/// 곱셈 스케일은 slam 을 폭발시키고, 단순 가산은 전부 중간으로 수렴시킨다. 그 둘이 아님을 여기서 고정한다.
/// </summary>
public class MonsterLevelScalingTests
{
    // 플레이어 곡선(level-table.json) — 검증 기준.
    private static int PlayerDef(int level) => 5 + 2 * (level - 1);
    private static int PlayerHp(int level) => 100 + 20 * (level - 1);

    [Theory]
    [InlineData(8)]    // vampire_bat  — 가장 약함
    [InlineData(14)]   // arachnya
    [InlineData(40)]   // leviathan_attack
    [InlineData(90)]   // leviathan_slam — 가장 강함
    public void 레벨이_올라도_역할_비중이_보존된다(int base1)
    {
        // 순피해 / 플레이어 HP 비율이 레벨 전반에서 유지돼야 한다 = "체감 난이도 불변".
        float ratioAtL1 = (base1 - PlayerDef(1)) / (float)PlayerHp(1);

        foreach (int lv in new[] { 6, 12, 19, 30, 60 })
        {
            int dmg = MonsterLevelScaling.Damage(base1, lv);
            float net = dmg - PlayerDef(lv);
            float ratio = net / PlayerHp(lv);

            Assert.True(Math.Abs(ratio - ratioAtL1) < 0.01f,
                $"base₁={base1} L{lv}: 비중 {ratio:P1} 이 L1 의 {ratioAtL1:P1} 에서 벗어났다(역할 붕괴)");
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(14)]
    [InlineData(90)]
    public void 어떤_레벨에서도_피해가_바닥에_눌리지_않는다(int base1)
    {
        // 원래 버그: base 고정 + DEF +2/L → L19 부터 전부 max(1,..) 바닥.
        // 증가폭 하한이 DEF 성장(2)이라 구조적으로 재발할 수 없다.
        for (int lv = 1; lv <= 60; lv++)
        {
            int dmg = MonsterLevelScaling.Damage(base1, lv);
            int net = dmg - PlayerDef(lv);

            Assert.True(net >= 2,
                $"base₁={base1} L{lv}: 순피해 {net} — 바닥에 눌렸다(레벨링 이전의 그 버그)");
        }
    }

    [Fact]
    public void 설계표의_검산값과_일치한다()
    {
        // monster-leveling.md §2 검산표를 그대로 고정 — 문서와 코드가 어긋나면 여기서 터진다.
        Assert.Equal(21, MonsterLevelScaling.Damage(8, 6));   // bat:      8 + 2.6×5  = 21
        Assert.Equal(33, MonsterLevelScaling.Damage(14, 6));  // arachnya: 14 + 3.8×5 = 33
        Assert.Equal(185, MonsterLevelScaling.Damage(90, 6)); // slam:     90 + 19×5  = 185
    }

    [Fact]
    public void L1은_저작값_그대로다()
    {
        // 레벨 도입이 기존 밸런스를 바꾸면 안 된다 — L1 은 항등.
        Assert.Equal(14, MonsterLevelScaling.Damage(14, 1));
        Assert.Equal(65, MonsterLevelScaling.Hp(65, 1));
        Assert.Equal(30, MonsterLevelScaling.Exp(30, 1));
    }

    [Theory]
    [InlineData(0)]   // 미저작
    [InlineData(-5)]  // 잘못된 데이터
    public void 미저작_레벨은_L1로_본다(int level)
    {
        // 데이터 누락이 스탯 붕괴(0 피해·0 HP)로 번지지 않게.
        Assert.Equal(MonsterLevelScaling.Damage(14, 1), MonsterLevelScaling.Damage(14, level));
        Assert.Equal(MonsterLevelScaling.Hp(65, 1), MonsterLevelScaling.Hp(65, level));
    }

    [Fact]
    public void HP는_플레이어_공격력_성장에_맞춰_커진다()
    {
        // 킬 타임 유지: 플레이어 AP 가 L6 에 2.5배(10→25) → 몬스터 HP 도 2.5배.
        Assert.Equal(65, MonsterLevelScaling.Hp(65, 1));
        Assert.Equal((int)MathF.Round(65 * 2.5f), MonsterLevelScaling.Hp(65, 6));
    }

    [Fact]
    public void 보상없는_몬스터는_스케일해도_0이다()
    {
        // test_brute 처럼 ExpReward=0 인 픽스처가 레벨 때문에 갑자기 보상을 주면 안 된다.
        Assert.Equal(0, MonsterLevelScaling.Exp(0, 30));
    }

    [Fact]
    public void 드롭_수량은_레벨에_비례한다()
    {
        // 등급 확률 배율은 없앴다(AC-G) — 변종이 자기 ID 의 드롭 테이블을 갖는다.
        Assert.Equal(1f, MonsterLevelScaling.DropQuantityMultiplier(1));
        Assert.Equal(2f, MonsterLevelScaling.DropQuantityMultiplier(6));   // HP(6)/HP(1) = 200/100
    }
}
