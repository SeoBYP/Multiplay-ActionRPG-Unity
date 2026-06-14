using System.Collections.Generic;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Tests.EditMode.GamePlayAbilitySystem
{
    /// <summary>
    /// GameplayEffect(버프/디버프) 핵심 동작 — 가역성·만료·스택·즉발/지속 분리.
    /// </summary>
    public class EffectSystemTests
    {
        private const int AtkBase = 50;
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects)
                if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        [Test]
        public void 지속버프_적용시_Stat_Current가_증가한다()
        {
            var asc = CreateAsc();
            asc.ApplyEffect(AtkBuffMultiplicative(durationMs: 5000, amount: 120));

            Assert.AreEqual(60, asc.GetAttribute(EGameplayAttribute.AttackPower).CurrentValue);
        }

        [Test]
        public void 지속버프_만료시_원래값으로_복원된다()
        {
            var asc = CreateAsc();
            asc.ApplyEffect(AtkBuffMultiplicative(durationMs: 1000, amount: 120));
            Assert.AreEqual(60, asc.GetAttribute(EGameplayAttribute.AttackPower).CurrentValue);

            asc.Tick(0.5f); // 0.5s — 아직 유지
            Assert.AreEqual(60, asc.GetAttribute(EGameplayAttribute.AttackPower).CurrentValue);

            asc.Tick(0.6f); // 누적 1.1s — 만료
            Assert.AreEqual(0, asc.ActiveEffects.Count);
            Assert.AreEqual(AtkBase, asc.GetAttribute(EGameplayAttribute.AttackPower).CurrentValue);
        }

        [Test]
        public void Stack정책_누적시_modifier가_스택수만큼_합산된다()
        {
            var asc = CreateAsc();
            var def = new GameplayEffectDefinition(
                "atk_stack", EEffectCategory.AttackPower, EDurationPolicy.Duration, 5000,
                new[] { GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, 10, EModifierType.Additive) },
                EStackPolicy.Stack, maxStacks: 3);

            asc.ApplyEffect(def);
            asc.ApplyEffect(def);
            asc.ApplyEffect(def);

            Assert.AreEqual(1, asc.ActiveEffects.Count, "Stack은 인스턴스를 늘리지 않고 stack 수만 올린다.");
            Assert.AreEqual(AtkBase + 30, asc.GetAttribute(EGameplayAttribute.AttackPower).CurrentValue);
        }

        [Test]
        public void None정책_재적용은_무시된다()
        {
            var asc = CreateAsc();
            var def = AtkBuffMultiplicative(durationMs: 5000, amount: 120); // 기본 None

            asc.ApplyEffect(def);
            asc.ApplyEffect(def);

            Assert.AreEqual(1, asc.ActiveEffects.Count);
        }

        [Test]
        public void 즉발효과는_Resource_HP를_영구_변경한다()
        {
            var asc = CreateAsc();
            var damage = new GameplayEffectDefinition(
                "hit", EEffectCategory.AttackPower, EDurationPolicy.Instant, 0,
                new[] { GameplayAttributeModifier.Create(EGameplayAttribute.Health, -30, EModifierType.Additive) });

            int instanceId = asc.ApplyEffect(damage);

            Assert.AreEqual(-1, instanceId, "즉발은 추적 인스턴스를 만들지 않는다.");
            Assert.AreEqual(0, asc.ActiveEffects.Count);
            Assert.AreEqual(70, asc.GetAttribute(EGameplayAttribute.Health).CurrentValue);
        }

        [Test]
        public void 서버권위_적용은_서버_InstanceId를_키로_쓰고_그_id로_제거된다()
        {
            var asc = CreateAsc();
            var def = AtkBuffMultiplicative(durationMs: 5000, amount: 120);

            asc.ApplyEffectAuthoritative(def, instanceId: 99, stacks: 1);

            Assert.AreEqual(1, asc.ActiveEffects.Count);
            Assert.AreEqual(99, asc.ActiveEffects[0].InstanceId, "클라가 생성한 id가 아니라 서버 InstanceId를 써야 한다.");
            Assert.AreEqual(60, asc.GetAttribute(EGameplayAttribute.AttackPower).CurrentValue);

            asc.RemoveEffect(99); // 서버 S_RemoveEffect 권위 제거
            Assert.AreEqual(0, asc.ActiveEffects.Count);
            Assert.AreEqual(AtkBase, asc.GetAttribute(EGameplayAttribute.AttackPower).CurrentValue);
        }

        [Test]
        public void 서버권위_같은_InstanceId_재적용은_중복없이_갱신된다()
        {
            var asc = CreateAsc();
            var def = AtkBuffMultiplicative(durationMs: 5000, amount: 120);

            asc.ApplyEffectAuthoritative(def, instanceId: 7);
            asc.ApplyEffectAuthoritative(def, instanceId: 7);

            Assert.AreEqual(1, asc.ActiveEffects.Count, "같은 서버 InstanceId 재수신은 멱등이어야 한다.");
        }

        [Test]
        public void 서버권위_HealthOverride는_카탈로그_고정값_대신_적용된다()
        {
            var asc = CreateAsc(); // Health 100/100
            var dmg = new GameplayEffectDefinition(
                "monster_attack_dmg", EEffectCategory.AttackPower, EDurationPolicy.Instant, 0,
                new[] { GameplayAttributeModifier.Create(EGameplayAttribute.Health, -30, EModifierType.Additive) });

            // 서버가 Defense 반영해 보낸 -7 을 적용(카탈로그 -30 무시) → 93.
            asc.ApplyEffectAuthoritative(dmg, instanceId: 5, stacks: 1, healthOverride: -7);

            Assert.AreEqual(93, asc.GetAttribute(EGameplayAttribute.Health).CurrentValue);
        }

        [Test]
        public void HealthOverride가_0이면_카탈로그_고정값을_그대로_적용한다()
        {
            var asc = CreateAsc();
            var dmg = new GameplayEffectDefinition(
                "x", EEffectCategory.AttackPower, EDurationPolicy.Instant, 0,
                new[] { GameplayAttributeModifier.Create(EGameplayAttribute.Health, -30, EModifierType.Additive) });

            asc.ApplyEffectAuthoritative(dmg, instanceId: 6, stacks: 1, healthOverride: 0);

            Assert.AreEqual(70, asc.GetAttribute(EGameplayAttribute.Health).CurrentValue); // 하위호환
        }

        // ── 헬퍼 ────────────────────────────────────────

        private AbilitySystemComponent CreateAsc()
        {
            var go = new GameObject("Combatant");
            _objects.Add(go);

            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.Attributes = new List<GameplayAttribute>
            {
                new(EGameplayAttribute.Health, 100, 100),                              // Resource
                new(EGameplayAttribute.AttackPower, AtkBase, 100000, EAttributeKind.Stat),
            };
            asc.InitializeAttributes();
            return asc;
        }

        private static GameplayEffectDefinition AtkBuffMultiplicative(int durationMs, int amount)
        {
            return new GameplayEffectDefinition(
                "atk_buff", EEffectCategory.AttackPower, EDurationPolicy.Duration, durationMs,
                new[] { GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, amount, EModifierType.Multiplicative) });
        }
    }
}
