using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// 로컬 플레이어 로코모션이 <b>실제로 재생되는지</b> — 실제 PlayerCharacter 프리팹 + 생성된 컨트롤러로 확인한다.
    ///
    /// 회귀 배경(2026-08-22): `Locomotion→StrafeLocomotion` 전이에 `interruptionSource` 를 주었더니
    /// 조건(Strafe=true)이 계속 참이라 **전이가 매 프레임 자기 자신으로 재시작**했다. 실측으로 40프레임 뒤에도
    /// `inTransition=True · normalizedTime 0.03` 고정 — StrafeLocomotion(8방향 Walk/Run 트리)에 영영 도달하지 못해
    /// <b>걷기·달리기가 아예 안 나왔다</b>. 파라미터만 검사하는 기존 테스트 213개는 이걸 잡지 못했다.
    /// → "파라미터를 넣었다"가 아니라 "그 상태에 도달했고 시간이 흐른다"를 단언한다.
    /// </summary>
    public class PlayerLocomotionAnimTests
    {
        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.Destroy(_instance);
            _instance = null;
        }

        [UnityTest]
        public IEnumerator Strafe를_켜면_StrafeLocomotion에_도달하고_재생이_진행된다()
        {
            var animator = Spawn();
            if (animator == null) yield break;

            animator.SetBool("Strafe", true);
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", 2.3f); // 걷기 속도(m/s)

            // 전이가 자기 자신으로 재시작하면 여기서 영원히 Locomotion 에 머문다.
            bool arrived = false;
            for (int i = 0; i < 30 && !arrived; i++)
            {
                yield return null;
                arrived = animator.GetCurrentAnimatorStateInfo(0).IsName("StrafeLocomotion");
            }

            Assert.IsTrue(arrived,
                "Strafe=true 면 8방향 트리(StrafeLocomotion)에 도달해야 한다 — 도달 못 하면 Walk/Run 이 안 나온다.");

            float before = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            for (int i = 0; i < 10; i++) yield return null;
            float after = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

            Assert.Greater(after, before,
                $"재생이 진행돼야 한다(전 {before:F3} → 후 {after:F3}). 멈춰 있으면 제자리 포즈만 보인다.");
        }

        [UnityTest]
        public IEnumerator 걷기와_달리기는_서로_다른_클립_구간을_쓴다()
        {
            // 블렌드 좌표가 m/s 실측이라, 같은 방향이라도 속도가 다르면 다른 클립이 섞인다.
            // (정규화 좌표로 되돌아가면 이 차이가 사라지고 발이 미끄러진다 — 그 회귀도 여기서 잡힌다.)
            var animator = Spawn();
            if (animator == null) yield break;

            animator.SetBool("Strafe", true);
            animator.SetFloat("MoveX", 0f);

            animator.SetFloat("MoveY", 2.3f);
            for (int i = 0; i < 20; i++) yield return null;
            var walkClips = animator.GetCurrentAnimatorClipInfo(0);

            animator.SetFloat("MoveY", 3.3f);
            for (int i = 0; i < 20; i++) yield return null;
            var runClips = animator.GetCurrentAnimatorClipInfo(0);

            Assert.IsNotEmpty(walkClips, "걷기 구간에 재생 중인 클립이 있어야 한다.");
            Assert.IsNotEmpty(runClips, "달리기 구간에 재생 중인 클립이 있어야 한다.");

            string walkTop = TopClipName(walkClips);
            string runTop = TopClipName(runClips);
            Assert.AreNotEqual(walkTop, runTop,
                $"걷기({walkTop})와 달리기({runTop})는 다른 클립이 주가 돼야 한다.");
        }

        // ── 리그 ────────────────────────────────────────────────────────────

        /// <summary>
        /// <b>컨트롤러만</b> 단독으로 굴린다 — 프리팹을 통째로 Instantiate 하면 DI 없는 컴포넌트가 NRE 를 던지고,
        /// 그 로그가 테스트를 실패시켜 <b>무관한 실패가 진짜 신호를 덮는다</b>(챕터27의 하네스 함정).
        /// 여기서 보려는 것은 상태 머신의 진행이지 프리팹 배선이 아니다(그건 PlayerAnimatorContractTests 가 본다).
        /// </summary>
        private Animator Spawn()
        {
            RuntimeAnimatorController controller = null;
#if UNITY_EDITOR
            controller = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/GameResources/Animations/Player/PlayerController_ARPG.controller");
#endif
            Assume.That(controller, Is.Not.Null, "PlayerController_ARPG 로드 실패(에디터 외 실행)");

            _instance = new GameObject("LocomotionProbe");
            var animator = _instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            return animator;
        }

        private static string TopClipName(AnimatorClipInfo[] clips)
        {
            string name = string.Empty;
            float best = -1f;
            foreach (var c in clips)
                if (c.weight > best) { best = c.weight; name = c.clip != null ? c.clip.name : string.Empty; }
            return name;
        }
    }
}
