using System.Collections.Generic;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 한 액터가 <b>실제로 보유한</b> 속성만 담는 집합(current/max 쌍).
    ///
    /// <para><b>왜 필드가 아니라 집합인가</b>: "이 액터에 그 속성이 없다"를 <b>데이터로</b> 표현하기 위해서다.
    /// 필드로 두면 몬스터에 없는 AttackPower/Defense/Mana 를 호출부가 리터럴 0 으로 위장하게 되고
    /// (실제로 <c>const int MonsterAttackPower = 0</c>·<c>const int MonsterDefense = 0</c> 이 그렇게 생겼다),
    /// 산식은 그 위장을 진짜 값으로 착각한다. 여기서는 <see cref="Has"/> 로 유무를 묻는다.</para>
    ///
    /// <para>스탯이 늘어도 <see cref="EGameplayAttribute"/> 에 값 하나만 추가하면 된다 —
    /// 저장소·적용 경로는 그대로다.</para>
    /// </summary>
    public sealed class AttributeSet
    {
        /// <summary>상한이 의미 없는 속성(스탯)의 Max. 버프가 base 를 넘을 수 있어야 하므로 클램프하지 않는다.</summary>
        public const int NoMax = int.MaxValue;

        private struct Entry
        {
            public int Current;
            public int Max;

            /// <summary>버프 적용 <b>전</b> 원본. 스탯 재계산은 항상 여기서 다시 시작한다.</summary>
            public int Base;

            /// <summary>
            /// 스탯인가(공격력·방어력 …). true 면 활성 Effect 로부터 <b>파생</b>되는 값이라
            /// 지속효과가 붙고 떨어질 때마다 Base 기준으로 재계산된다.
            /// false = 자원(HP·마나) — 소비·회복으로 <b>스스로 변하는</b> 값이라 재계산 대상이 아니다.
            /// </summary>
            public bool IsStat;
        }

        private readonly Dictionary<EGameplayAttribute, Entry> _map = new Dictionary<EGameplayAttribute, Entry>();

        /// <summary>
        /// 현재값 접근. 읽기는 미보유 시 0, 쓰기는 미보유 시 무동작.
        /// <b>속성별 프로퍼티를 만들지 않는다</b> — 스탯이 늘 때 <see cref="EGameplayAttribute"/> 에만 값을 추가하면 되도록.
        /// </summary>
        public int this[EGameplayAttribute attribute]
        {
            get => GetOr(attribute);
            set => SetCurrent(attribute, value);
        }

        /// <summary>이 액터가 그 속성을 보유하는가. <b>false = 0 이 아니라 "없음"</b>.</summary>
        public bool Has(EGameplayAttribute attribute) => _map.ContainsKey(attribute);

        /// <summary>보유 속성 목록(적용 대상 판별·디버그용).</summary>
        public IEnumerable<EGameplayAttribute> Defined => _map.Keys;

        /// <summary>
        /// 속성을 부여한다. current 는 [0, max] 로 클램프되고 그 값이 Base 가 된다. 이미 있으면 덮어쓴다.
        /// <paramref name="isStat"/> = 활성 Effect 로부터 파생되는 값인가(재계산 대상인가).
        /// </summary>
        public void Define(EGameplayAttribute attribute, int current, int max, bool isStat = false)
        {
            int clamped = Clamp(current, max);
            _map[attribute] = new Entry { Current = clamped, Max = max, Base = clamped, IsStat = isStat };
        }

        /// <summary>스탯 재계산의 기준값(버프 적용 전 원본). 미보유면 <paramref name="fallback"/>.</summary>
        public int BaseOr(EGameplayAttribute attribute, int fallback = 0)
            => _map.TryGetValue(attribute, out var e) ? e.Base : fallback;

        /// <summary>재계산 대상(스탯)만 열거한다. 자원(HP·마나)은 나오지 않는다.</summary>
        public IEnumerable<EGameplayAttribute> Stats
        {
            get
            {
                foreach (var kv in _map)
                    if (kv.Value.IsStat)
                        yield return kv.Key;
            }
        }

        /// <summary>보유 시 현재값을 내고 true. 미보유면 false(out 0).</summary>
        public bool TryGet(EGameplayAttribute attribute, out int current)
        {
            if (_map.TryGetValue(attribute, out var e))
            {
                current = e.Current;
                return true;
            }

            current = 0;
            return false;
        }

        /// <summary>현재값. 미보유면 <paramref name="fallback"/>.</summary>
        public int GetOr(EGameplayAttribute attribute, int fallback = 0)
            => _map.TryGetValue(attribute, out var e) ? e.Current : fallback;

        /// <summary>상한값. 미보유면 <paramref name="fallback"/>.</summary>
        public int MaxOr(EGameplayAttribute attribute, int fallback = 0)
            => _map.TryGetValue(attribute, out var e) ? e.Max : fallback;

        /// <summary>현재값 설정([0, Max] 클램프). <b>미보유 속성엔 무동작</b> — 없는 속성이 몰래 생기지 않게.</summary>
        public void SetCurrent(EGameplayAttribute attribute, int value)
        {
            if (!_map.TryGetValue(attribute, out var e))
                return;

            e.Current = Clamp(value, e.Max);
            _map[attribute] = e;
        }

        /// <summary>상한 변경(현재값은 새 상한으로 재클램프). 미보유면 무동작.</summary>
        public void SetMax(EGameplayAttribute attribute, int max)
        {
            if (!_map.TryGetValue(attribute, out var e))
                return;

            e.Max = max;
            e.Base = Clamp(e.Base, max);
            e.Current = Clamp(e.Current, max);
            _map[attribute] = e;
        }

        private static int Clamp(int value, int max)
        {
            if (value < 0) return 0;
            if (value > max) return max;
            return value;
        }
    }
}
