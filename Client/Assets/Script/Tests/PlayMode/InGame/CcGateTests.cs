using System.Collections;
using System.Collections.Generic;
using Game.Gameplay.Character;
using Game.Gameplay.Character.Input;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// 2.6.2 상태이상(CC) 입력 게이트(PlayMode): State.Stun 동안 PlayerCharacterAgent 가 공격 입력을 억제하고,
    /// Duration 효과가 ASC.Tick 으로 만료되면 자동 해제돼 다시 입력이 통한다(사망 게이트와 달리 자동 복구).
    ///
    /// 격리 방식은 PlayerDeathGateTests 와 동일: TestableAgent 가 FSM Start 를 건너뛰어 게이트만 구동.
    /// </summary>
    public class CcGateTests
    {
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects)
                if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        [UnityTest]
        public IEnumerator 스턴_중에는_공격입력이_억제되고_만료되면_재개된다()
        {
            var (agent, input, asc) = BuildAgent();

            bool attacked = false;
            agent.OnAttackPerformed += _ => attacked = true;

            // 스턴 부여 — Duration 1500ms, GrantedTags=[State.Stun] (modifier 없음 = 순수 상태태그).
            asc.ApplyEffect(new GameplayEffectDefinition(
                "test_stun", EEffectCategory.Defense, EDurationPolicy.Duration, 1500,
                new List<GameplayAttributeModifier>(),
                grantedTags: new GameplayTag[] { GameplayTags.Stun }));
            Assert.IsTrue(asc.HasTag(GameplayTags.Stun), "스턴 효과 적용 시 State.Stun 태그가 있어야 한다.");

            input.AttackPressed = true;
            agent.DriveUpdate();
            Assert.IsFalse(attacked, "스턴 중에는 공격 입력이 억제돼야 한다.");

            // 효과 만료 — ASC.Tick 으로 1.6s 경과 → 태그 자동 해제.
            asc.Tick(1.6f);
            Assert.IsFalse(asc.HasTag(GameplayTags.Stun), "Duration 만료 후 State.Stun 이 해제돼야 한다.");

            input.AttackPressed = true;
            agent.DriveUpdate();
            Assert.IsTrue(attacked, "스턴 해제 후에는 공격 입력이 다시 통해야 한다.");
            yield break;
        }

        // ── 리그 (PlayerDeathGateTests 와 동일 구조) ──────────────

        private (TestableAgent agent, FakeInput input, GasComponent asc) BuildAgent()
        {
            var go = new GameObject("CcGateAgent");
            go.SetActive(false);
            _objects.Add(go);

            var input = go.AddComponent<FakeInput>();
            var agent = go.AddComponent<TestableAgent>(); // RequireComponent 로 ASC·Motor·Animations 등 자동 추가
            var asc = go.GetComponent<GasComponent>();
            asc.Attributes = new List<GameplayAttribute> { new(EGameplayAttribute.Health, 100, 100) };

            go.SetActive(true);
            return (agent, input, asc);
        }

        private sealed class TestableAgent : PlayerCharacterAgent
        {
            protected override void Start() { /* FSM 초기화 스킵 → base.Update() no-op, 게이트만 구동 */ }
            public void DriveUpdate() => Update();
        }

        private sealed class FakeInput : MonoBehaviour, ICharacterInputSource
        {
            public bool AttackPressed;
            public CharacterInputFrame Current => default;
            public bool ConsumeJumpPressed() => false;
            public bool ConsumeDodgePressed() => false;
            public bool ConsumeInteractPressed() => false;
            public bool ConsumeAttackPressed()
            {
                var v = AttackPressed;
                AttackPressed = false;
                return v;
            }
            public bool ConsumeHeavyAttackPressed() => false;
            public bool ConsumeLockOnPressed() => false;
        }
    }
}
