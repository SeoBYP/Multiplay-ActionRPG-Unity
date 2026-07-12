using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Gameplay;
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

        [UnityTest]
        public IEnumerator 상호작용하면_이동잠금_Rooted가_부여된다()
        {
            var (agent, input, asc) = BuildAgent(withDetector: true);

            // 감지 구체(정면 1m, 높이 1m, 반경 0.5) 안에 IInteractable 콜라이더 배치.
            var itemGo = new GameObject("FakeItem");
            _objects.Add(itemGo);
            itemGo.transform.position = agent.transform.position + Vector3.up * 1f + agent.transform.forward * 1f;
            itemGo.AddComponent<SphereCollider>().radius = 0.3f;
            var item = itemGo.AddComponent<FakeInteractable>();
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(asc.HasTag(ActionTags.Rooted), "발동 전에는 Rooted 태그가 없어야 한다.");

            input.InteractPressed = true;
            agent.DriveUpdate();

            Assert.IsTrue(item.Interacted, "감지된 대상의 Interact 가 호출돼야 한다(경로 확인).");
            Assert.IsTrue(asc.HasTag(ActionTags.Rooted), "상호작용(줍기) 시에도 이동잠금(Rooted)이 부여돼야 한다.");
        }

        [UnityTest]
        public IEnumerator Rooted_태그가_있으면_GroundState가_수평이동을_막는다()
        {
            var go = new GameObject("RootedMoveAgent");
            go.SetActive(false);
            _objects.Add(go);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.5f; cc.center = new Vector3(0f, 1f, 0f);
            var motor  = go.AddComponent<CharacterMotor>();
            var ground = go.AddComponent<GroundedDetector>();
            var anims  = go.AddComponent<CharacterAgentAnimations>();
            var asc    = go.AddComponent<AbilitySystemComponent>();
            asc.Attributes = new List<GameplayAttribute> { new(EGameplayAttribute.Health, 100, 100) };
            go.SetActive(true);

            var settings = new LocomotionSettings();
            motor.Construct(settings); // [Inject] 메서드 직접 호출
            var input = new FakeMoveSource(new Vector2(0f, 1f)); // 계속 전진 입력
            var state = new GroundState(motor, ground, anims, input, settings, asc);
            state.Enter();

            // (1) Rooted 없음 → 수평 이동해야 한다.
            var start = go.transform.position;
            for (int i = 0; i < 20; i++) { state.Update(0.02f); yield return null; }
            var moved = go.transform.position;
            float freeHoriz = new Vector2(moved.x - start.x, moved.z - start.z).magnitude;
            Assert.Greater(freeHoriz, 0.01f, $"Rooted 가 없으면 수평 이동해야 한다(실측 {freeHoriz:F3}m).");

            // (2) Rooted 부여 → 수평 이동이 멈춰야 한다(중력=수직은 허용).
            asc.AddTag(ActionTags.Rooted);
            var before = go.transform.position;
            for (int i = 0; i < 20; i++) { state.Update(0.02f); yield return null; }
            var after = go.transform.position;
            float rootedHoriz = new Vector2(after.x - before.x, after.z - before.z).magnitude;
            Assert.Less(rootedHoriz, 0.001f, $"Rooted 중에는 수평 이동이 없어야 한다(실측 {rootedHoriz:F4}m).");
        }

        [UnityTest]
        public IEnumerator Rooted_태그가_있으면_공중_FallState도_수평이동을_막는다()
        {
            var go = new GameObject("RootedAirAgent");
            go.SetActive(false);
            _objects.Add(go);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.5f; cc.center = new Vector3(0f, 1f, 0f);
            var motor = go.AddComponent<CharacterMotor>();
            var anims = go.AddComponent<CharacterAgentAnimations>();
            var asc   = go.AddComponent<AbilitySystemComponent>();
            asc.Attributes = new List<GameplayAttribute> { new(EGameplayAttribute.Health, 100, 100) };
            go.SetActive(true);
            go.transform.position = new Vector3(0f, 5f, 0f); // 공중

            var settings = new LocomotionSettings();
            motor.Construct(settings);
            var input = new FakeMoveSource(new Vector2(1f, 0f)); // 우측 에어컨트롤 입력
            var fall = new FallState(motor, anims, input, settings, asc);
            fall.Enter();

            // Rooted 부여 → 낙하(수직)는 되지만 수평(X/Z)은 0 이어야 한다.
            asc.AddTag(ActionTags.Rooted);
            var before = go.transform.position;
            for (int i = 0; i < 20; i++) { fall.Update(0.02f); yield return null; }
            var after = go.transform.position;

            float horiz = new Vector2(after.x - before.x, after.z - before.z).magnitude;
            Assert.Less(horiz, 0.001f, $"Rooted 중엔 공중에서도 수평 이동이 없어야 한다(실측 {horiz:F4}m).");
            Assert.Less(after.y, before.y - 0.05f, "중력에 의한 낙하(수직 이동)는 유지돼야 한다.");
        }

        [UnityTest]
        public IEnumerator 사망하면_Dead_애니_부활하면_로코모션으로_복귀한다()
        {
            var go = new GameObject("ReviveAnimAgent");
            go.SetActive(false);
            _objects.Add(go);
            go.AddComponent<FakeInput>();

            // Animator 자식(실제 PlayerController) — CharacterAgentAnimations 가 GetComponentInChildren 로 찾는다.
            var model = new GameObject("Model");
            model.transform.SetParent(go.transform, false);
            var animator = model.AddComponent<Animator>();
#if UNITY_EDITOR
            animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.RuntimeAnimatorController>(
                "Assets/GameResources/Animations/Player/PlayerController.controller");
#endif
            animator.avatar = null;

            var agent = go.AddComponent<TestableAgent>();
            var asc = go.GetComponent<AbilitySystemComponent>();
            asc.Attributes = new List<GameplayAttribute> { new(EGameplayAttribute.Health, 100, 100) };

            // CharacterAgentAnimations 트리거명(런타임 AddComponent 라 프리팹값 없음) — Awake 전에 세팅.
            var caa = go.GetComponent<CharacterAgentAnimations>();
            void SetName(string field, string val) => typeof(CharacterAgentAnimations)
                .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(caa, val);
            SetName("m_animationDeathTrigger", "Dead");
            SetName("m_animationReviveTrigger", "Revive");

            go.SetActive(true); // Awake: CAA 애니 캐시 + agent OnAttributeChanged 구독
            animator.Rebind();
            animator.Update(0f);
            Assume.That(animator.runtimeAnimatorController, Is.Not.Null, "PlayerController 로드 실패(에디터 외 실행)");

            // 사망: HP→0 → OnAttributeChanged → SetTrigger(Dead) → AnyState→Dead
            asc.GetAttribute(EGameplayAttribute.Health).SetCurrent(0);
            for (int i = 0; i < 12; i++) { animator.Update(0.1f); yield return null; }
            Assert.IsTrue(asc.HasTag(GameplayTags.Dead), "HP0 이면 State.Dead 태그가 서야 한다.");
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Dead"), "사망 시 Animator 가 Dead 상태여야 한다.");

            // 부활: agent.Revive → SetTrigger(Revive) → Dead→GetUp(기상) → 재생 후 로코모션
            agent.Revive(Vector3.zero);
            for (int i = 0; i < 4; i++) { animator.Update(0.1f); yield return null; }
            Assert.IsFalse(asc.HasTag(GameplayTags.Dead), "부활 후 State.Dead 태그가 해제돼야 한다.");
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("GetUp"), "부활 직후 기상(GetUp) 애니가 재생돼야 한다.");

            // 기상 클립(2.67s) 재생 후 로코모션(Idle Walk Run)으로 복귀.
            for (int i = 0; i < 40; i++) { animator.Update(0.1f); yield return null; }
            Assert.IsFalse(animator.GetCurrentAnimatorStateInfo(0).IsName("GetUp"), "기상 후 GetUp 을 벗어나 로코모션으로 복귀해야 한다.");
            Assert.IsFalse(animator.GetCurrentAnimatorStateInfo(0).IsName("Dead"), "부활 후 Animator 가 Dead 를 벗어나야 한다.");
        }

        // ── 리그 (CcGateTests 와 동일 구조) ──────────────

        private (TestableAgent agent, FakeInput input, AbilitySystemComponent asc) BuildAgent(bool withDetector = false)
        {
            var go = new GameObject("ActionRootAgent");
            go.SetActive(false);
            _objects.Add(go);

            var input = go.AddComponent<FakeInput>();
            if (withDetector)
            {
                // 실제 InteractionDetector 사용 — 감지 레이어는 private SerializeField 라 리플렉션으로 전체 허용.
                var det = go.AddComponent<InteractionDetector>();
                typeof(InteractionDetector)
                    .GetField("m_detectionLayer", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(det, (LayerMask)(-1));
            }
            var agent = go.AddComponent<TestableAgent>(); // RequireComponent 로 ASC·Motor·Animations 등 자동 추가
            var asc = go.GetComponent<AbilitySystemComponent>();
            asc.Attributes = new List<GameplayAttribute> { new(EGameplayAttribute.Health, 100, 100) };

            go.SetActive(true);
            return (agent, input, asc);
        }

        private sealed class FakeInteractable : MonoBehaviour, IInteractable
        {
            public bool Interacted;
            public void Interact(GameObject interactor) => Interacted = true;
        }

        /// <summary>GroundState 직접 구동용 — 매 프레임 동일한 이동 입력을 낸다.</summary>
        private sealed class FakeMoveSource : ICharacterInputSource
        {
            private readonly CharacterInputFrame _frame;
            public FakeMoveSource(Vector2 move) => _frame = default(CharacterInputFrame).WithMove(move);
            public CharacterInputFrame Current => _frame;
            public bool ConsumeJumpPressed() => false;
            public bool ConsumeDodgePressed() => false;
            public bool ConsumeInteractPressed() => false;
            public bool ConsumeAttackPressed() => false;
            public bool ConsumeHeavyAttackPressed() => false;
            public bool ConsumeLockOnPressed() => false;
        }

        private sealed class TestableAgent : PlayerCharacterAgent
        {
            protected override void Start() { /* FSM 초기화 스킵 → base.Update() no-op, 게이트만 구동 */ }
            public void DriveUpdate() => Update();
        }

        private sealed class FakeInput : MonoBehaviour, ICharacterInputSource
        {
            public bool AttackPressed;
            public bool InteractPressed;
            public CharacterInputFrame Current => default;
            public bool ConsumeJumpPressed() => false;
            public bool ConsumeDodgePressed() => false;
            public bool ConsumeInteractPressed()
            {
                var v = InteractPressed;
                InteractPressed = false;
                return v;
            }
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
