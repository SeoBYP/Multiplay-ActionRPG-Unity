using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 캐릭터나 오브젝트가 GAS 기능을 사용하기 위한 진입점이다.
    /// Attribute를 보관하고, Ability를 부여하고, Ability 활성화를 중계한다.
    /// </summary>
    public class AbilitySystemComponent : MonoBehaviour
    {
        // Inspector에서 초기 Attribute 값을 세팅한다. 런타임 조회는 Dictionary로 한다.
        public List<GameplayAttribute> Attributes = new();

        private Dictionary<EGameplayAttribute, GameplayAttribute> _gameplayAttributes = new();
        private readonly List<Ability> _abilities = new();
        private readonly HashSet<GameplayAttribute> _wired = new();
        private readonly List<ActiveGameplayEffect> _active = new();
        private long _clockMs;
        private int _nextInstanceId = 1;
        private bool _initialized;

        public IReadOnlyList<Ability> Abilities => _abilities;
        public IReadOnlyList<ActiveGameplayEffect> ActiveEffects => _active;

        /// <summary>
        /// Attribute의 CurrentValue가 변할 때 (type, current, max)로 발행.
        /// 소비자: HUD 등 외부가 ASC 하나만 구독하면 모든 스탯 변화를 받는다.
        /// </summary>
        public event Action<EGameplayAttribute, int, int> OnAttributeChanged;

        /// <summary>활성 Effect가 추가/제거/만료될 때 발행. (남은시간 변화는 발행하지 않음 — View가 로컬 카운트다운)</summary>
        public event Action OnActiveEffectsChanged;

        private void Awake()
        {
            InitializeAttributes();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void InitializeAttributes()
        {
            if (Attributes.Count == 0)
            {
                // 테스트용 또는 세팅 누락 방지용 기본값. 실제 캐릭터는 Inspector에서 명시하는 것이 좋다.
                Attributes.Add(new GameplayAttribute(EGameplayAttribute.Health, 100, 100));
            }

            _gameplayAttributes.Clear();
            foreach (var attribute in Attributes)
            {
                attribute.Validate();
                _gameplayAttributes[attribute.AttributeType] = attribute;

                // 각 attribute는 정확히 1회만 구독 (재초기화 시 중복 구독 방지).
                // 변화 시 ASC가 모아 OnAttributeChanged로 외부에 재발행한다.
                if (_wired.Add(attribute))
                {
                    var captured = attribute;
                    captured.OnChanged += () => RaiseAttributeChanged(captured);
                }
            }

            _initialized = true;
        }

        private void RaiseAttributeChanged(GameplayAttribute attribute)
        {
            OnAttributeChanged?.Invoke(attribute.AttributeType, attribute.CurrentValue, attribute.MaxValue);
        }

        private void OnValidate()
        {
            foreach (var attribute in Attributes)
            {
                attribute.Validate();
            }
        }
        
        public GameplayAttribute GetAttribute(EGameplayAttribute attributeType)
        {
            EnsureInitialized();
            return _gameplayAttributes.GetValueOrDefault(attributeType);
        }

        public bool TryGetAttribute(EGameplayAttribute attributeType, out GameplayAttribute attribute)
        {
            EnsureInitialized();
            return _gameplayAttributes.TryGetValue(attributeType, out attribute);
        }

        public T GrantAbility<T>(T ability) where T : Ability
        {
            if (ability == null)
                throw new ArgumentNullException(nameof(ability));

            // Ability가 어떤 ASC에 속하는지 연결한 뒤 목록에 보관한다.
            ability.GrantTo(this);
            _abilities.Add(ability);
            return ability;
        }

        public bool TryActivateAbility<T>(AbilityActivationContext context) where T : Ability
        {
            // 타입으로 Ability를 찾는 편의 API. 같은 타입 Ability를 여러 개 둘 계획이면 별도 핸들이 필요하다.
            T ability = _abilities.OfType<T>().FirstOrDefault();
            return ability != null && TryActivateAbility(ability, context);
        }

        public bool TryActivateAbility(Ability ability, AbilityActivationContext context)
        {
            if (ability == null)
                return false;

            // 호출자가 Source를 몰라도 ASC가 항상 자신을 Source로 넣어준다.
            AbilityActivationContext sourcedContext = (context ?? new AbilityActivationContext(new List<AbilitySystemComponent>()))
                .WithSource(this);
            return ability.TryActivate(sourcedContext);
        }

        // ── GameplayEffect (버프/디버프) ───────────────────────────

        /// <summary>
        /// Effect를 적용한다.
        ///   Instant  → 대상 Resource Attribute를 즉시·영구 변경 (데미지/힐).
        ///   Duration/Infinite → 활성 목록에 등록하고 Stat을 재계산. 반환값은 InstanceId.
        /// </summary>
        public int ApplyEffect(GameplayEffectDefinition def, AbilitySystemComponent source = null)
        {
            EnsureInitialized();
            if (def == null)
                return -1;

            if (def.Policy == EDurationPolicy.Instant)
            {
                foreach (var mod in def.Modifiers)
                    if (_gameplayAttributes.TryGetValue(mod.AttributeType, out var attr))
                        attr.ApplyModifier(mod);
                return -1;
            }

            // Duration / Infinite — 스택 정책
            var existing = FindStackable(def);
            if (existing != null)
            {
                switch (def.Stack)
                {
                    case EStackPolicy.None:
                        return existing.InstanceId; // 재적용 무시
                    case EStackPolicy.Refresh:
                        existing.Refresh(_clockMs);
                        break;
                    case EStackPolicy.Stack:
                        existing.AddStack(def.MaxStacks);
                        existing.Refresh(_clockMs);
                        break;
                }
            }
            else
            {
                existing = new ActiveGameplayEffect(_nextInstanceId++, def, _clockMs, 1);
                _active.Add(existing);
            }

            RecalculateStats();
            OnActiveEffectsChanged?.Invoke();
            return existing.InstanceId;
        }

        /// <summary>
        /// 서버 권위 적용(EF-2d): 서버가 부여한 InstanceId를 그대로 키로 사용한다(클라가 id 생성 안 함).
        /// 같은 InstanceId 재수신 시 갱신(멱등). 제거는 서버 S_RemoveEffect → RemoveEffect(instanceId)가 권위.
        ///
        /// 시작 시각은 로컬 clock(_clockMs)을 쓴다 — 공유 시계(서버 tick) 도입 전까지 만료 타이밍의
        /// 클라 일관성을 위해. 서버 StartTick 기반 정밀 정정은 공유 시계 합류 시.
        /// </summary>
        public void ApplyEffectAuthoritative(GameplayEffectDefinition def, int instanceId, int stacks = 1)
        {
            EnsureInitialized();
            if (def == null)
                return;

            if (def.Policy == EDurationPolicy.Instant)
            {
                foreach (var mod in def.Modifiers)
                    if (_gameplayAttributes.TryGetValue(mod.AttributeType, out var attr))
                        attr.ApplyModifier(mod);
                return;
            }

            _active.RemoveAll(e => e.InstanceId == instanceId);
            _active.Add(new ActiveGameplayEffect(instanceId, def, _clockMs, stacks < 1 ? 1 : stacks));

            // 이후 로컬 생성 id가 서버 id와 충돌하지 않도록 카운터를 끌어올린다.
            if (instanceId >= _nextInstanceId)
                _nextInstanceId = instanceId + 1;

            RecalculateStats();
            OnActiveEffectsChanged?.Invoke();
        }

        public void RemoveEffect(int instanceId)
        {
            int removed = _active.RemoveAll(e => e.InstanceId == instanceId);
            if (removed <= 0)
                return;
            RecalculateStats();
            OnActiveEffectsChanged?.Invoke();
        }

        /// <summary>내부 clock을 전진시키고 만료된 Effect를 제거한다. (테스트는 직접 호출)</summary>
        public void Tick(float deltaTime)
        {
            _clockMs += (long)(deltaTime * 1000f);
            if (_active.Count == 0)
                return;

            int removed = _active.RemoveAll(e => !e.IsInfinite && _clockMs >= e.EndMs);
            if (removed <= 0)
                return;
            RecalculateStats();
            OnActiveEffectsChanged?.Invoke();
        }

        /// <summary>표시/중계용 스냅샷. now 기준 남은시간을 ASC가 계산해 채운다.</summary>
        public IReadOnlyList<ActiveEffectSnapshot> GetActiveEffectSnapshots()
        {
            var list = new List<ActiveEffectSnapshot>(_active.Count);
            foreach (var e in _active)
            {
                int remaining = e.IsInfinite ? 0 : (int)Math.Max(0, e.EndMs - _clockMs);
                list.Add(new ActiveEffectSnapshot(e.Definition.Id, remaining, e.Definition.DurationMs, e.Stacks, e.IsInfinite));
            }
            return list;
        }

        private ActiveGameplayEffect FindStackable(GameplayEffectDefinition def)
        {
            // 1단계: DefId 기준으로 동일 Effect를 찾는다. (source별 분리는 2단계에서)
            foreach (var e in _active)
                if (e.Definition.Id == def.Id)
                    return e;
            return null;
        }

        private void RecalculateStats()
        {
            foreach (var kv in _gameplayAttributes)
            {
                var attr = kv.Value;
                if (attr.Kind != EAttributeKind.Stat)
                    continue;
                attr.SetCurrent(GameplayEffectMath.Aggregate(attr.BaseValue, ModifiersFor(attr.AttributeType), attr.MaxValue));
            }
        }

        private IEnumerable<GameplayAttributeModifier> ModifiersFor(EGameplayAttribute type)
        {
            foreach (var active in _active)
                foreach (var mod in active.Definition.Modifiers)
                    if (mod.AttributeType == type)
                        for (int s = 0; s < active.Stacks; s++)
                            yield return mod;
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
                InitializeAttributes();
        }
    }
}
