using System;
using System.Collections.Generic;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 한 액터의 GAS 상태 — <b>속성 · 태그 · 쿨다운 · 활성 Effect 를 한 곳에서</b> 보유한다. 종족(플레이어/몬스터)을 모른다.
    ///
    /// <para><b>왜 Actor 에서 뺐나</b>: Actor 는 "무엇이 존재하고 어디 있는가"(신원·공간·수명)를 맡고,
    /// "그것이 전투적으로 어떤 상태인가"는 여기가 맡는다. 한 클래스가 둘 다 하면 새 능력치·새 태그를
    /// 넣을 때마다 Actor 가 부풀어 결국 예전 PlayerState 처럼 다시 겸직 클래스가 된다.</para>
    ///
    /// <para><b>왜 Shared 인가</b>: GAS 의 알맹이(속성·태그·활성 Effect·집계 산식)는 엔진에 의존하지 않는다.
    /// 여기에 두면 서버 Actor 와 클라가 <b>같은 타입·같은 산식</b>을 쓴다 — 서버 HP == 클라 HP 가 구조로 보장된다.
    /// 클라의 Unity 경계 어댑터는 <c>GasComponent</c>(MonoBehaviour) 이고, 그쪽이 이 클래스를 <b>소유</b>한다.</para>
    ///
    /// <para><b>스스로 스레드 안전하다.</b> 서버에서 이 상태는 <b>두 스레드</b>가 만진다 —
    /// 틱 루프(마나 회복·피해)와 패킷 핸들러(마나 차감·쿨다운·회피). 예전에는 방/저장소 락에 의존해
    /// <b>저장소를 지나는 경로만</b> 안전했고 핸들러가 Gas 를 직접 만지는 경로는 구멍이었다
    /// (실제로 마나가 그 구멍으로 샜다). 경계를 여기로 내려 <b>어느 경로로 오든</b> 안전하게 만든다.</para>
    ///
    /// <para>속성 저장소와 태그 집합은 <b>private</b> 이다 — 밖에 노출하면 락을 우회할 수 있다.
    /// 접근은 인덱서 하나(<c>gas[attr]</c>)로 한다. 속성이 늘어도 <b>멤버를 추가하지 않는다</b>.</para>
    /// </summary>
    public sealed class AbilitySystemComponent
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
        public void DefineResource(EGameplayAttribute attribute, int max) => DefineResource(attribute, max, max);

        /// <summary>
        /// 자원 속성을 <b>시작 현재값</b>과 함께 부여한다. 프리팹에 "HP 30/100" 처럼 저작된 경우를 그대로 살린다
        /// (서버 스폰은 만땅이라 <see cref="DefineResource(EGameplayAttribute,int)"/> 를 쓴다).
        /// </summary>
        public void DefineResource(EGameplayAttribute attribute, int current, int max)
        {
            if (max <= 0) return;
            lock (SyncRoot) _attributes.Define(attribute, current, max);
        }

        /// <summary>
        /// 스탯 속성 부여(공격력·방어력 — 상한 없음). 버프가 base 를 넘을 수 있어야 하므로 클램프하지 않는다.
        /// <b>0 을 넣어도 부여한다</b> — "0 인 스탯"과 "스탯이 없음"은 다르고, 그 구분이 이 설계의 요점이다.
        /// </summary>
        public void DefineStat(EGameplayAttribute attribute, int value)
        {
            lock (SyncRoot) _attributes.Define(attribute, value, AttributeSet.NoMax, isStat: true);
        }

        // ── 속성 접근 (멤버는 인덱서 하나 — 속성이 늘어도 여기는 안 늘어난다) ──

        /// <summary>현재값. 읽기는 미보유 시 0, 쓰기는 미보유 시 무동작.</summary>
        public int this[EGameplayAttribute attribute]
        {
            get { lock (SyncRoot) return _attributes.GetOr(attribute); }
            set { lock (SyncRoot) _attributes.SetCurrent(attribute, value); }
        }

        /// <summary>
        /// 상한 변경(현재값·Base 를 새 상한으로 재클램프). 미보유면 무동작.
        /// 레벨 파생 MaxHealth 처럼 <b>권위 스탯이 나중에 도착</b>하는 경우에 쓴다 —
        /// 풀충전이 필요하면 호출 측이 현재값 설정을 잇는다(여기서 정하지 않는다).
        /// </summary>
        public void SetMax(EGameplayAttribute attribute, int max)
        {
            lock (SyncRoot) _attributes.SetMax(attribute, max);
        }

        /// <summary>상한값. 미보유면 0.</summary>
        public int Max(EGameplayAttribute attribute)
        {
            lock (SyncRoot) return _attributes.MaxOr(attribute);
        }

        /// <summary>보유 속성 목록 스냅샷. 변화 감지·디버그용(순회 중 갱신돼도 안전하도록 복사본).</summary>
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

        /// <summary>
        /// 태그 보유 여부. <b>직접 부여한 태그 ∪ 활성 Effect 가 부여하는 태그</b>를 합산한다.
        ///
        /// <para>활성 Effect 의 태그를 컨테이너에 <b>복사해 두지 않는</b> 이유: 만료·중복 부여 때
        /// 회수 장부를 따로 관리해야 하고(두 Effect 가 같은 스턴을 주면 하나가 끝날 때 떼면 안 된다)
        /// 그 장부가 어긋나는 순간 영구 스턴이 된다. 파생값으로 두면 만료가 곧 해제라 어긋날 여지가 없다.</para>
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

        /// <summary>
        /// 어빌리티 발동이 태그로 차단되는가. <see cref="AbilityActivationMath"/> 의 blocked 인자에 그대로 넣는다 —
        /// 예전의 하드코딩 blocked:false 를 대체하는 자리.
        /// </summary>
        public bool IsActivationBlocked
        {
            get { lock (SyncRoot) return HasTagLocked(GameplayTags.Dead) || HasTagLocked(GameplayTags.Stun); }
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

        // ── 활성 Effect (지속시간 · 스택 · 만료) ─────────────────────────
        //
        // 서버가 만료를 소유하는 자리다. 예전에는 CC 를 브로드캐스트만 하고 만료를 클라가 했다 —
        // 즉 "언제 스턴이 풀리는가"의 권위가 클라에 있었고, 서버는 자기가 건 스턴조차 몰랐다.
        // (그래서 서버의 IsActivationBlocked 는 항상 false 였다.)

        private readonly List<ActiveGameplayEffect> _active = new List<ActiveGameplayEffect>();

        /// <summary>활성 Effect 수(진단·테스트용).</summary>
        public int ActiveEffectCount
        {
            get { lock (SyncRoot) return _active.Count; }
        }

        /// <summary>활성 Effect 스냅샷(복사본 — 밖에서 만져도 내부가 안 바뀐다).</summary>
        public IReadOnlyList<ActiveGameplayEffect> ActiveEffects()
        {
            lock (SyncRoot) return _active.ToArray();
        }

        /// <summary>
        /// Effect 적용. <b>Instant 는 즉시 속성만 바꾸고 목록에 올리지 않는다</b>(되돌릴 것이 없다).
        /// Duration/Infinite 는 목록에 올려 태그·스탯의 출처가 되고, 만료 시 통째로 사라진다.
        ///
        /// <para>같은 <paramref name="instanceId"/> 재적용은 <b>멱등</b>(교체)이라 패킷이 중복 도착해도 두 번 걸리지 않는다.
        /// 스택 정책은 <see cref="EStackPolicy"/> 를 따른다 — 같은 Effect Id 가 이미 있으면 새 인스턴스를 만들지 않고
        /// 기존 것을 갱신/중첩한다.</para>
        /// </summary>
        /// <returns>
        /// 활성 목록에서 이 Effect 를 가리키는 인스턴스 id. Instant 이거나 def 가 null 이면 -1.
        /// <b>넘긴 <paramref name="instanceId"/> 와 다를 수 있다</b> — 스택 정책이 기존 인스턴스를 재사용하면
        /// 그쪽 id 가 나온다. 호출부는 <b>반환값</b>을 브로드캐스트해야 만료 통지(<c>S_RemoveEffect</c>)와 짝이 맞는다.
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
                // 같은 인스턴스의 재수신 = 갱신. 스택 정책보다 먼저 본다(중복 패킷이 스택을 부풀리면 안 된다).
                int existingIndex = IndexOfInstance(instanceId);
                if (existingIndex >= 0)
                {
                    _active[existingIndex] = new ActiveGameplayEffect(instanceId, definition, nowMs, stacks);
                    RecalculateStatsLocked();
                    return instanceId;
                }

                // 같은 Effect 가 이미 걸려 있으면 스택 정책이 결정한다. 새 인스턴스를 만드는 건
                // "아직 안 걸려 있을 때"뿐이다 — 그래야 None 이 중첩되지 않는다.
                var same = FindByEffectId(definition.Id);
                if (same != null)
                {
                    if (definition.Stack == EStackPolicy.None)
                        return same.InstanceId; // 재적용 무시 — 상태가 그대로라 재계산도 불필요

                    same.Refresh(nowMs); // 지속시간 갱신은 Refresh·Stack 공통
                    if (definition.Stack == EStackPolicy.Stack)
                        same.AddStack(definition.MaxStacks);

                    RecalculateStatsLocked();
                    return same.InstanceId;
                }

                _active.Add(new ActiveGameplayEffect(instanceId, definition, nowMs, stacks));
                RecalculateStatsLocked();
                return instanceId;
            }
        }

        /// <summary>Effect 를 강제 해제한다. 반환 = 실제로 있었는가(멱등 가드).</summary>
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
        /// 만료된 Effect 를 걷어낸다. <b>이 틱에 만료된 인스턴스 id 목록</b>을 돌려주고, 없으면 null.
        ///
        /// <para>null 을 돌려주는 이유: 만료는 드문 사건인데 매 틱 · 매 액터마다 빈 리스트를 할당하면
        /// 10Hz × 액터 수만큼 쓰레기가 생긴다. 호출부는 null 검사 한 번으로 그 비용을 없앤다.</para>
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

        private ActiveGameplayEffect? FindByEffectId(string effectId)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Definition.Id == effectId)
                    return _active[i];
            return null;
        }

        /// <summary>
        /// 스탯을 Base + 활성 Effect 모디파이어로 <b>다시 계산</b>한다(클라 <c>RecalculateStats</c> 와 동일 산식).
        /// 자원(HP·마나)은 대상이 아니다 — 소비·회복으로 스스로 변하는 값이라 재계산하면 되돌아가 버린다.
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

        /// <summary>
        /// 스탯 목록 스냅샷. <see cref="AttributeSet.Stats"/> 는 지연 열거라 순회 중 <c>SetCurrent</c> 가
        /// 딕셔너리 엔트리를 갈아끼우면 열거자가 무효화된다(구조 변경이 아니어도 .NET 은 버전을 올린다).
        /// </summary>
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
        /// 모디파이어를 <b>보유한 속성에만</b> 적용한다(미보유 대상 모디파이어는 무시 — 몬스터에 마나가 몰래 생기지 않는다).
        /// 속성별로 모아 <see cref="GameplayEffectMath.Aggregate"/>(클라와 동일 산식)로 집계하므로 서버·클라 값이 어긋나지 않는다.
        ///
        /// <para><b>즉발(Instant) 의미로만 적용한다</b> — 적용 즉시 현재값을 바꾸고 되돌리지 않는다.
        /// Duration 효과는 이 경로가 아니라 <see cref="ApplyEffect"/> 로 넣는다(여기로 넣으면 영구 버프가 된다).</para>
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
