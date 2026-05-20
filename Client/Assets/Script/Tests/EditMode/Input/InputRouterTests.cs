using System.Collections.Generic;
using Game.Input;
using Game.Tests.EditMode.Input.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.Tests.EditMode.Input
{
    /// <summary>
    /// InputRouter 단위 테스트.
    ///
    /// InputTestFixture 를 상속해서 가상 키보드/마우스 디바이스를 생성하고
    /// 실제 New Input System 이벤트를 시뮬레이션한다.
    ///
    /// Press(key) 호출 시 InputSystem.Update() 가 내부에서 실행되어
    /// PlayerInputActions.performed 콜백이 즉시 트리거된다.
    /// </summary>
    [TestFixture]
    public class InputRouterTests : InputTestFixture
    {
        private Keyboard            _keyboard;
        private Mouse               _mouse;
        private PlayerInputActions  _actions;
        private InputRouter         _router;

        public override void Setup()
        {
            base.Setup();

            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse    = InputSystem.AddDevice<Mouse>();

            _actions = new PlayerInputActions();
            _router  = new InputRouter(_actions);
            _router.Initialize();
        }

        public override void TearDown()
        {
            _router.Dispose();
            if (_actions.asset != null)
                Object.DestroyImmediate(_actions.asset);

            base.TearDown();
        }

        // ── 단일 핸들러 ─────────────────────────────────────────────

        [Test]
        public void L키_누르면_ToggleLobby_라우팅된다()
        {
            var handler = new TrackingHandler(priority: 10, consumes: false);
            _router.Register(handler);

            Press(_keyboard.lKey);

            Assert.IsTrue(handler.WasCalled(GameInputAction.ToggleLobby));
        }

        [Test]
        public void E키_누르면_Interact_라우팅된다()
        {
            var handler = new TrackingHandler(priority: 10, consumes: false);
            _router.Register(handler);

            Press(_keyboard.eKey);

            Assert.IsTrue(handler.WasCalled(GameInputAction.Interact));
        }

        [Test]
        public void Escape키_누르면_Pause_라우팅된다()
        {
            var handler = new TrackingHandler(priority: 10, consumes: false);
            _router.Register(handler);

            Press(_keyboard.escapeKey);

            Assert.IsTrue(handler.WasCalled(GameInputAction.Pause));
        }

        [Test]
        public void LeftCtrl키_누르면_Dodge_라우팅된다()
        {
            var handler = new TrackingHandler(priority: 10, consumes: false);
            _router.Register(handler);

            Press(_keyboard.leftCtrlKey);

            Assert.IsTrue(handler.WasCalled(GameInputAction.Dodge));
        }

        [Test]
        public void 마우스_왼쪽버튼_클릭하면_Attack_라우팅된다()
        {
            var handler = new TrackingHandler(priority: 10, consumes: false);
            _router.Register(handler);

            Press(_mouse.leftButton);

            Assert.IsTrue(handler.WasCalled(GameInputAction.Attack));
        }

        // ── Priority 정렬 ────────────────────────────────────────────

        [Test]
        public void 낮은_우선순위_먼저_등록해도_높은_우선순위가_먼저_호출된다()
        {
            var callOrder = new List<int>();
            var high = new OrderTrackingHandler(priority: 100, id: 0, callOrder);
            var low  = new OrderTrackingHandler(priority: 10,  id: 1, callOrder);

            _router.Register(low);
            _router.Register(high);

            Press(_keyboard.lKey);

            Assert.AreEqual(2, callOrder.Count);
            Assert.AreEqual(0, callOrder[0]); // high (id=0) 가 먼저
            Assert.AreEqual(1, callOrder[1]); // low  (id=1) 가 나중
        }

        // ── consumed 차단 ────────────────────────────────────────────

        [Test]
        public void 높은_우선순위_핸들러가_소비하면_낮은_우선순위는_호출되지_않는다()
        {
            var high = new TrackingHandler(priority: 100, consumes: true);
            var low  = new TrackingHandler(priority: 10,  consumes: false);
            _router.Register(high);
            _router.Register(low);

            Press(_keyboard.lKey);

            Assert.IsTrue(high.WasCalled(GameInputAction.ToggleLobby));
            Assert.IsFalse(low.WasCalled(GameInputAction.ToggleLobby),
                "high 가 consumed 했으므로 low 는 호출되지 않아야 한다.");
        }

        [Test]
        public void 소비하지_않으면_모든_핸들러가_호출된다()
        {
            var first  = new TrackingHandler(priority: 100, consumes: false);
            var second = new TrackingHandler(priority: 10,  consumes: false);
            _router.Register(first);
            _router.Register(second);

            Press(_keyboard.lKey);

            Assert.IsTrue(first.WasCalled(GameInputAction.ToggleLobby));
            Assert.IsTrue(second.WasCalled(GameInputAction.ToggleLobby));
        }

        // ── Register / Unregister ────────────────────────────────────

        [Test]
        public void 등록_해제된_핸들러는_호출되지_않는다()
        {
            var handler = new TrackingHandler(priority: 10, consumes: false);
            _router.Register(handler);
            _router.Unregister(handler);

            Press(_keyboard.lKey);

            Assert.IsFalse(handler.WasCalled(GameInputAction.ToggleLobby));
        }

        [Test]
        public void 동일_핸들러를_중복_등록해도_한_번만_호출된다()
        {
            var handler = new TrackingHandler(priority: 10, consumes: false);
            _router.Register(handler);
            _router.Register(handler);

            Press(_keyboard.lKey);

            Assert.AreEqual(1, handler.CallCount(GameInputAction.ToggleLobby),
                "동일 핸들러를 두 번 등록해도 한 번만 호출돼야 한다.");
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────

        /// <summary>호출 순서를 추적하는 핸들러.</summary>
        private class OrderTrackingHandler : IInputHandler
        {
            private readonly int        _id;
            private readonly List<int>  _order;

            public int Priority { get; }

            public OrderTrackingHandler(int priority, int id, List<int> order)
            {
                Priority = priority;
                _id      = id;
                _order   = order;
            }

            public bool TryHandle(GameInputAction action)
            {
                _order.Add(_id);
                return false;
            }
        }
    }
}
