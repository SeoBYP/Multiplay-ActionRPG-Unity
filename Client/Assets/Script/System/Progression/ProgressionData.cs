namespace Game.System.Progression
{
    /// <summary>
    /// 진행(레벨/경험치) + 레벨 파생 스탯의 도메인 표현(proto에서 변환).
    /// 상위 레이어(Presentation/GUI)가 proto(GetProgressionResponse)에 의존하지 않도록 한다.
    /// 스탯은 서버가 레벨 테이블에서 룩업해 내려준 값(클라는 그대로 표시만).
    /// </summary>
    public readonly struct ProgressionData
    {
        public readonly int Level;
        public readonly long Exp;
        public readonly long ExpToNext;   // 다음 레벨까지 필요 Exp. 만렙이면 0.
        public readonly int MaxLevel;
        public readonly ProgressionStats Stats;

        public ProgressionData(int level, long exp, long expToNext, int maxLevel, ProgressionStats stats)
        {
            Level = level;
            Exp = exp;
            ExpToNext = expToNext;
            MaxLevel = maxLevel;
            Stats = stats;
        }

        public bool IsMaxLevel => ExpToNext <= 0;

        /// <summary>현재 레벨 진척률 0~1 (만렙이면 1).</summary>
        public float ExpProgress
            => IsMaxLevel ? 1f : (ExpToNext > 0 ? (float)((double)Exp / ExpToNext) : 0f);
    }

    /// <summary>레벨에서 파생된 base 스탯(서버 권위 룩업값).</summary>
    public readonly struct ProgressionStats
    {
        public readonly int MaxHealth;
        public readonly int MaxMana;
        public readonly int AttackPower;
        public readonly int Defense;
        public readonly int Strength;
        public readonly int Dexterity;
        public readonly int Intelligence;

        public ProgressionStats(int maxHealth, int maxMana, int attackPower, int defense,
            int strength, int dexterity, int intelligence)
        {
            MaxHealth = maxHealth;
            MaxMana = maxMana;
            AttackPower = attackPower;
            Defense = defense;
            Strength = strength;
            Dexterity = dexterity;
            Intelligence = intelligence;
        }
    }
}
