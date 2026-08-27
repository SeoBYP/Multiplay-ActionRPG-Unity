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
            var asc    = go.AddComponent<GasComponent>();
            asc.Attributes = new List<GameplayAttribute> { new(EGameplayAttribute.Health, 100, 100) };
            go.SetActive(true);

            var settings = new LocomotionSettings();
            motor.Construct(settings); // [Inject] 메서드 직접 호출
            var input = new FakeMoveSource(new Vector2(0f, 1f)); // 계속 전진 입력
            var state = new GroundState(motor, ground, anims, input, settings, asc);
            state.Enter();

            // ⚠️ 고정 프레임 수로 돌리면 안 된다: `CharacterMotor.Move` 의 변위는 실제 `Time.deltaTime` 기반인데
            // 테스트가 로직에만 고정 dt 를 넣으면, 에디터가 빠르게 렌더링할 때 20프레임 = 실시간 수 ms 라
            // 거의 안 움직여 "Rooted 가 아닌데 이동 없음"으로 오판한다(실측 0.0015m 로 이 테스트가 깨져 있었다).
            // → 실시간 예산으로 돌리고, 상태에도 그 프레임의 실제 dt 를 넘겨 두 계층의 시간축을 일치시킨다.
            var start = go.transform.position;
            yield return DriveFor(state, 0.5f);
            var moved = go.transform.position;
            float freeHoriz = new Vector2(moved.x - start.x, moved.z - start.z).magnitude;
            Assert.Greater(freeHoriz, 0.01f, $"Rooted 가 없으면 수평 이동해야 한다(실측 {freeHoriz:F3}m).");

            // (2) Rooted 부여 → 수평 이동이 멈춰야 한다(중력=수직은 허용).
            asc.AddTag(ActionTags.Rooted);
            var before = go.transform.position;
            yield return DriveFor(state, 0.5f);
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
            var asc   = go.AddComponent<GasComponent>();
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
                "Assets/GameResources/Animations/Player/PlayerController_ARPG.controller");
