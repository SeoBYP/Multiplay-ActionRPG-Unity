using Game.Gameplay.Input;
using Game.Tests.EditMode.Input.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Tests.EditMode.Input
{
    /// <summary>
    /// HUD 창 토글 키의 정식 라우팅 (F13).
    ///
    /// 예전에는 GameHud 가 `Keyboard.current` 를 직접 폴링했다 — 같은 프로젝트 안에서
    /// 락온(정식 경로)과 창 토글(임시 폴링)이 서로 다른 방식으로 굴러가고 있었다.
    /// 이제 i·k·q·g 가 전부 `.inputactions` → 생성 래퍼 → InputRouter → GameInputAction 으로 흐른다.
    /// </summary>
    [TestFixture]
    public class HudToggleRoutingTests : InputTestFixture
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
            _actions.Player.Enable(); // 맵 활성화는 평소 전역이 소유 — 단위 테스트에선 직접 켠다.

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
        public void I키_누르면_ToggleInventory_라우팅된다()
        {
            Press(_keyboard.iKey);
            Assert.IsTrue(_handler.WasCalled(GameInputAction.ToggleInventory));
        }

        [Test]
        public void K키_누르면_ToggleEquipment_라우팅된다()
        {
            Press(_keyboard.kKey);
            Assert.IsTrue(_handler.WasCalled(GameInputAction.ToggleEquipment));
        }

        [Test]
        public void Q키_누르면_ToggleQuest_라우팅된다()
        {
            Press(_keyboard.qKey);
            Assert.IsTrue(_handler.WasCalled(GameInputAction.ToggleQuest));
        }

        [Test]
        public void G키_누르면_ToggleAbility_라우팅된다()
        {
            Press(_keyboard.gKey);
            Assert.IsTrue(_handler.WasCalled(GameInputAction.ToggleAbility));
        }

        [Test]
        public void Dispose_하면_구독이_해제돼_더_이상_라우팅되지_않는다()
        {
            // PlayerInputActions 는 루트 싱글턴이라, 씬을 오갈 때마다 새 라우터가 구독만 하고 끝나면
            // 죽은 라우터의 델리게이트가 계속 쌓인다. Dispose 가 자기 구독을 되돌려야 한다.
            _router.Dispose();

            var survivor = new TrackingHandler(priority: 100, consumes: true);
            _router.Register(survivor);

            Press(_keyboard.iKey);

            Assert.IsFalse(survivor.WasCalled(GameInputAction.ToggleInventory),
                "Dispose 후에도 라우팅됐다 = performed 구독이 남아 있다");
        }
    }
}
