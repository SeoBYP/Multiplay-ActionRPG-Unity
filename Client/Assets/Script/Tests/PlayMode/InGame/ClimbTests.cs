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
