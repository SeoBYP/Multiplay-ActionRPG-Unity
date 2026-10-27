using System;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 스탯 기반 데미지 산식(결정론, 순수). 서버 권위 전투가 쓰고, 클라가 동일 산식으로 플로팅 데미지를 예측한다.
    /// baseDamage = 카탈로그 effect 의 Health 감소량, attackPower/defense = 서버 권위 합산 스탯.
    /// 최소 1 보장(방어가 공격을 넘어도 무피해가 되지 않게).
    /// </summary>
    public static class StatCombatMath
    {
        /// <summary>근접 데미지 = max(1, baseDamage + attackPower - defense).</summary>
        public static int MeleeDamage(int baseDamage, int attackPower, int defense)
            => Math.Max(1, baseDamage + attackPower - defense);
    }
}
