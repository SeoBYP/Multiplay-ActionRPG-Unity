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
    /// ⓔ-1 사망 입력 게이트(PlayMode): HP 0 → State.Dead → PlayerCharacterAgent 가 공격 입력을 억제한다.
    ///
    /// FSM/Motor 리그 없이 **게이트 자체를 격리 검증**한다:
    ///   - TestableAgent 가 Start(FSM 초기화)를 건너뜀 → CurrentState=null → base.Update() 는 no-op.
    ///   - DriveUpdate() 가 실제 PlayerCharacterAgent.Update(게이트 코드)를 직접 구동.
    ///   - 프레임을 yield 하지 않아 컴포넌트 자동 Update/FixedUpdate 부작용 없음(SetActive 로 Awake 만 동기 실행).
    /// </summary>
    public class PlayerDeathGateTests
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
        public IEnumerator 살아있으면_공격입력이_OnAttackPerformed를_발동한다()
        {
            var (agent, input, _) = BuildAgent();

            bool attacked = false;
            agent.OnAttackPerformed += _ => attacked = true;

            input.AttackPressed = true;
            agent.DriveUpdate();

            Assert.IsTrue(attacked, "생존 시 공격 입력은 OnAttackPerformed 를 발동해야 한다.");
            yield break;
        }

        [UnityTest]
        public IEnumerator HP0이면_State_Dead가_세워지고_공격입력이_억제된다()
        {
            var (agent, input, asc) = BuildAgent();

            bool attacked = false;
            agent.OnAttackPerformed += _ => attacked = true;

            // 치명타 → HP 0 → 에이전트가 ASC.OnAttributeChanged 로 State.Dead 를 세운다.
            asc.ApplyEffect(new GameplayEffectDefinition(
                "lethal", EEffectCategory.AttackPower, EDurationPolicy.Instant, 0,
                new[] { GameplayAttributeModifier.Create(EGameplayAttribute.Health, -999, EModifierType.Additive) }));

            Assert.AreEqual(0, asc.GetAttribute(EGameplayAttribute.Health).CurrentValue);
            Assert.IsTrue(asc.HasTag(GameplayTags.Dead), "HP 0 이면 State.Dead 가 세워져야 한다.");

            input.AttackPressed = true;
            agent.DriveUpdate();

            Assert.IsFalse(attacked, "사망(다운) 상태에서는 공격 입력이 억제돼야 한다.");
            yield break;
        }

        // ── 리그 ──────────────────────────────────────────────

        private (TestableAgent agent, FakeInput input, AbilitySystemComponent asc) BuildAgent()
        {
            var go = new GameObject("DeathGateAgent");
            go.SetActive(false);                          // Awake 를 구성 완료 후 한 번에 돌리려고 비활성으로 시작
            _objects.Add(go);

            var input = go.AddComponent<FakeInput>();
            var agent = go.AddComponent<TestableAgent>(); // RequireComponent 로 ASC·Motor·Animations 등 자동 추가
            var asc = go.GetComponent<AbilitySystemComponent>();
            asc.Attributes = new List<GameplayAttribute> { new(EGameplayAttribute.Health, 100, 100) };

            go.SetActive(true);                           // Awake: 에이전트가 ASC.OnAttributeChanged 구독. Start 는 override 로 스킵.
            return (agent, input, asc);
        }

        /// <summary>FSM 없이 게이트만 구동하는 테스트 서브클래스.</summary>
        private sealed class TestableAgent : PlayerCharacterAgent
        {
            protected override void Start() { /* FSM 초기화 스킵 → CurrentState=null → base.Update() no-op */ }
            public void DriveUpdate() => Update();
        }

        /// <summary>공격 입력을 테스트가 제어하는 가짜 입력원(Component).</summary>
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
