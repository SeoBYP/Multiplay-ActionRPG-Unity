using System;
using System.Collections.Generic;
using UnityEngine;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// GAS 의 <b>Unity 경계 어댑터</b>. 게임 로직은 하나도 갖지 않고 Shared
    /// <see cref="AbilitySystemComponent"/> 를 <b>소유</b>해 위임한다.
    ///
    /// <para><b>왜 갈랐나</b>: 예전엔 이 MonoBehaviour 가 활성 Effect 목록·스택 정책·만료·태그 합산·스탯 재계산을
    /// 전부 직접 들고 있었고, 서버는 그것을 <b>다시 한 번</b> 구현했다. 두 구현은 이미 갈라져 있었다 —
    /// 같은 <c>EStackPolicy.None</c> 효과를 두 번 걸면 한쪽은 1개, 다른 쪽은 2개가 됐다.
    /// 이제 산식은 한 곳(Shared)에만 있고, 여기 남은 것은 <b>엔진이 있어야만 할 수 있는 일</b>뿐이다:
    /// 프리팹 직렬화(<see cref="Attributes"/>) · <c>Update</c> 시계 · C# 이벤트 재발행.</para>
    /// </summary>
    public class GasComponent : MonoBehaviour
    {
        /// <summary>
        /// Inspector 저작 <b>시작값</b>. 런타임 값은 여기 있지 않다 — <see cref="Awake"/> 에서
        /// <see cref="Gas"/> 로 옮겨지고, 이후 이 리스트는 읽히지 않는다.
        /// </summary>
        public List<GameplayAttribute> Attributes = new();

        /// <summary>런타임 GAS 상태 일체(속성·태그·활성 Effect·쿨다운). <b>서버 Actor 와 같은 타입</b>.</summary>
        public AbilitySystemComponent Gas { get; } = new();

        private readonly Dictionary<EGameplayAttribute, (int Current, int Max)> _notified = new();
        private long _clockMs;
        private int _nextInstanceId = 1;
        private bool _initialized;

        public IReadOnlyList<ActiveGameplayEffect> ActiveEffects => Gas.ActiveEffects();

        /// <summary>
        /// 속성의 현재값이 변할 때 (type, current, max)로 발행.
        /// 소비자: HUD 등 외부가 이 컴포넌트 하나만 구독하면 모든 스탯 변화를 받는다.
        /// </summary>
        public event Action<EGameplayAttribute, int, int> OnAttributeChanged;

        /// <summary>활성 Effect가 추가/제거/만료될 때 발행. (남은시간 변화는 발행하지 않음 — View가 로컬 카운트다운)</summary>
        public event Action OnActiveEffectsChanged;

        private void Awake() => InitializeAttributes();

        private void Update() => Tick(Time.deltaTime);

        /// <summary>저작값을 런타임(Shared)으로 옮긴다. 재호출하면 저작 시작값으로 되돌린다.</summary>
        public void InitializeAttributes()
        {
            if (Attributes.Count == 0)
            {
                // 테스트용 또는 세팅 누락 방지용 기본값. 실제 캐릭터는 Inspector에서 명시하는 것이 좋다.
                Attributes.Add(new GameplayAttribute(EGameplayAttribute.Health, 100, 100));
            }

            foreach (var authored in Attributes)
            {
                authored.Validate();
                if (authored.Kind == EAttributeKind.Stat)
                    Gas.DefineStat(authored.AttributeType, authored.BaseValue);
                else
                    Gas.DefineResource(authored.AttributeType, authored.StartingValue, authored.MaxValue);
            }

            _initialized = true;
            RaiseAttributeChanges();
        }

        private void OnValidate()
        {
            foreach (var attribute in Attributes)
                attribute.Validate();
        }

        // ── 속성 접근 (값만 오간다 — 속성 객체를 밖으로 내주지 않는다) ──
        //
        // 예전엔 GetAttribute() 가 GameplayAttribute 를 통째로 내줬고, 호출부가 거기 대고 SetCurrent/ApplyModifier
        // 를 직접 불렀다. 그래서 "값이 변했다"를 이 컴포넌트가 놓치는 경로가 생겼다(이벤트 미발행).
        // 이제 모든 변경이 여기를 지나므로 이벤트가 새지 않는다.

        /// <summary>현재값. 미보유 속성은 0.</summary>
        public int Current(EGameplayAttribute attributeType)
        {
            EnsureInitialized();
            return Gas[attributeType];
        }

        /// <summary>상한값. 미보유 속성은 0.</summary>
        public int Max(EGameplayAttribute attributeType)
        {
            EnsureInitialized();
            return Gas.Max(attributeType);
        }

        /// <summary>이 캐릭터가 그 속성을 보유하는가. <b>false = 0 이 아니라 "없음"</b>.</summary>
        public bool Has(EGameplayAttribute attributeType)
        {
            EnsureInitialized();
            return Gas.Has(attributeType);
        }

        /// <summary>현재값 설정([0, Max] 클램프). 미보유 속성엔 무동작.</summary>
        public void SetCurrent(EGameplayAttribute attributeType, int value)
        {
            EnsureInitialized();
            Gas[attributeType] = value;
            RaiseAttributeChanges();
        }

        /// <summary>
        /// 상한 변경(현재값은 새 상한으로 재클램프). 서버 권위 MaxHealth·MaxMana 정렬용.
        /// 풀충전이 필요하면 호출 측이 <see cref="SetCurrent"/> 를 잇는다.
        /// </summary>
        public void SetMax(EGameplayAttribute attributeType, int max)
        {
            EnsureInitialized();
            Gas.SetMax(attributeType, max);
            RaiseAttributeChanges();
        }

        /// <summary>모디파이어 즉시 적용(보유 속성에만). 속성별 집계는 Shared 산식이 한다.</summary>
        public void ApplyModifiers(IReadOnlyList<GameplayAttributeModifier> mods)
        {
            EnsureInitialized();
            Gas.ApplyModifiers(mods);
            RaiseAttributeChanges();
        }

        // ── GameplayEffect (버프/디버프) ───────────────────────────

        /// <summary>
        /// 로컬 Effect 적용(인스턴스 id 는 클라가 생성).
        ///   Instant  → 대상 속성을 즉시·영구 변경 (데미지/힐).
        ///   Duration/Infinite → 활성 목록에 등록하고 Stat 을 재계산.
        /// </summary>
        /// <returns>활성 인스턴스 id. Instant 이거나 def 가 null 이면 -1.</returns>
        public int ApplyEffect(GameplayEffectDefinition def, GasComponent source = null)
        {
            EnsureInitialized();
            int id = Gas.ApplyEffect(def, _nextInstanceId, _clockMs);
            if (id == _nextInstanceId)
                _nextInstanceId++; // 스택 정책이 기존 인스턴스를 재사용했으면 id 를 소비하지 않는다
            AfterEffectChange();
            return id;
        }

        /// <summary>
        /// 서버 권위 적용(EF-2d): 서버가 부여한 InstanceId를 그대로 키로 사용한다(클라가 id 생성 안 함).
        /// 같은 InstanceId 재수신 시 갱신(멱등). 제거는 서버 S_RemoveEffect → RemoveEffect(instanceId)가 권위.
        ///
        /// 시작 시각은 로컬 clock(_clockMs)을 쓴다 — 공유 시계(서버 tick) 도입 전까지 만료 타이밍의
        /// 클라 일관성을 위해. 서버 StartTick 기반 정밀 정정은 공유 시계 합류 시.
        /// </summary>
        /// <param name="healthOverride">
        /// 0이 아니면 Instant 효과의 Health 모디파이어 양을 이 서버 권위 값(음수=데미지)으로 덮어쓴다.
        /// 스탯 의존 데미지(몬스터 공격 − Defense 등)는 카탈로그 고정값 대신 서버가 계산해 보낸 값을 적용한다.
        /// </param>
        public void ApplyEffectAuthoritative(GameplayEffectDefinition def, int instanceId, int stacks = 1, int healthOverride = 0)
        {
            EnsureInitialized();
            if (def == null)
                return;

            if (def.Policy == EDurationPolicy.Instant)
            {
                ApplyModifiers(WithHealthOverride(def.Modifiers, healthOverride));
                return;
            }

            Gas.ApplyEffect(def, instanceId, _clockMs, stacks);

            // 이후 로컬 생성 id가 서버 id와 충돌하지 않도록 카운터를 끌어올린다.
            if (instanceId >= _nextInstanceId)
                _nextInstanceId = instanceId + 1;

            AfterEffectChange();
        }

        /// <summary>서버 권위 Health 델타로 카탈로그 고정값을 갈아끼운다. 0 이면 정의 그대로.</summary>
        private static IReadOnlyList<GameplayAttributeModifier> WithHealthOverride(
            IReadOnlyList<GameplayAttributeModifier> mods, int healthOverride)
        {
            if (healthOverride == 0)
                return mods;

            var replaced = new List<GameplayAttributeModifier>(mods.Count);
            foreach (var mod in mods)
            {
                replaced.Add(mod.AttributeType == EGameplayAttribute.Health
                    ? GameplayAttributeModifier.Create(EGameplayAttribute.Health, healthOverride, mod.ModifierType)
                    : mod);
            }
            return replaced;
        }

        public void RemoveEffect(int instanceId)
        {
            EnsureInitialized();
            if (!Gas.RemoveEffect(instanceId))
                return;
            AfterEffectChange();
        }

        // ── GameplayTag (상태 태그) ───────────────────────────

        /// <summary>
        /// 직접 부여한 상태 태그(예: 사망 State.Dead). Effect 없이 즉시 세우는 태그용.
        /// 활성 Effect의 GrantedTags 는 HasTag 가 동적으로 합산하므로 여기 넣지 않는다.
        /// </summary>
        public void AddTag(GameplayTag tag) => Gas.AddTag(tag);

        public void RemoveTag(GameplayTag tag) => Gas.RemoveTag(tag);

        /// <summary>직접 부여한 태그 + 활성 Effect의 GrantedTags 를 합쳐 질의. 입력 게이트 등이 폴링한다.</summary>
        public bool HasTag(GameplayTag tag) => Gas.HasTag(tag);

        /// <summary>내부 clock을 전진시키고 만료된 Effect를 제거한다. (테스트는 직접 호출)</summary>
        public void Tick(float deltaTime)
        {
            EnsureInitialized();
            _clockMs += (long)(deltaTime * 1000f);

            if (Gas.TickEffects(_clockMs) is null)
                return;

            AfterEffectChange();
        }

        /// <summary>표시/중계용 스냅샷. now 기준 남은시간을 여기서 계산해 채운다(clock 을 밖으로 내지 않으려고).</summary>
        public IReadOnlyList<ActiveEffectSnapshot> GetActiveEffectSnapshots()
        {
            var active = Gas.ActiveEffects();
            var list = new List<ActiveEffectSnapshot>(active.Count);
            foreach (var e in active)
            {
                int remaining = e.IsInfinite ? 0 : EffectTiming.RemainingMs(e.StartMs, e.Definition.DurationMs, _clockMs);
                list.Add(new ActiveEffectSnapshot(e.Definition.Id, remaining, e.Definition.DurationMs, e.Stacks, e.IsInfinite));
            }
            return list;
        }

        // ── 이벤트 재발행 ───────────────────────────────────────────

        /// <summary>Effect 변경 후속 — 스탯 재계산은 Shared 가 이미 했고, 여기선 그 결과를 알리기만 한다.</summary>
        private void AfterEffectChange()
        {
            RaiseAttributeChanges();
            OnActiveEffectsChanged?.Invoke();
        }

        /// <summary>
        /// 직전 발행값과 달라진 속성만 <see cref="OnAttributeChanged"/> 로 알린다.
        /// Shared 는 이벤트를 갖지 않는다(락 안에서 콜백을 부르면 그 콜백이 다시 락을 잡는 순간 설계가 무너진다) —
        /// 그래서 <b>변경 후 비교</b>가 Unity 쪽 책임이다.
        /// </summary>
        private void RaiseAttributeChanges()
        {
            if (OnAttributeChanged == null)
            {
                Remember();
                return;
            }

            foreach (var type in Gas.DefinedAttributes())
            {
                var now = (Gas[type], Gas.Max(type));
                if (_notified.TryGetValue(type, out var last) && last == now)
                    continue;

                _notified[type] = now;
                OnAttributeChanged.Invoke(type, now.Item1, now.Item2);
            }
        }

        private void Remember()
        {
            foreach (var type in Gas.DefinedAttributes())
                _notified[type] = (Gas[type], Gas.Max(type));
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
                InitializeAttributes();
        }
    }
}
