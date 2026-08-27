using System.Collections.Generic;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Tests.EditMode.GamePlayAbilitySystem
{
    /// <summary>
    /// ⓒ/ⓔ-1: ASC GameplayTag 글루 — 직접 태그(사망 State.Dead) + 활성 Effect.GrantedTags 합산.
    /// 사망 입력 게이트(PlayerCharacterAgent)가 폴링하는 HasTag 의 계약을 박제한다.
    /// </summary>
    public class AbilitySystemTagTests
    {
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects)
                if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        [Test]
        public void 직접_태그를_추가하면_HasTag가_참이고_제거하면_거짓이다()
        {
            var asc = CreateAsc();

            Assert.IsFalse(asc.HasTag(GameplayTags.Dead));

            asc.AddTag(GameplayTags.Dead);
            Assert.IsTrue(asc.HasTag(GameplayTags.Dead));

            asc.RemoveTag(GameplayTags.Dead);
            Assert.IsFalse(asc.HasTag(GameplayTags.Dead));
        }

        [Test]
        public void 무효_빈태그는_보유되지_않는다()
        {
            var asc = CreateAsc();

            asc.AddTag("");
            Assert.IsFalse(asc.HasTag(""));
        }

        [Test]
        public void HasTag는_활성_Effect의_GrantedTags를_합산한다()
        {
            var asc = CreateAsc();
            var stun = new GameplayEffectDefinition(
                "stun", EEffectCategory.MoveSpeed, EDurationPolicy.Duration, 1000,
                new[] { GameplayAttributeModifier.Create(EGameplayAttribute.MoveSpeed, 0, EModifierType.Multiplicative) },
                grantedTags: new GameplayTag[] { "State.Stunned" });

            Assert.IsFalse(asc.HasTag("State.Stunned"));

            asc.ApplyEffect(stun);
            Assert.IsTrue(asc.HasTag("State.Stunned"), "활성 Effect 동안 GrantedTags 를 보유한다.");

            asc.Tick(1.1f); // 만료
            Assert.IsFalse(asc.HasTag("State.Stunned"), "만료되면 GrantedTags 도 사라진다.");
        }

        [Test]
        public void 직접태그와_GrantedTags는_독립적으로_동작한다()
        {
            var asc = CreateAsc();
            asc.AddTag(GameplayTags.Dead); // 직접

            var stun = new GameplayEffectDefinition(
                "stun", EEffectCategory.MoveSpeed, EDurationPolicy.Duration, 1000,
                new[] { GameplayAttributeModifier.Create(EGameplayAttribute.MoveSpeed, 0, EModifierType.Multiplicative) },
                grantedTags: new GameplayTag[] { "State.Stunned" });
            asc.ApplyEffect(stun);

            Assert.IsTrue(asc.HasTag(GameplayTags.Dead));
            Assert.IsTrue(asc.HasTag("State.Stunned"));

            asc.Tick(1.1f); // 스턴 만료 — 사망 직접태그는 유지
            Assert.IsTrue(asc.HasTag(GameplayTags.Dead), "직접 부여 태그는 Effect 만료와 무관하게 유지된다.");
            Assert.IsFalse(asc.HasTag("State.Stunned"));
        }

        private GasComponent CreateAsc()
        {
            var go = new GameObject("Combatant");
            _objects.Add(go);

            var asc = go.AddComponent<GasComponent>();
            asc.Attributes = new List<GameplayAttribute>
            {
                new(EGameplayAttribute.Health, 100, 100),
                new(EGameplayAttribute.MoveSpeed, 100, 100000, EAttributeKind.Stat),
            };
            asc.InitializeAttributes();
            return asc;
        }
    }
}
