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
    /// Action 이동잠금(Rooted, PlayMode): 공격/상호작용 발동 시 <see cref="ActionTags.Rooted"/> 가 부여되고,
    /// 지속시간 경과 후 Update 가 자동 해제한다. GroundState 가 이 태그를 폴링해 수평 이동을 0으로 만든다(Slow 게이트와 동일 패턴).
    ///
    /// 격리 방식은 CcGateTests/PlayerDeathGateTests 와 동일: TestableAgent 가 FSM Start 를 건너뛰어 게이트만 구동.
    /// </summary>
    public class ActionRootTests
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
        public IEnumerator 공격하면_이동잠금_Rooted가_부여되고_지속후_해제된다()
        {
            var (agent, input, asc) = BuildAgent();
            Assert.IsFalse(asc.HasTag(ActionTags.Rooted), "발동 전에는 Rooted 태그가 없어야 한다.");

            input.AttackPressed = true;
            agent.DriveUpdate();
            Assert.IsTrue(asc.HasTag(ActionTags.Rooted), "공격 발동 시 이동잠금(Rooted)이 부여돼야 한다.");

            // 스킬 데이터 미주입 → fallback 0.4s. 실시간 경과 후 Update 가 만료 해제.
            yield return new WaitForSeconds(0.5f);
            agent.DriveUpdate();
            Assert.IsFalse(asc.HasTag(ActionTags.Rooted), "지속시간 경과 후 Rooted 가 자동 해제돼야 한다.");
        }

        // ── 리그 (CcGateTests 와 동일 구조) ──────────────

        private (TestableAgent agent, FakeInput input, AbilitySystemComponent asc) BuildAgent()
        {
            var go = new GameObject("ActionRootAgent");
            go.SetActive(false);
            _objects.Add(go);

            var input = go.AddComponent<FakeInput>();
            var agent = go.AddComponent<TestableAgent>(); // RequireComponent 로 ASC·Motor·Animations 등 자동 추가
            var asc = go.GetComponent<AbilitySystemComponent>();
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
