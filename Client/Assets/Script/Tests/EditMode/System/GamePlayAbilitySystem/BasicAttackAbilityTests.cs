using System.Collections.Generic;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Tests.EditMode.GamePlayAbilitySystem
{
    public class BasicAttackAbilityTests
    {
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obj in _objects)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }

            _objects.Clear();
        }

        [Test]
        public void 일반공격_어빌리티는_대상_체력을_감소시킨다()
        {
            AbilitySystemComponent attacker = CreateAbilitySystem("Attacker", 100);
            AbilitySystemComponent target = CreateAbilitySystem("Target", 100);
            BasicAttackAbility ability = attacker.GrantAbility(new BasicAttackAbility(10));

            bool activated = attacker.TryActivateAbility(
                ability,
                new AbilityActivationContext(new List<AbilitySystemComponent> { target }));

            Assert.That(activated, Is.True);
            Assert.That(target.GetAttribute(EGameplayAttribute.Health).CurrentValue, Is.EqualTo(90));
        }

        [Test]
        public void 일반공격_어빌리티는_자기자신만_대상일_때_발동하지_않는다()
        {
            AbilitySystemComponent attacker = CreateAbilitySystem("Attacker", 100);
            BasicAttackAbility ability = attacker.GrantAbility(new BasicAttackAbility(10));

            bool activated = attacker.TryActivateAbility(
                ability,
                new AbilityActivationContext(new List<AbilitySystemComponent> { attacker }));

            Assert.That(activated, Is.False);
            Assert.That(attacker.GetAttribute(EGameplayAttribute.Health).CurrentValue, Is.EqualTo(100));
        }

        [Test]
        public void 일반공격_어빌리티는_대상이_없으면_발동하지_않는다()
        {
            AbilitySystemComponent attacker = CreateAbilitySystem("Attacker", 100);
            BasicAttackAbility ability = attacker.GrantAbility(new BasicAttackAbility(10));

            bool activated = attacker.TryActivateAbility(
                ability,
                new AbilityActivationContext(new List<AbilitySystemComponent>()));

            Assert.That(activated, Is.False);
        }

        private AbilitySystemComponent CreateAbilitySystem(string objectName, int health)
        {
            GameObject obj = new(objectName);
            _objects.Add(obj);

            AbilitySystemComponent abilitySystem = obj.AddComponent<AbilitySystemComponent>();
            abilitySystem.Attributes = new List<GameplayAttribute>
            {
                new(EGameplayAttribute.Health, health, health)
            };
            abilitySystem.InitializeAttributes();

            return abilitySystem;
        }
    }
}
