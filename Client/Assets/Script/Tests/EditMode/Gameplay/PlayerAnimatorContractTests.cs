using System;
using Game.Gameplay.Character;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 플레이어 컨트롤러 자산의 계약 고정(코드 생성물이라 사람이 손대면 조용히 어긋난다).
    /// 특히 <b>루트모션 태그</b>와 <b>블렌드 좌표=클립 실측 속도</b>는 눈으로 보기 전엔 안 드러나는 회귀라 여기서 잡는다.
    /// </summary>
    public class PlayerAnimatorContractTests
    {
        private const string ControllerPath = "Assets/GameResources/Animations/Player/PlayerController_ARPG.controller";

        private static AnimatorController Load()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assume.That(ctrl, Is.Not.Null, "PlayerController_ARPG 를 찾지 못했다");
            return ctrl;
        }

        [Test]
        public void 루트모션_태그는_공격_상태에만_붙어있다()
        {
            // 회피(Evade 3.6m)·사망(0.84m)·기상(0.79m) 클립에도 루트 이동이 있지만 드라이버가 이동을 전담한다.
            // 태그가 번지면 대시 + 루트모션이 겹쳐 두 배로 날아간다.
            foreach (var st in Load().layers[0].stateMachine.states)
            {
                bool isCombo = st.state.name.StartsWith("Combo");
                bool tagged = st.state.tag == RootMotionRelay.RootMotionTag;
                Assert.AreEqual(isCombo, tagged,
                    $"'{st.state.name}' 의 RootMotion 태그가 기대와 다르다(공격만 true).");
            }
        }

        [Test]
        public void 스트레이프_전이는_즉시여야_한다()
        {
            // 회귀(2026-08-22): 이 전이에 블렌드 시간이 있으면 두 가지가 동시에 깨진다.
            //  ① 블렌드 창 동안 AnyState 트리거(Dodge/Attack/Jump)가 삼켜진다.
            //  ② 그걸 interruptionSource 로 풀면 조건이 계속 참이라 전이가 매 프레임 자기 자신으로 재시작해
            //     StrafeLocomotion 에 영영 도달하지 못한다(실측: Walk/Run 이 아예 안 나왔다).
            // Strafe 는 스폰 직후 한 번 켜지고 유지되므로 즉시 전환이 안전하다.
            var sm = Load().layers[0].stateMachine;
            var loco = Array.Find(sm.states, s => s.state.name == "Locomotion").state;
            var strafe = Array.Find(sm.states, s => s.state.name == "StrafeLocomotion").state;

            var toStrafe = Array.Find(loco.transitions, t => t.destinationState == strafe);
            var toLoco = Array.Find(strafe.transitions, t => t.destinationState == loco);

            Assert.IsNotNull(toStrafe, "Locomotion → StrafeLocomotion 전이가 있어야 한다");
            Assert.IsNotNull(toLoco, "StrafeLocomotion → Locomotion 전이가 있어야 한다");

            Assert.AreEqual(0f, toStrafe.duration, 0.0001f, "스트레이프 진입은 즉시여야 한다(블렌드 창 = 트리거 유실).");
            Assert.AreEqual(0f, toLoco.duration, 0.0001f);
            Assert.AreEqual(TransitionInterruptionSource.None, toStrafe.interruptionSource,
                "인터럽트를 켜면 조건이 참인 동안 전이가 자기 자신으로 재시작한다(실측).");
        }

        [Test]
        public void 스트레이프_블렌드는_클립_실측속도_좌표를_쓴다()
        {
            var strafe = Array.Find(Load().layers[0].stateMachine.states,
                s => s.state.name == "StrafeLocomotion").state;
            Assert.IsNotNull(strafe, "StrafeLocomotion 상태가 있어야 한다");

            var tree = strafe.motion as BlendTree;
            Assert.IsNotNull(tree, "StrafeLocomotion 은 블렌드 트리여야 한다");
            Assert.AreEqual(18, tree.children.Length,
                "Idle 1 + Walk 8방향 + Run 8방향 + Sprint 1 = 18 이어야 한다(걸을 땐 걷는 클립이 나와야 하므로 Walk 링 필수).");

            // 좌표가 0~1 정규화가 아니라 m/s 실측이어야 한다(정규화면 발 속도와 이동 속도가 어긋나 미끄러진다).
            float maxExtent = 0f;
            foreach (var c in tree.children)
                maxExtent = Mathf.Max(maxExtent, Mathf.Abs(c.position.x), Mathf.Abs(c.position.y));
            Assert.Greater(maxExtent, 3f, $"블렌드 좌표는 m/s 단위여야 한다(최대 {maxExtent:F2}).");

            // 스프린트 클립(3.44m/s)은 게임 속도(5.335)에 맞춰 배속 보정돼 있어야 한다.
            var sprint = Array.Find(tree.children, c => c.motion != null && c.motion.name.Contains("Sprint"));
            Assert.Greater(sprint.timeScale, 1.3f,
                $"Sprint 는 클립보다 빨리 달리므로 배속 보정이 필요하다(현재 {sprint.timeScale:F2}).");
        }
    }
}