#endif
            animator.avatar = null;

            var agent = go.AddComponent<TestableAgent>();
            var asc = go.GetComponent<GasComponent>();
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
            asc.SetCurrent(EGameplayAttribute.Health, 0);
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

        [UnityTest]
        public IEnumerator 피해를_입고_살아있으면_Hit_애니가_재생된다()
        {
            var rig = BuildAnimRig(("m_animationDeathTrigger", "Dead"), ("m_animationHitTrigger", "Hit"));

            // 첫 통지는 기준값만 세운다(피격 아님) — 그 다음 감소부터가 피격.
            rig.asc.SetCurrent(EGameplayAttribute.Health, 90);
            yield return null;
            Assert.IsFalse(rig.animator.GetCurrentAnimatorStateInfo(0).IsName("Hit"),
                "첫 HP 통지만으로는 피격 애니가 나오면 안 된다.");

            rig.asc.SetCurrent(EGameplayAttribute.Health, 70);
            for (int i = 0; i < 6; i++) { rig.animator.Update(0.05f); yield return null; }
            Assert.IsTrue(rig.animator.GetCurrentAnimatorStateInfo(0).IsName("Hit"),
                "HP 가 줄면 피격(Hit) 애니로 전이해야 한다.");
        }

        [UnityTest]
        public IEnumerator 사망하는_피해는_Hit이_아니라_Dead로_간다()
        {
            var rig = BuildAnimRig(("m_animationDeathTrigger", "Dead"), ("m_animationHitTrigger", "Hit"));

            rig.asc.SetCurrent(EGameplayAttribute.Health, 90);
            yield return null;
            rig.asc.SetCurrent(EGameplayAttribute.Health, 0); // 치명타
            for (int i = 0; i < 12; i++) { rig.animator.Update(0.1f); yield return null; }

            Assert.IsTrue(rig.animator.GetCurrentAnimatorStateInfo(0).IsName("Dead"),
                "죽는 피해는 Hit 이 아니라 Dead 로 가야 한다(다운 포즈가 피격에 밀리면 안 된다).");
        }

        [UnityTest]
        public IEnumerator 회피_방향이_애니_파라미터로_전달된다()
        {
            var rig = BuildAnimRig(
                ("m_animationDodgeTrigger", "Dodge"),
                ("m_animationDodgeXFloat", "DodgeX"),
                ("m_animationDodgeYFloat", "DodgeY"));

            rig.go.transform.rotation = Quaternion.identity; // 정면 = +Z
            var motor = rig.go.GetComponent<CharacterMotor>();
            var dodge = new DodgeDriver(motor, rig.asc, rig.caa, new LocomotionSettings());

            dodge.Begin(Vector3.right, Time.time); // 월드 오른쪽 = 캐릭터 로컬 오른쪽
            yield return null;
            Assert.AreEqual(1f, rig.animator.GetFloat("DodgeX"), 0.01f, "오른쪽 회피면 DodgeX = +1");
            Assert.AreEqual(0f, rig.animator.GetFloat("DodgeY"), 0.01f, "오른쪽 회피면 DodgeY = 0");

            dodge.Cancel();
            dodge.Begin(Vector3.back, Time.time); // 뒤로 구르기
            yield return null;
            Assert.AreEqual(-1f, rig.animator.GetFloat("DodgeY"), 0.01f, "뒤 회피면 DodgeY = -1");
            Assert.AreEqual(0f, rig.animator.GetFloat("DodgeX"), 0.01f, "뒤 회피면 DodgeX = 0");
        }

        [UnityTest]
        public IEnumerator 락온이_아니어도_이동방향이_8방향_파라미터로_나온다()
        {
            var rig = BuildAnimRig(
                ("m_animationSpeedFloat", "Speed"),
                ("m_animationMoveXFloat", "MoveX"),
                ("m_animationMoveYFloat", "MoveY"),
                ("m_animationStrafeBool", "Strafe"));

            var motor = rig.go.GetComponent<CharacterMotor>();
            var ground = rig.go.GetComponent<GroundedDetector>();
            var settings = new LocomotionSettings();
            motor.Construct(settings);

            // 오른쪽 입력 — 몸은 카메라(=여기선 자기 forward)를 향한 채 게걸음이어야 한다.
            var state = new GroundState(motor, ground, rig.caa, new FakeMoveSource(new Vector2(1f, 0f)), settings, rig.asc);
            state.Enter();
            yield return DriveFor(state, 0.4f);

            Assert.IsTrue(rig.animator.GetBool("Strafe"),
                "락온이 아니어도 8방향 블렌드(Strafe)를 써야 한다 — 예전엔 락온 때만 켜져 옆으로 가도 전진 클립이 나왔다.");
            Assert.Greater(rig.animator.GetFloat("MoveX"), 1f,
                $"오른쪽 입력이면 MoveX 가 m/s 단위 양수여야 한다(실측 {rig.animator.GetFloat("MoveX"):F2}).");
            Assert.Less(Mathf.Abs(rig.animator.GetFloat("MoveY")), 0.5f,
                "정면 성분은 거의 0 이어야 한다(순수 옆걸음).");
        }

        [UnityTest]
        public IEnumerator 방향을_꺾어도_8방향_파라미터가_한번에_튀지_않는다()
        {
            // 회귀: 방향 파라미터를 감쇠 없이 꽂으면 좌→우 전환 때 좌표가 한 프레임에 4.6(=2.3×2) 점프해
            // 블렌드가 좌측 클립 → 우측 클립으로 툭 바뀐다(사용자 피드백: "좌로 걷다 우로 바꿀 때 블렌딩이 안 된다").
            var rig = BuildAnimRig(
                ("m_animationSpeedFloat", "Speed"),
                ("m_animationMoveXFloat", "MoveX"),
                ("m_animationMoveYFloat", "MoveY"),
                ("m_animationStrafeBool", "Strafe"));

            var motor = rig.go.GetComponent<CharacterMotor>();
            var ground = rig.go.GetComponent<GroundedDetector>();
            var settings = new LocomotionSettings();
            motor.Construct(settings);

            var input = new MutableMoveSource(new Vector2(-1f, 0f)); // 좌측 게걸음
            var state = new GroundState(motor, ground, rig.caa, input, settings, rig.asc);
            state.Enter();
            yield return DriveFor(state, 0.5f); // 전진 정착

            float beforeX = rig.animator.GetFloat("MoveX");
            Assert.Less(beforeX, -1f, $"좌측 이동 중이면 MoveX 가 음수여야 한다(실측 {beforeX:F2}).");

            // 좌 → 우 급전환(가장 급격한 케이스) — 프레임별 좌표 변화량을 잰다.
            input.Move = new Vector2(1f, 0f);
            var prev = new Vector2(rig.animator.GetFloat("MoveX"), rig.animator.GetFloat("MoveY"));
            float maxStep = 0f, elapsed = 0f;
            while (elapsed < 0.5f)
            {
                state.Update(Time.deltaTime);
                yield return null;
                elapsed += Time.deltaTime;
                var now = new Vector2(rig.animator.GetFloat("MoveX"), rig.animator.GetFloat("MoveY"));
                maxStep = Mathf.Max(maxStep, (now - prev).magnitude);
                prev = now;
            }

            Debug.Log($"[감쇠측정] 좌→우 전환 최대 프레임 변화량 = {maxStep:F3} (감쇠 {settings.MoveBlendDamp}s)");
            Assert.Less(maxStep, 1.5f,
                $"방향 전환 시 블렌드 좌표가 한 프레임에 튀면 안 된다(실측 최대 {maxStep:F2}, 감쇠 없으면 4.6).");
            Assert.Greater(rig.animator.GetFloat("MoveX"), 1f,
                "감쇠는 지연일 뿐 — 0.5s 안에는 새 방향으로 수렴해야 한다.");
        }

        /// <summary>실제 컨트롤러가 붙은 Animator + ASC + Motor 리그. 파라미터명은 프리팹 값이 없으므로 주입한다.</summary>
        private (GameObject go, Animator animator, GasComponent asc, CharacterAgentAnimations caa)
            BuildAnimRig(params (string field, string value)[] names)
        {
            var go = new GameObject("AnimRigAgent");
            go.SetActive(false);
            _objects.Add(go);
            go.AddComponent<FakeInput>();

            var model = new GameObject("Model");
            model.transform.SetParent(go.transform, false);
            var animator = model.AddComponent<Animator>();
#if UNITY_EDITOR
            animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.RuntimeAnimatorController>(
                "Assets/GameResources/Animations/Player/PlayerController_ARPG.controller");
#endif
            animator.avatar = null;

            go.AddComponent<TestableAgent>(); // RequireComponent 로 ASC·Motor·Animations 자동 추가
            var asc = go.GetComponent<GasComponent>();
            asc.Attributes = new List<GameplayAttribute> { new(EGameplayAttribute.Health, 100, 100) };

            var caa = go.GetComponent<CharacterAgentAnimations>();
            foreach (var n in names)
                typeof(CharacterAgentAnimations)
                    .GetField(n.field, BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(caa, n.value);

            go.SetActive(true); // Awake: 애니 캐시 + agent 의 HP 구독
            animator.Rebind();
            animator.Update(0f);
            Assume.That(animator.runtimeAnimatorController, Is.Not.Null, "PlayerController_ARPG 로드 실패(에디터 외 실행)");
            return (go, animator, asc, caa);
        }

        // ── 리그 (CcGateTests 와 동일 구조) ──────────────

        private (TestableAgent agent, FakeInput input, GasComponent asc) BuildAgent(bool withDetector = false)
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
            var asc = go.GetComponent<GasComponent>();
            asc.Attributes = new List<GameplayAttribute> { new(EGameplayAttribute.Health, 100, 100) };

            go.SetActive(true);
            return (agent, input, asc);
        }

        /// <summary>
        /// 상태를 <paramref name="seconds"/> 만큼 <b>실시간</b> 구동한다. 프레임 수가 아니라 시간으로 도는 이유:
        /// <see cref="CharacterMotor.Move"/> 의 변위가 <c>Time.deltaTime</c> 기반이라, 고정 프레임 수로 돌리면
        /// 결과가 에디터 FPS 에 좌우된다(빠를수록 덜 움직인다). 상태에도 같은 프레임의 실제 dt 를 넘겨 시간축을 맞춘다.
        /// </summary>
        private static IEnumerator DriveFor(State state, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                state.Update(Time.deltaTime);
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        private sealed class FakeInteractable : MonoBehaviour, IInteractable
        {
            public bool Interacted;
            public void Interact(GameObject interactor) => Interacted = true;
        }

        /// <summary>테스트 중 방향을 바꿀 수 있는 입력 소스(급전환 재현용).</summary>
        private sealed class MutableMoveSource : ICharacterInputSource
        {
            public Vector2 Move;
            public MutableMoveSource(Vector2 move) => Move = move;
            public CharacterInputFrame Current => default(CharacterInputFrame).WithMove(Move);
            public bool ConsumeJumpPressed() => false;
            public bool ConsumeDodgePressed() => false;
            public bool ConsumeInteractPressed() => false;
            public bool ConsumeAttackPressed() => false;
            public bool ConsumeHeavyAttackPressed() => false;
            public bool ConsumeLockOnPressed() => false;
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
