using System;
using System.Collections.Generic;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 한 액터의 GAS 상태 — <b>속성 · 태그 · 쿨다운을 한 곳에서</b> 보유한다. 종족(플레이어/몬스터)을 모른다.
    ///
    /// <para><b>왜 Actor 에서 뺐나</b>: Actor 는 "무엇이 존재하고 어디 있는가"(신원·공간·수명)를 맡고,
    /// "그것이 전투적으로 어떤 상태인가"는 여기가 맡는다. 한 클래스가 둘 다 하면 새 능력치·새 태그를
    /// 넣을 때마다 Actor 가 부풀어 결국 예전 PlayerState 처럼 다시 겸직 클래스가 된다.</para>
    ///
    /// <para><b>왜 Shared 인가</b>: 클라 <c>AbilitySystemComponent</c> 는 MonoBehaviour 라 서버가 못 쓰지만,
    /// 그 알맹이(속성·태그·집계 산식)는 엔진에 의존하지 않는다. 여기에 두면 서버 Actor 와 클라가
    /// <b>같은 타입·같은 산식</b>을 쓴다 — 서버 HP == 클라 HP 가 구조로 보장된다.</para>
    ///
    /// <para><b>스스로 스레드 안전하다.</b> 서버에서 이 상태는 <b>두 스레드</b>가 만진다 —
    /// 틱 루프(마나 회복·피해)와 패킷 핸들러(마나 차감·쿨다운·회피). 예전에는 방/저장소 락에 의존해
    /// <b>저장소를 지나는 경로만</b> 안전했고 핸들러가 Gas 를 직접 만지는 경로는 구멍이었다
    /// (실제로 마나가 그 구멍으로 샜다). 경계를 여기로 내려 <b>어느 경로로 오든</b> 안전하게 만든다.</para>
    ///
    /// <para>속성 저장소와 태그 집합은 <b>private</b> 이다 — 밖에 노출하면 락을 우회할 수 있다.
    /// 접근은 인덱서 하나(<c>gas[attr]</c>)로 한다. 속성이 늘어도 <b>멤버를 추가하지 않는다</b>.</para>
    /// </summary>
    public sealed class GasComponent
    {
        /// <summary>
        /// 여러 연산을 하나의 원자 단위로 묶어야 할 때 호출자가 잡는 락(읽고-판단하고-쓰기).
        /// 단일 연산은 이 클래스가 알아서 잡는다. Monitor 는 재진입 가능해 중첩 호출이 안전하다.
        /// </summary>
        public object SyncRoot { get; } = new object();

        /// <summary>보유 속성(current/max). <b>없는 속성은 0 이 아니라 "없음"</b>이다.</summary>
        private readonly AttributeSet _attributes = new AttributeSet();

        /// <summary>상태 태그(사망·스턴·무적 …).</summary>
        private readonly GameplayTagContainer _tags = new GameplayTagContainer();

        // ── 속성 부여 ───────────────────────────────────────────────────

        /// <summary>자원 속성 부여(HP·마나 — 상한이 있고 만땅으로 시작). max&lt;=0 이면 부여하지 않는다(= 미보유).</summary>
        public void DefineResource(EGameplayAttribute attribute, int max)
        {
            if (max <= 0) return;
            lock (SyncRoot) _attributes.Define(attribute, max, max);
        }

        /// <summary>
        /// 스탯 속성 부여(공격력·방어력 — 상한 없음). 버프가 base 를 넘을 수 있어야 하므로 클램프하지 않는다.
        /// <b>0 을 넣어도 부여한다</b> — "0 인 스탯"과 "스탯이 없음"은 다르고, 그 구분이 이 설계의 요점이다.
        /// </summary>
        public void DefineStat(EGameplayAttribute attribute, int value)
        {
            lock (SyncRoot) _attributes.Define(attribute, value, AttributeSet.NoMax);
        }

        // ── 속성 접근 (멤버는 인덱서 하나 — 속성이 늘어도 여기는 안 늘어난다) ──

        /// <summary>현재값. 읽기는 미보유 시 0, 쓰기는 미보유 시 무동작.</summary>
        public int this[EGameplayAttribute attribute]
        {
            get { lock (SyncRoot) return _attributes.GetOr(attribute); }
            set { lock (SyncRoot) _attributes.SetCurrent(attribute, value); }
        }

        /// <summary>상한값. 미보유면 0.</summary>
        public int Max(EGameplayAttribute attribute)
        {
            lock (SyncRoot) return _attributes.MaxOr(attribute);
        }

        /// <summary>이 액터가 그 속성을 보유하는가. <b>false = 0 이 아니라 "없음"</b>.</summary>
        public bool Has(EGameplayAttribute attribute)
        {
            lock (SyncRoot) return _attributes.Has(attribute);
        }

        /// <summary>사망(HP 소진). 모든 액터가 Health 를 갖는다는 전제 위의 <b>도메인 술어</b>다.</summary>
        public bool IsDead
        {
            get { lock (SyncRoot) return _attributes.GetOr(EGameplayAttribute.Health) <= 0; }
        }

        // ── 태그 ────────────────────────────────────────────────────────

        /// <summary>태그 부여. 반환 = <b>이번에 새로 붙었는가</b>(중복 발화 dedup 에 그대로 쓴다).</summary>
        public bool AddTag(GameplayTag tag)
        {
            lock (SyncRoot) return _tags.Add(tag);
        }

        /// <summary>태그 제거. 반환 = <b>실제로 있었는가</b>(멱등 가드에 그대로 쓴다).</summary>
        public bool RemoveTag(GameplayTag tag)
        {
            lock (SyncRoot) return _tags.Remove(tag);
        }

        public bool HasTag(GameplayTag tag)
        {
            lock (SyncRoot) return _tags.HasTag(tag);
        }

        /// <summary>
        /// 어빌리티 발동이 태그로 차단되는가. <see cref="AbilityActivationMath"/> 의 blocked 인자에 그대로 넣는다 —
        /// 예전의 하드코딩 blocked:false 를 대체하는 자리.
        /// </summary>
        public bool IsActivationBlocked
        {
            get { lock (SyncRoot) return _tags.HasTag(GameplayTags.Dead) || _tags.HasTag(GameplayTags.Stun); }
        }

        // ── 쿨다운 (구 PlayerState._lastSkillCastMs + MonsterState._lastCastByAbility 통합) ──
        // 키는 abilityId(문자열). 둘 다 결국 같은 AbilityActivationMath 에 먹이는 값이라 저장소가 갈릴 이유가 없었다.

        private readonly Dictionary<string, long> _lastCastMs = new Dictionary<string, long>();

        /// <summary>이 어빌리티의 마지막 발동 시각. 미발동은 0(=쿨다운 통과).</summary>
        public long LastCast(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return 0L;
            lock (SyncRoot) return _lastCastMs.TryGetValue(abilityId, out var t) ? t : 0L;
        }

        /// <summary>발동 확정 시 기록(쿨다운 시작).</summary>
        public void MarkCast(string abilityId, long nowMs)
        {
            if (string.IsNullOrEmpty(abilityId)) return;
            lock (SyncRoot) _lastCastMs[abilityId] = nowMs;
        }

        /// <summary>
        /// 쿨다운 게이트 + 커밋. 지났으면 기록하고 true, 아니면 <b>아무것도 기록하지 않고</b> false.
        /// 판정과 기록이 <b>한 락 안</b>이라 동시 요청 둘이 함께 통과하지 못한다(연사 치팅 차단의 핵심).
        /// </summary>
        public bool TryBeginAbility(string abilityId, int cooldownMs, long nowMs)
        {
            if (string.IsNullOrEmpty(abilityId)) return false;
            lock (SyncRoot)
            {
                long last = _lastCastMs.TryGetValue(abilityId, out var t) ? t : 0L;
                if (!SkillTimelineMath.CooldownElapsed(cooldownMs, last, nowMs))
                    return false;

                _lastCastMs[abilityId] = nowMs;
                return true;
            }
        }

        // ── 자원 소비/회복 ──────────────────────────────────────────────

        /// <summary>
        /// 마나 차감. 충분하면 차감하고 true, 부족하면 변경 없이 false. cost&lt;=0 은 무료(항상 true).
        /// 검사와 차감이 <b>한 락 안</b>이라 동시 차감으로 마나가 음수가 되거나 두 번 쓰이지 않는다.
        /// </summary>
        public bool TrySpendMana(int cost)
        {
            if (cost <= 0) return true;
            lock (SyncRoot)
            {
                int mana = _attributes.GetOr(EGameplayAttribute.Mana);
                if (mana < cost) return false;
                _attributes.SetCurrent(EGameplayAttribute.Mana, mana - cost);
                return true;
            }
        }

        private double _manaRegenAccum;

        /// <summary>
        /// 시간 비례 마나 자연 회복. 소수부를 누적해 정수 단위로 더하고 MaxMana 로 클램프.
        /// 클라도 같은 rate 로 예측하므로 동기화 패킷 없이 수렴한다.
        /// </summary>
        public void RegenMana(float dt)
        {
            lock (SyncRoot)
            {
                int mana = _attributes.GetOr(EGameplayAttribute.Mana);
                int maxMana = _attributes.MaxOr(EGameplayAttribute.Mana);
                if (maxMana <= 0 || mana >= maxMana)
                {
                    _manaRegenAccum = 0;
                    return;
                }

                _manaRegenAccum += ManaConfig.RegenPerSecond * dt;
                int whole = (int)_manaRegenAccum;
                if (whole <= 0) return;

                _manaRegenAccum -= whole;
                _attributes.SetCurrent(EGameplayAttribute.Mana, Math.Min(maxMana, mana + whole));
            }
        }

        // ── 효과 적용 ───────────────────────────────────────────────────

        /// <summary>
        /// 모디파이어를 <b>보유한 속성에만</b> 적용한다(미보유 대상 모디파이어는 무시 — 몬스터에 마나가 몰래 생기지 않는다).
        /// 속성별로 모아 <see cref="GameplayEffectMath.Aggregate"/>(클라와 동일 산식)로 집계하므로 서버·클라 값이 어긋나지 않는다.
        ///
        /// <para><b>즉발(Instant) 의미로만 적용한다</b> — 적용 즉시 현재값을 바꾸고 되돌리지 않는다.
        /// Duration 스탯 버프(예: atk_up_20)를 이 경로로 넣으면 <b>영구 버프가 된다</b>.
        /// 지속효과를 서버가 소유하려면 활성 Effect 추적 + 만료 틱이 필요하고, 그건 별도 증분이다.</para>
        /// </summary>
        public void ApplyModifiers(IReadOnlyList<GameplayAttributeModifier> mods)
        {
            if (mods == null || mods.Count == 0)
                return;

            // 대상 속성을 먼저 모은다(대개 1개 — Health). 같은 속성의 모디파이어는 한 번에 집계해야
            // Additive/Multiplicative 순서 규칙(GameplayEffectMath)이 그대로 성립한다.
            var touched = new HashSet<EGameplayAttribute>();
            for (int i = 0; i < mods.Count; i++)
                touched.Add(mods[i].AttributeType);

            lock (SyncRoot)
            {
                foreach (var attribute in touched)
                {
                    if (!_attributes.TryGet(attribute, out int current))
                        continue; // 이 액터에겐 존재하지 않는 속성

                    int next = GameplayEffectMath.Aggregate(
                        current, Filter(mods, attribute), _attributes.MaxOr(attribute));
                    _attributes.SetCurrent(attribute, next);
                }
            }
        }

        private static IEnumerable<GameplayAttributeModifier> Filter(
            IReadOnlyList<GameplayAttributeModifier> mods, EGameplayAttribute attribute)
        {
            for (int i = 0; i < mods.Count; i++)
                if (mods[i].AttributeType == attribute)
                    yield return mods[i];
        }
    }
}
