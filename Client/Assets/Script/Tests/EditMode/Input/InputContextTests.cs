using Game.Gameplay.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tests.EditMode.Input
{
    /// <summary>
    /// InputContext 단위 테스트 — UI 점유 시 Player 맵 토글 + 중첩 카운팅 + 원래상태 복구.
    /// 디바이스 입력 시뮬레이션은 불필요(맵 enabled 플래그만 검증)하지만,
    /// InputTestFixture 로 InputSystem 환경을 격리한다.
    /// </summary>
    [TestFixture]
    public class InputContextTests : InputTestFixture
    {
        private PlayerInputActions _actions;
        private InputContext       _context;

        public override void Setup()
        {
            base.Setup();
            _actions = new PlayerInputActions();
            _context = new InputContext(_actions);
        }

        public override void TearDown()
        {
            if (_actions.asset != null)
                Object.DestroyImmediate(_actions.asset);
            base.TearDown();
        }

        [Test]
        public void UI진입하면_Player맵이_비활성화된다()
        {
            _actions.Player.Enable(); // 게임플레이 기본 상태
            Assert.IsTrue(_actions.Player.enabled);

            _context.EnterUi();

            Assert.IsFalse(_actions.Player.enabled, "UI 진입 시 Player 맵이 꺼져야 한다");
            Assert.IsTrue(_context.IsUiActive);
        }

        [Test]
        public void UI해제하면_Player맵이_다시_활성화된다()
        {
            _actions.Player.Enable();

            _context.EnterUi();
            _context.ExitUi();

            Assert.IsTrue(_actions.Player.enabled, "마지막 UI 해제 시 Player 맵이 복구돼야 한다");
            Assert.IsFalse(_context.IsUiActive);
        }

        [Test]
        public void 중첩진입은_마지막_해제에만_Player맵을_복구한다()
        {
            _actions.Player.Enable();

            _context.EnterUi(); // depth 1 → 끔
            _context.EnterUi(); // depth 2
            _context.ExitUi();  // depth 1 → 아직 점유 중

            Assert.IsFalse(_actions.Player.enabled, "중첩 점유가 남아 있으면 복구되면 안 된다");

            _context.ExitUi();  // depth 0 → 복구

            Assert.IsTrue(_actions.Player.enabled, "마지막 해제에서만 복구돼야 한다");
        }

        [Test]
        public void 원래_비활성이면_해제후에도_비활성을_유지한다()
        {
            _actions.Player.Disable(); // 캐릭터 없는 메뉴 씬 가정

            _context.EnterUi();
            _context.ExitUi();

            Assert.IsFalse(_actions.Player.enabled, "원래 꺼져 있었다면 멋대로 켜면 안 된다");
        }

        [Test]
        public void 과다_ExitUi_호출은_무시된다()
        {
            _actions.Player.Enable();

            _context.ExitUi(); // depth 0에서 호출 — underflow 방어
            _context.ExitUi();

            Assert.IsTrue(_actions.Player.enabled);
            Assert.IsFalse(_context.IsUiActive);
        }
    }
}
