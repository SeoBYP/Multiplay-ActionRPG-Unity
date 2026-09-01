using Game.Gameplay.Input;
using Game.Tests.EditMode.Input.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tests.EditMode.Input
{
    /// <summary>
    /// 채팅 입력 열기 키(Enter) 라우팅.
    ///
    /// "전송 Enter" 는 여기 없다 — 채팅 입력이 활성인 동안 <see cref="InputContext"/> 가
    /// Player 맵을 통째로 끄기 때문에, 전송은 UI 맵(InputField.onSubmit)이 받는다.
    /// 즉 같은 Enter 가 상태에 따라 다른 맵에서 처리된다.
    /// </summary>
    [TestFixture]
    public class ChatInputRoutingTests : InputTestFixture
    {
        private Keyboard           _keyboard;
        private PlayerInputActions _actions;
        private InputRouter        _router;
        private TrackingHandler    _handler;

        public override void Setup()
        {
            base.Setup();

            _keyboard = InputSystem.AddDevice<Keyboard>();
            _actions  = new PlayerInputActions();
            _router   = new InputRouter(_actions);
            _router.Initialize();
            _actions.Player.Enable();

            _handler = new TrackingHandler(priority: 100, consumes: true);
            _router.Register(_handler);
        }

        public override void TearDown()
        {
            _router.Dispose();
            // Disable 없이 asset 만 파괴하면 PlayerInputActions 종료자가 맵 누수 assert 를 띄운다.
            // 그 로그는 GC 시점에 붙어 "뒤이어 실행되는 무관한 테스트"를 실패시킨다.
            _actions.Disable();
            if (_actions.asset != null)
                Object.DestroyImmediate(_actions.asset);

            base.TearDown();
        }

        [Test]
        public void Enter키_누르면_Chat_라우팅된다()
        {
            Press(_keyboard.enterKey);
            Assert.IsTrue(_handler.WasCalled(GameInputAction.Chat));
        }

        [Test]
        public void Player맵이_꺼져있으면_Enter가_라우팅되지_않는다()
        {
            // 채팅 입력 중(=UI 점유로 Player 맵 OFF)에는 열기 키가 다시 먹으면 안 된다.
            _actions.Player.Disable();

            Press(_keyboard.enterKey);
            Assert.IsFalse(_handler.WasCalled(GameInputAction.Chat));
        }
    }
}
