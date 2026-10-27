using System;
using System.Collections.Generic;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 한 액터의 GAS 상태(속성·태그·쿨다운·활성 Effect). 종족을 모른다.
    /// Shared 라 서버와 클라(<c>GasComponent</c> 가 소유)가 같은 산식을 쓴다 — 서버 HP == 클라 HP.
    /// <para><b>스스로 스레드 안전하다</b> — 서버에선 틱 루프와 패킷 핸들러가 동시에 만진다.
    /// 그래서 저장소는 private 이고 접근은 인덱서(<c>gas[attr]</c>)로만 한다(노출하면 락 우회).</para>
    /// </summary>
    public sealed class AbilitySystemComponent
    {
        /// <summary>읽고-판단하고-쓰기를 원자로 묶을 때 호출자가 잡는 락. 재진입 가능.</summary>
        public object SyncRoot { get; } = new object();

        /// <summary>보유 속성(current/max). <b>없는 속성은 0 이 아니라 "없음"</b>이다.</summary>
        private readonly AttributeSet _attributes = new AttributeSet();

        private readonly GameplayTagContainer _tags = new GameplayTagContainer();

        // ── 속성 부여 ───────────────────────────────────────────────────

        /// <summary>자원 속성(HP·마나) 부여, 가득 찬 상태로 시작. max&lt;=0 이면 부여하지 않는다.</summary>
        public void DefineResource(EGameplayAttribute attribute, int max) => DefineResource(attribute, max, max);

        /// <summary>시작 현재값과 함께 부여(프리팹에 "HP 30/100" 처럼 저작된 경우).</summary>
        public void DefineResource(EGameplayAttribute attribute, int current, int max)
        {
            if (max <= 0) return;
            lock (SyncRoot) _attributes.Define(attribute, current, max);
        }

        /// <summary>스탯 속성 부여(상한 없음 — 버프가 base 를 넘을 수 있다). 0 이어도 부여한다("0" ≠ "없음").</summary>
        public void DefineStat(EGameplayAttribute attribute, int value)
        {
            lock (SyncRoot) _attributes.Define(attribute, value, AttributeSet.NoMax, isStat: true);
        }

        // ── 속성 접근 ───────────────────────────────────────────────────

        /// <summary>현재값. 읽기는 미보유 시 0, 쓰기는 미보유 시 무동작.</summary>
        public int this[EGameplayAttribute attribute]
        {
            get { lock (SyncRoot) return _attributes.GetOr(attribute); }
            set { lock (SyncRoot) _attributes.SetCurrent(attribute, value); }
        }

        /// <summary>상한 변경(현재값·Base 재클램프). 미보유면 무동작. 풀충전은 호출 측이 한다.</summary>
        public void SetMax(EGameplayAttribute attribute, int max)
        {
            lock (SyncRoot) _attributes.SetMax(attribute, max);
        }

        /// <summary>상한값. 미보유면 0.</summary>
        public int Max(EGameplayAttribute attribute)
        {
            lock (SyncRoot) return _attributes.MaxOr(attribute);
        }

        /// <summary>보유 속성 목록 스냅샷(복사본).</summary>
        public IReadOnlyList<EGameplayAttribute> DefinedAttributes()
        {
            lock (SyncRoot)
            {
                var list = new List<EGameplayAttribute>();
                foreach (var attribute in _attributes.Defined)
                    list.Add(attribute);
                return list;
            }
        }

        public bool Has(EGameplayAttribute attribute)
        {
            lock (SyncRoot) return _attributes.Has(attribute);
        }

        /// <summary>HP 소진. 모든 액터가 Health 를 갖는다는 전제.</summary>
        public bool IsDead
        {
            get { lock (SyncRoot) return _attributes.GetOr(EGameplayAttribute.Health) <= 0; }
        }

        // ── 태그 ────────────────────────────────────────────────────────

        /// <summary>반환 = 이번에 새로 붙었는가(중복 발화 dedup 용).</summary>
        public bool AddTag(GameplayTag tag)
        {
            lock (SyncRoot) return _tags.Add(tag);
        }

        /// <summary>반환 = 실제로 있었는가(멱등 가드용).</summary>
        public bool RemoveTag(GameplayTag tag)
        {
            lock (SyncRoot) return _tags.Remove(tag);
        }

        /// <summary>
        /// 직접 부여한 태그 ∪ 활성 Effect 의 태그. Effect 태그를 컨테이너에 복사하지 않는다 —
        /// 복사하면 두 Effect 가 같은 스턴을 줄 때 회수 장부가 어긋나 영구 스턴이 된다. 파생값이면 만료 = 해제.
        /// </summary>
        public bool HasTag(GameplayTag tag)
        {
            lock (SyncRoot) return HasTagLocked(tag);
        }

        private bool HasTagLocked(GameplayTag tag)
        {
            if (_tags.HasTag(tag))
                return true;

            for (int i = 0; i < _active.Count; i++)
            {
                var granted = _active[i].Definition.GrantedTags;
                for (int g = 0; g < granted.Count; g++)
                    if (granted[g] == tag)
                        return true;
            }

            return false;
        }

        /// <summary>사망·스턴으로 발동 차단. <see cref="AbilityActivationMath"/> 의 blocked 인자.</summary>
        public bool IsActivationBlocked
        {
            get { lock (SyncRoot) return HasTagLocked(GameplayTags.Dead) || HasTagLocked(GameplayTags.Stun); }
        }

        // ── 쿨다운 (키 = abilityId, 플레이어·몬스터 공용) ──────────────────

        private readonly Dictionary<string, long> _lastCastMs = new Dictionary<string, long>();

        private readonly HashSet<string> _granted = new HashSet<string>();

        public void GrantAbility(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return;
            lock (SyncRoot) _granted.Add(abilityId);
        }
        
        public bool HasAbility(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return false;
            lock (SyncRoot) return _granted.Contains(abilityId);
        }
        
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
        /// 쿨다운 게이트 + 커밋. 지났으면 기록하고 true, 아니면 기록 없이 false.
        /// 판정과 기록이 한 락 안이라 동시 요청 둘이 함께 통과하지 못한다(연사 치팅 차단).
        /// </summary>
        public bool TryBeginAbility(string abilityId, int cooldownMs, long nowMs)
        {
            if (string.IsNullOrEmpty(abilityId)) return false;
            lock (SyncRoot)
            {
                long last = _lastCastMs.GetValueOrDefault(abilityId, 0L);
                if (!SkillTimelineMath.CooldownElapsed(cooldownMs, last, nowMs))
                    return false;

                _lastCastMs[abilityId] = nowMs;
                return true;
            }
        }
        


        // ── 자원 소비/회복 ──────────────────────────────────────────────

        /// <summary>
        /// 충분하면 차감하고 true, 부족하면 변경 없이 false. cost&lt;=0 은 항상 true.
        /// 검사와 차감이 한 락 안이라 동시 차감으로 음수가 되지 않는다.
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

        /// <summary>마나 자연 회복(소수부 누적). 클라도 같은 rate 로 예측해 동기화 패킷 없이 수렴한다.</summary>
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

        // ── 활성 Effect (지속시간 · 스택 · 만료 — 만료 권위는 서버) ─────────

        private readonly List<ActiveGameplayEffect> _active = new List<ActiveGameplayEffect>();

        public int ActiveEffectCount
        {
            get { lock (SyncRoot) return _active.Count; }
        }

        /// <summary>활성 Effect 스냅샷(복사본).</summary>
        public IReadOnlyList<ActiveGameplayEffect> ActiveEffects()
        {
            lock (SyncRoot) return _active.ToArray();
        }

        /// <summary>
        /// Instant 는 속성만 바꾸고 목록에 올리지 않는다. Duration/Infinite 는 목록에 올라 만료 시 사라진다.
        /// 같은 <paramref name="instanceId"/> 재적용은 멱등(교체), 같은 Effect Id 는 <see cref="EStackPolicy"/> 대로 갱신/중첩.
        /// </summary>
        /// <returns>
        /// 활성 인스턴스 id(Instant·null 이면 -1). 스택 정책이 기존 인스턴스를 재사용하면 <b>넘긴 id 와 다르다</b> —
        /// 호출부는 반환값을 브로드캐스트해야 <c>S_RemoveEffect</c> 와 짝이 맞는다.
        /// </returns>
        public int ApplyEffect(GameplayEffectDefinition definition, int instanceId, long nowMs, int stacks = 1)
        {
            if (definition == null)
                return -1;

            if (definition.Policy == EDurationPolicy.Instant)
            {
                ApplyModifiers(definition.Modifiers);
                return -1;
            }

            lock (SyncRoot)
            {
                // 스택 정책보다 먼저 — 중복 패킷이 스택을 부풀리면 안 된다.
                int existingIndex = IndexOfInstance(instanceId);
                if (existingIndex >= 0)
                {
                    _active[existingIndex] = new ActiveGameplayEffect(instanceId, definition, nowMs, stacks);
                    RecalculateStatsLocked();
                    return instanceId;
                }

                // 새 인스턴스는 아직 안 걸려 있을 때만 — 그래야 None 이 중첩되지 않는다.
                int sameIndex = IndexOfEffectId(definition.Id);
                if (sameIndex >= 0)
                {
                    var same = _active[sameIndex];
                    if (definition.Stack == EStackPolicy.None)
                        return same.InstanceId;

                    var updated = same.WithStart(nowMs);
                    if (definition.Stack == EStackPolicy.Stack)
                        updated = updated.WithAddedStack(definition.MaxStacks);

                    // struct 복사본이라 되쓰지 않으면 갱신이 조용히 사라진다.
                    _active[sameIndex] = updated;

                    RecalculateStatsLocked();
                    return same.InstanceId;
                }

                _active.Add(new ActiveGameplayEffect(instanceId, definition, nowMs, stacks));
                RecalculateStatsLocked();
                return instanceId;
            }
        }

        /// <summary>강제 해제. 반환 = 실제로 있었는가.</summary>
        public bool RemoveEffect(int instanceId)
        {
            lock (SyncRoot)
            {
                int index = IndexOfInstance(instanceId);
                if (index < 0)
                    return false;

                _active.RemoveAt(index);
                RecalculateStatsLocked();
                return true;
            }
        }

        /// <summary>
        /// 이 틱에 만료된 인스턴스 id 목록, 없으면 null(빈 리스트 대신 — 10Hz × 액터 수 할당을 피한다).
        /// </summary>
        public IReadOnlyList<int>? TickEffects(long nowMs)
        {
            lock (SyncRoot)
            {
                if (_active.Count == 0)
                    return null;

                List<int>? expired = null;
                for (int i = _active.Count - 1; i >= 0; i--)
                {
                    if (!_active[i].IsExpiredAt(nowMs))
                        continue;

                    (expired ??= new List<int>()).Add(_active[i].InstanceId);
                    _active.RemoveAt(i);
                }

                if (expired == null)
                    return null;

                RecalculateStatsLocked();
                return expired;
            }
        }

        private int IndexOfInstance(int instanceId)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].InstanceId == instanceId)
                    return i;
            return -1;
        }

        /// <summary>
        /// 같은 Effect Id 의 위치, 없으면 −1. 값이 아니라 인덱스인 이유 — struct 복사본에 갱신하면 반영되지 않는다.
        /// </summary>
        private int IndexOfEffectId(string effectId)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Definition.Id == effectId)
                    return i;
            return -1;
        }

        /// <summary>
        /// 스탯 = Base + 활성 모디파이어로 재계산. 자원(HP·마나)은 제외 — 재계산하면 소비·회복이 되돌아간다.
        /// 호출자가 <see cref="SyncRoot"/> 를 잡고 있어야 한다.
        /// </summary>
        private void RecalculateStatsLocked()
        {
            foreach (var attribute in StatsSnapshot())
            {
                int recalculated = GameplayEffectMath.Aggregate(
                    _attributes.BaseOr(attribute), ActiveModifiersFor(attribute), _attributes.MaxOr(attribute));
                _attributes.SetCurrent(attribute, recalculated);
            }
        }

        /// <summary>순회 중 <c>SetCurrent</c> 가 지연 열거자를 무효화하므로 스냅샷으로 돈다.</summary>
        private List<EGameplayAttribute> StatsSnapshot()
        {
            var list = new List<EGameplayAttribute>();
            foreach (var attribute in _attributes.Stats)
                list.Add(attribute);
            return list;
        }

        private IEnumerable<GameplayAttributeModifier> ActiveModifiersFor(EGameplayAttribute attribute)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var active = _active[i];
                var mods = active.Definition.Modifiers;
                for (int m = 0; m < mods.Count; m++)
                {
                    if (mods[m].AttributeType != attribute)
                        continue;
                    for (int s = 0; s < active.Stacks; s++)
                        yield return mods[m];
                }
            }
        }

        // ── 효과 적용 ───────────────────────────────────────────────────

        /// <summary>
        /// 보유한 속성에만 즉발 적용(미보유는 무시 — 몬스터에 마나가 생기지 않는다).
        /// Duration 효과를 여기로 넣으면 영구 버프가 된다 — <see cref="ApplyEffect"/> 를 쓴다.
        /// </summary>
        public void ApplyModifiers(IReadOnlyList<GameplayAttributeModifier> mods)
        {
            if (mods == null || mods.Count == 0)
                return;

            // 같은 속성의 모디파이어는 한 번에 집계해야 Additive/Multiplicative 순서 규칙이 성립한다.
            var touched = new HashSet<EGameplayAttribute>();
            for (int i = 0; i < mods.Count; i++)
                touched.Add(mods[i].AttributeType);

            lock (SyncRoot)
            {
                foreach (var attribute in touched)
                {
                    if (!_attributes.TryGet(attribute, out int current))
                        continue;

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
