using System.Collections.Generic;
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
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private GameObject _go;
        private PlayerInputActions _actions;

        [TearDown]
        public void TearDown()
        {
            // 생성물 정리를 테스트 본문 끝이 아니라 TearDown 에 모은 이유:
            // 본문 중간에서 Assert 가 실패하면 그 아래 줄에 도달하지 못해 정리가 통째로 건너뛰어지고,
            // 남은 GameObject 와 켜진 입력 맵이 **뒤이어 실행되는 무관한 테스트**로 새어 들어간다.
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            _go = null;

            // 특히 입력 맵은 켜진 채 GC 되면 PlayerInputActions 종료자가 누수 assert 를 띄우고,
            // 그 로그가 그때 돌던 엉뚱한 테스트를 실패시킨다.
            // (Dispose() 는 Destroy(asset) 만 하므로 Disable 을 대신하지 못한다.)
            if (_actions != null)
            {
                _actions.Disable();
                if (_actions.asset != null) Object.DestroyImmediate(_actions.asset);
                _actions = null;
            }
        }

        /// <summary>입력 액션 생성 — TearDown 이 반드시 정리하도록 필드에 보관한다.</summary>
        private PlayerInputActions NewActions()
        {
            _actions = new PlayerInputActions();
            return _actions;
        }

        /// <summary>GameObject 생성 — TearDown 이 반드시 파괴하도록 추적 목록에 넣는다.</summary>
        private GameObject NewGo(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        [Test]
        public void 활성화시_점유_비활성화시_해제된다()
        {
            int begin = 0, end = 0;
            _go = NewGo("ui-capture");
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
            _go = NewGo("ui-capture");
            _go.AddComponent<UiInputCaptureBehaviour>().Bind(() => begin++, () => end++);
            Assert.AreEqual(1, begin);

            Object.DestroyImmediate(_go);
            _go = null;
            Assert.AreEqual(1, end, "Destroy(OnDisable) 시에도 해제돼야 한다");
        }

        [Test]
        public void SetActive_false면_실제_Player맵이_다시_켜진다()
        {
            var actions = NewActions();
            actions.Player.Enable(); // 게임플레이 기본 상태
            var ctx = new InputContext(actions);

            _go = NewGo("ui-capture");
            _go.AddComponent<UiInputCaptureBehaviour>().Bind(ctx.EnterUi, ctx.ExitUi);

            Assert.IsFalse(actions.Player.enabled, "UI 활성 → Player 맵 OFF (이동/점프 차단)");

            _go.SetActive(false); // X 닫기
            Assert.IsTrue(actions.Player.enabled, "숨기면 Player 맵 복구 → 플레이어 다시 움직임");
        }

        /// <summary>로비 X(숨김) → L(재표시) 사이클: 숨기면 이동 복구, 다시 열면 다시 차단.</summary>
        [Test]
        public void 숨김_후_다시_표시하면_Player맵이_다시_꺼진다()
        {
            var actions = NewActions();
            actions.Player.Enable();
            var ctx = new InputContext(actions);

            _go = NewGo("lobby");
            _go.AddComponent<UiInputCaptureBehaviour>().Bind(ctx.EnterUi, ctx.ExitUi);
            Assert.IsFalse(actions.Player.enabled, "열림 → OFF");

            _go.SetActive(false); // X
            Assert.IsTrue(actions.Player.enabled, "숨김 → ON(이동 복구)");

            _go.SetActive(true);  // L 재표시
            Assert.IsFalse(actions.Player.enabled, "재표시 → 다시 OFF");
        }

        /// <summary>중첩(로비+팝업): 하나만 닫혀선 안 되고 둘 다 닫혀야 Player 맵이 복구된다(refcount).</summary>
        [Test]
        public void 중첩_점유는_둘다_해제돼야_Player맵이_켜진다()
        {
            var actions = NewActions();
            actions.Player.Enable();
            var ctx = new InputContext(actions);

            var lobby = NewGo("lobby");
            var popup = NewGo("popup");
            lobby.AddComponent<UiInputCaptureBehaviour>().Bind(ctx.EnterUi, ctx.ExitUi); // depth 1
            popup.AddComponent<UiInputCaptureBehaviour>().Bind(ctx.EnterUi, ctx.ExitUi); // depth 2
            Assert.IsFalse(actions.Player.enabled);

            Object.DestroyImmediate(popup); // depth 1 — 로비가 아직 점유
            Assert.IsFalse(actions.Player.enabled, "하나만 닫히면 복구되면 안 된다");

            Object.DestroyImmediate(lobby); // depth 0
            Assert.IsTrue(actions.Player.enabled, "둘 다 닫히면 복구");
        }
    }
}
