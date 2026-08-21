using System.Collections;
using System.Collections.Generic;
using Game.Gameplay.Character;
using Game.Gameplay.Character.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// 사다리 오르기(P6, 로컬 전용). 세 갈래를 고정한다:
    ///   ① 상호작용 → 부착 요청(one-shot) → Ground→Climb 전이 신호
    ///   ② 상/하단 도달 → Climb→Ground 이탈 신호
    ///   ③ ClimbState 는 사다리에 스냅하고 <b>수직으로만</b> 움직인다(중력·수평 이동 없음)
    /// </summary>
    public class ClimbTests
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
        public IEnumerator 사다리와_상호작용하면_클라임_전이신호가_한번만_선다()
        {
            var ladder = BuildLadder(Vector3.zero, height: 4f);
            var (player, sensor) = BuildPlayerSensor(new Vector3(0f, 0f, 1f));
            yield return null;

            var rule = new GroundToClimbTransition(sensor);
            Assert.IsFalse(rule.ShouldTransition(0f), "상호작용 전에는 전이 신호가 없어야 한다.");

            ladder.Interact(player);
            Assert.IsTrue(rule.ShouldTransition(0f), "사다리 상호작용 시 Climb 전이 신호가 서야 한다.");
            Assert.IsFalse(rule.ShouldTransition(0f), "부착 요청은 one-shot 이라 두 번 소비되면 안 된다.");
            Assert.AreEqual(StateKind.Climb, rule.NextState);
        }

        [UnityTest]
        public IEnumerator 사다리_상단이나_하단에_닿으면_지상으로_이탈한다()
        {
            var ladder = BuildLadder(Vector3.zero, height: 4f);
            var (player, sensor) = BuildPlayerSensor(new Vector3(0f, 2f, 0.4f));
            sensor.RequestAttach(ladder);
            yield return null;

            var rule = new ClimbToGroundTransition(sensor, player.transform);
            Assert.IsFalse(rule.ShouldTransition(0f), "사다리 중간에서는 이탈하지 않아야 한다.");

            player.transform.position = new Vector3(0f, ladder.TopY + 0.05f, 0.4f);
            Assert.IsTrue(rule.ShouldTransition(0f), "상단에 닿으면 이탈해야 한다.");
            Assert.IsTrue(sensor.ShouldDetach(player.transform.position, out bool atTop) && atTop,
                "상단 이탈은 atTop=true 로 알려야 한다(위로 올라서기 판정).");

            player.transform.position = new Vector3(0f, ladder.BottomY - 0.05f, 0.4f);
            Assert.IsTrue(rule.ShouldTransition(0f), "하단에 닿으면(발이 땅) 이탈해야 한다.");
        }

        [UnityTest]
        public IEnumerator 클라임_중에는_사다리에_스냅하고_수직으로만_움직인다()
        {
            var ladder = BuildLadder(new Vector3(3f, 0f, 3f), height: 4f);

            var go = new GameObject("ClimbAgent");
            go.SetActive(false);
            _objects.Add(go);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.5f; cc.center = new Vector3(0f, 1f, 0f);
            var motor = go.AddComponent<CharacterMotor>();
            var anims = go.AddComponent<CharacterAgentAnimations>();
            var sensor = go.AddComponent<ClimbSensor>();
            go.SetActive(true);
            go.transform.position = new Vector3(3f, 1f, 4.2f); // 사다리 앞쪽에 서 있다

            var settings = new LocomotionSettings();
            motor.Construct(settings);
            sensor.RequestAttach(ladder);

            var state = new ClimbState(motor, anims, new FakeUpInput(1f), sensor, settings);
            state.Enter();
            yield return null;

            // 스냅: 사다리 중심축 근처로 붙는다(정면 오프셋만큼만 떨어져서).
            var snapped = go.transform.position;
            float planar = new Vector2(snapped.x - 3f, snapped.z - 3f).magnitude;
            Assert.Less(planar, 0.6f, $"부착 시 사다리 축 근처로 스냅해야 한다(실측 {planar:F2}m).");

            // 수직 상승만: y 는 오르고 x/z 는 그대로.
            var before = go.transform.position;
            for (int i = 0; i < 20; i++) { state.Update(0.02f); yield return null; }
            var after = go.transform.position;

            Assert.Greater(after.y - before.y, 0.05f, $"위 입력이면 올라가야 한다(실측 {(after.y - before.y):F3}m).");
            float horiz = new Vector2(after.x - before.x, after.z - before.z).magnitude;
            Assert.Less(horiz, 0.01f, $"사다리에서는 수평 이동이 없어야 한다(실측 {horiz:F4}m).");
        }

        [UnityTest]
        public IEnumerator 점프하면_사다리_반대쪽으로_밀려나며_낙하로_빠진다()
        {
            var ladder = BuildLadder(Vector3.zero, height: 4f);
            var (go, motor, anims, sensor, settings) = BuildClimber(new Vector3(0f, 2f, 1.0f));
            sensor.RequestAttach(ladder);

            var input = new MutableClimbInput { Axis = 1f };
            var state = new ClimbState(motor, anims, input, sensor, settings);
            state.Enter();
            yield return null;

            var beforePlanar = new Vector2(go.transform.position.x, go.transform.position.z);
            input.JumpPressed = true;                 // Space
            state.Update(0.02f);

            var fall = new ClimbToFallTransition(sensor);
            Assert.IsTrue(fall.ShouldTransition(0f), "점프하면 낙하로 빠져야 한다(사다리 중간에서도 이탈 가능).");
            Assert.AreEqual(StateKind.Fall, fall.NextState);

            state.Exit();
            var afterPlanar = new Vector2(go.transform.position.x, go.transform.position.z);
            float pushed = (afterPlanar - beforePlanar).magnitude;
            Assert.Greater(pushed, 0.3f,
                $"사다리 반대쪽으로 밀려나야 한다(실측 {pushed:F2}m, 설정 {settings.ClimbJumpOffDistance}m).");
        }

        [UnityTest]
        public IEnumerator 바닥_근처에서_아래_입력이면_최하단까지_안_가도_내려선다()
        {
            var ladder = BuildLadder(Vector3.zero, height: 4f);
            // 바닥에서 0.3m 위 — 해제 높이(0.6) 안.
            var (go, motor, anims, sensor, settings) = BuildClimber(new Vector3(0f, ladder.BottomY + 0.3f, 0.4f));
            sensor.RequestAttach(ladder);

            var input = new MutableClimbInput { Axis = -1f }; // 아래 입력
            var state = new ClimbState(motor, anims, input, sensor, settings);
            state.Enter();
            go.transform.position = new Vector3(0f, ladder.BottomY + 0.3f, 0.4f); // Enter 의 스냅 이후 높이 고정
            yield return null;

            var toGround = new ClimbToGroundTransition(sensor, go.transform);
            Assert.IsFalse(toGround.ShouldTransition(0f), "아직 아래 입력을 처리하기 전");

            state.Update(0.02f);
            Assert.IsTrue(toGround.ShouldTransition(0f),
                "바닥 근처(0.3m)에서 아래를 누르면 최하단(0m)까지 가지 않아도 내려서야 한다.");
            Assert.AreEqual(StateKind.Ground, toGround.NextState, "Idle 로 복귀 = Ground 상태");
        }

        [UnityTest]
        public IEnumerator 상단_이탈은_레이캐스트로_찾은_바닥_위에_선다()
        {
            var ladder = BuildLadder(Vector3.zero, height: 4f);

            // 사다리 위쪽에 바닥(플랫폼)을 놓는다 — 실제로는 옥상/난간.
            var floor = new GameObject("TopFloor");
            _objects.Add(floor);
            floor.transform.position = new Vector3(0f, 3.7f, -0.8f);
            var fc = floor.AddComponent<BoxCollider>();
            fc.size = new Vector3(4f, 0.2f, 4f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            // 사다리 앞쪽(+z)에서 올라왔다고 보면 이탈은 반대편(-z)
            var exit = ladder.GetTopExitPosition(new Vector3(0f, 4f, 1f));
            Assert.AreEqual(3.8f, exit.y, 0.15f,
                $"이탈 지점은 플랫폼 윗면(3.8) 높이여야 한다(실측 {exit.y:F2}). 고정 높이면 공중에 뜬다.");
        }

        [UnityTest]
        public IEnumerator 옆에서_다가와도_사다리_정면에_붙는다()
        {
            // 회귀: 부착 방향을 '접근한 방향' 그대로 쓰면 옆(폭 방향)에서 오면 측면 기둥에 옆으로 매달린다
            // (실제로 그런 자세가 나왔다). 면 법선에 스냅해 어느 쪽에서 와도 정면으로 마주보게 한다.
            var ladder = BuildLadder(Vector3.zero, height: 4f);
            yield return null;

            // BuildLadder 의 박스는 x 1.0 · z 0.3 → 얇은 축(면 법선) = z
            foreach (var approach in new[] { new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f) })
            {
                var stand = new Vector3(0f, 1.5f, 0f) + approach * 1.2f;
                ladder.GetAttachPose(stand, out var pos, out var rot);

                float sideOffset = Mathf.Abs(pos.x);   // 폭 방향으로 밀려나면 안 된다
                float faceOffset = Mathf.Abs(pos.z);   // 면 방향으로만 떨어져 있어야 한다
                Assert.Less(sideOffset, 0.05f,
                    $"옆에서 와도 폭 방향으로 매달리면 안 된다(실측 x={pos.x:F2}).");
                Assert.Greater(faceOffset, 0.1f,
                    $"사다리 면 앞쪽에 붙어야 한다(실측 z={pos.z:F2}).");

                var fwd = rot * Vector3.forward;
                Assert.Less(Mathf.Abs(fwd.x), 0.2f, $"몸이 옆을 보면 안 된다(실측 forward={fwd}).");
                Assert.Greater(Mathf.Abs(fwd.z), 0.9f, "사다리 면을 정면으로 봐야 한다.");
            }
        }

        // ── 리그 ────────────────────────────────────────────────────────────
        private Ladder BuildLadder(Vector3 basePos, float height)
        {
            var go = new GameObject("Ladder");
            _objects.Add(go);
            go.transform.position = basePos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = new Vector3(1f, height, 0.3f);
            return go.AddComponent<Ladder>();
        }

        private (GameObject player, ClimbSensor sensor) BuildPlayerSensor(Vector3 pos)
        {
            var go = new GameObject("ClimbPlayer");
            _objects.Add(go);
            go.transform.position = pos;
            return (go, go.AddComponent<ClimbSensor>());
        }

        /// <summary>축과 점프를 테스트가 바꿀 수 있는 입력 소스.</summary>
        private sealed class MutableClimbInput : ICharacterInputSource
        {
            public float Axis;
            public bool JumpPressed;
            public CharacterInputFrame Current => default(CharacterInputFrame).WithMove(new Vector2(0f, Axis));
            public bool ConsumeJumpPressed() { bool j = JumpPressed; JumpPressed = false; return j; }
            public bool ConsumeDodgePressed() => false;
            public bool ConsumeInteractPressed() => false;
            public bool ConsumeAttackPressed() => false;
            public bool ConsumeHeavyAttackPressed() => false;
            public bool ConsumeLockOnPressed() => false;
        }

        /// <summary>Motor·Animations·ClimbSensor 가 붙은 등반용 리그.</summary>
        private (GameObject go, CharacterMotor motor, CharacterAgentAnimations anims, ClimbSensor sensor, LocomotionSettings settings)
            BuildClimber(Vector3 pos)
        {
            var go = new GameObject("ClimbRig");
            go.SetActive(false);
            _objects.Add(go);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.5f; cc.center = new Vector3(0f, 1f, 0f);
            var motor = go.AddComponent<CharacterMotor>();
            var anims = go.AddComponent<CharacterAgentAnimations>();
            var sensor = go.AddComponent<ClimbSensor>();
            go.SetActive(true);
            go.transform.position = pos;

            var settings = new LocomotionSettings();
            motor.Construct(settings);
            return (go, motor, anims, sensor, settings);
        }

        /// <summary>사다리 입력용 — 전/후 축만 고정으로 낸다.</summary>
        private sealed class FakeUpInput : ICharacterInputSource
        {
            private readonly CharacterInputFrame _frame;
            public FakeUpInput(float axis) => _frame = default(CharacterInputFrame).WithMove(new Vector2(0f, axis));
            public CharacterInputFrame Current => _frame;
            public bool ConsumeJumpPressed() => false;
            public bool ConsumeDodgePressed() => false;
            public bool ConsumeInteractPressed() => false;
            public bool ConsumeAttackPressed() => false;
            public bool ConsumeHeavyAttackPressed() => false;
            public bool ConsumeLockOnPressed() => false;
        }
    }
}
