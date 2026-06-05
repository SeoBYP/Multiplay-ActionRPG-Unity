using Game.GUI;
using Game.Gameplay.Input;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Input
{
    /// <summary>
    /// UiInputCaptureBehaviour 검증 — 붙은 GameObject가 활성인 동안 입력 점유, 비활성/파괴 시 해제.
    ///
    /// 핵심 회귀: 로비 X 버튼(btn_close)은 뷰 GameObject를 SetActive(false)로 숨긴다.
    /// 점유 해제가 OnDisable에 묶여 있어야 X로 닫을 때 입력이 풀려 플레이어가 다시 움직인다.
    /// (이전엔 버튼 onClick 리스너에 의존해, SetActive가 onClick 도중 계층을 끄면 해제가 누락돼 프리즈했다.)
    /// </summary>
    [TestFixture]
    public class UiInputCaptureBehaviourTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        [Test]
        public void 활성화시_점유_비활성화시_해제된다()
        {
            int begin = 0, end = 0;
            _go = new GameObject("ui-capture");
            var cap = _go.AddComponent<UiInputCaptureBehaviour>();

            cap.Bind(() => begin++, () => end++);
            Assert.AreEqual(1, begin, "Bind 시 이미 활성이면 즉시 점유해야 한다");
            Assert.AreEqual(0, end);

            _go.SetActive(false); // = 로비 X 버튼(btn_close)이 하는 SetActive(false)
            Assert.AreEqual(1, end, "비활성화(숨김) 시 점유가 해제돼야 한다");

            _go.SetActive(true);  // = L 로 다시 표시
            Assert.AreEqual(2, begin, "재활성화 시 다시 점유해야 한다");
            Assert.AreEqual(1, end);
        }

        [Test]
        public void 파괴시에도_점유가_해제된다()
        {
            int begin = 0, end = 0;
            _go = new GameObject("ui-capture");
            _go.AddComponent<UiInputCaptureBehaviour>().Bind(() => begin++, () => end++);
            Assert.AreEqual(1, begin);

            Object.DestroyImmediate(_go);
            _go = null;
            Assert.AreEqual(1, end, "Destroy(OnDisable) 시에도 해제돼야 한다");
        }

        [Test]
        public void SetActive_false면_실제_Player맵이_다시_켜진다()
        {
            var actions = new PlayerInputActions();
            actions.Player.Enable(); // 게임플레이 기본 상태
            var ctx = new InputContext(actions);

            _go = new GameObject("ui-capture");
            _go.AddComponent<UiInputCaptureBehaviour>().Bind(ctx.EnterUi, ctx.ExitUi);

            Assert.IsFalse(actions.Player.enabled, "UI 활성 → Player 맵 OFF (이동/점프 차단)");

            _go.SetActive(false); // X 닫기
            Assert.IsTrue(actions.Player.enabled, "숨기면 Player 맵 복구 → 플레이어 다시 움직임");

            Object.DestroyImmediate(actions.asset);
        }

        /// <summary>로비 X(숨김) → L(재표시) 사이클: 숨기면 이동 복구, 다시 열면 다시 차단.</summary>
        [Test]
        public void 숨김_후_다시_표시하면_Player맵이_다시_꺼진다()
        {
            var actions = new PlayerInputActions();
            actions.Player.Enable();
            var ctx = new InputContext(actions);

            _go = new GameObject("lobby");
            _go.AddComponent<UiInputCaptureBehaviour>().Bind(ctx.EnterUi, ctx.ExitUi);
            Assert.IsFalse(actions.Player.enabled, "열림 → OFF");

            _go.SetActive(false); // X
            Assert.IsTrue(actions.Player.enabled, "숨김 → ON(이동 복구)");

            _go.SetActive(true);  // L 재표시
            Assert.IsFalse(actions.Player.enabled, "재표시 → 다시 OFF");

            Object.DestroyImmediate(actions.asset);
        }

        /// <summary>중첩(로비+팝업): 하나만 닫혀선 안 되고 둘 다 닫혀야 Player 맵이 복구된다(refcount).</summary>
        [Test]
        public void 중첩_점유는_둘다_해제돼야_Player맵이_켜진다()
        {
            var actions = new PlayerInputActions();
            actions.Player.Enable();
            var ctx = new InputContext(actions);

            var lobby = new GameObject("lobby");
            var popup = new GameObject("popup");
            lobby.AddComponent<UiInputCaptureBehaviour>().Bind(ctx.EnterUi, ctx.ExitUi); // depth 1
            popup.AddComponent<UiInputCaptureBehaviour>().Bind(ctx.EnterUi, ctx.ExitUi); // depth 2
            Assert.IsFalse(actions.Player.enabled);

            Object.DestroyImmediate(popup); // depth 1 — 로비가 아직 점유
            Assert.IsFalse(actions.Player.enabled, "하나만 닫히면 복구되면 안 된다");

            Object.DestroyImmediate(lobby); // depth 0
            Assert.IsTrue(actions.Player.enabled, "둘 다 닫히면 복구");

            Object.DestroyImmediate(actions.asset);
        }
    }
}
