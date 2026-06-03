using System.Collections.Generic;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// Stat 집계의 순수 함수. **정수 기반** — float drift를 피해 클라·서버가 동일 결과를 낸다.
    /// 클라이언트 미러(Script.System.GamePlayAbilitySystem.GameplayEffectMath)와 동일 알고리즘.
    /// </summary>
    public static class GameplayEffectMath
    {
        private const long PercentBase = 10000; // 100.00%

        /// <summary>Current = Clamp( (Base + Σ Additive) × Π(pct/100) , 0, Max )</summary>
        public static int Aggregate(int baseValue, IEnumerable<GameplayAttributeModifier> modifiers, int max)
        {
            int additive = 0;
            long pct = PercentBase;

            foreach (var m in modifiers)
            {
                switch (m.ModifierType)
                {
                    case EModifierType.Additive:
                        additive += m.Amount;
                        break;
                    case EModifierType.Multiplicative:
                        pct = pct * m.Amount / 100;
                        break;
                }
            }

            long value = (long)(baseValue + additive) * pct / PercentBase;
            if (value < 0) value = 0;
            if (value > max) value = max;
            return (int)value;
        }
    }
}
