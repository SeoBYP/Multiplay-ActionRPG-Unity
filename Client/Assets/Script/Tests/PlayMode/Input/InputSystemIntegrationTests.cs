using System;
using System.Collections;
using System.Collections.Generic;
using Game.Gameplay.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Input
{
    /// <summary>
    /// Input System 통합 테스트 (PlayMode).
    ///
    /// 검증 범위:
    ///   실제 New Input System 이벤트 → InputRouter → IInputHandler 체인 전체 흐름.
    ///
    /// EditMode 의 InputRouterTests 와 차이:
    ///   - [UnityTest] 로 프레임 단위 비동기 처리 확인
    ///   - 여러 핸들러가 씬 생명주기에 따라 Register / Unregister 되는 동적 시나리오
    ///   - InteractionSystem 과 InputRouter 의 협력 관계
    /// </summary>
    [TestFixture]
    public class InputSystemIntegrationTests : InputTestFixture
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

            // 프로덕션에선 GlobalInputInitializer가 맵을 Enable한다(입력 전역화).
            // InputRouter는 더 이상 Enable하지 않으므로, 그 초기화가 없는 이 테스트에서
            // 직접 활성화해야 performed 콜백이 발생한다.
            _actions.Enable();
        }

        public override void TearDown()
        {
            _router.Dispose();
            // Disable 없이 asset만 파괴하면 PlayerInputActions 종료자가 맵 누수 경고를 띄운다.
            _actions.Disable();
            if (_actions.asset != null)
                Object.DestroyImmediate(_actions.asset);

            base.TearDown();
        }

        // ── 키 → 핸들러 전달 (프레임 대기 포함) ─────────────────────

        [UnityTest]
        public IEnumerator L키_누르고_한_프레임_후_ToggleLobby_전달된다()
        {
            var received = new List<GameInputAction>();
            var handler  = new LambdaHandler(10, a => { received.Add(a); return false; });
            _router.Register(handler);

            Press(_keyboard.lKey);
            yield return null;

            Assert.IsTrue(received.Contains(GameInputAction.ToggleLobby),
                "L 키 → ToggleLobby 액션이 핸들러에 전달돼야 한다.");
        }

        [UnityTest]
        public IEnumerator 여러_키_프레임마다_눌러도_각각_라우팅된다()
        {
            var received = new List<GameInputAction>();
            var handler  = new LambdaHandler(10, a => { received.Add(a); return false; });
            _router.Register(handler);

            Press(_keyboard.lKey);
            yield return null;

            Press(_keyboard.escapeKey);
            yield return null;

            Assert.IsTrue(received.Contains(GameInputAction.ToggleLobby));
            Assert.IsTrue(received.Contains(GameInputAction.Pause));
        }

        // ── InteractionSystem 협력 테스트 ───────────────────────────

        [UnityTest]
        public IEnumerator InteractionSystem_유효한_대상_있으면_E키를_소비한다()
        {
            var interactionSystem = new InteractionSystem(_router);
            interactionSystem.Initialize();

            var fakeTarget = new FakeInteractable(canInteract: true);
            interactionSystem.SetCurrent(fakeTarget);

            var lowHandler = new LambdaHandler(10, a => false);
            _router.Register(lowHandler);

            Press(_keyboard.eKey);
            yield return null;

            Assert.IsTrue(fakeTarget.InteractCalled,
                "InteractionSystem 이 E 키를 받아 Interact() 를 호출해야 한다.");
            Assert.IsFalse(lowHandler.WasCalled(GameInputAction.Interact),
                "InteractionSystem(Priority=50) 이 consumed 했으므로 낮은 핸들러는 호출되지 않아야 한다.");

            interactionSystem.Dispose();
        }

        [UnityTest]
        public IEnumerator InteractionSystem_대상_없으면_E키를_소비하지_않는다()
        {
            var interactionSystem = new InteractionSystem(_router);
            interactionSystem.Initialize();

            var fallbackHandler = new LambdaHandler(10, a => false);
            _router.Register(fallbackHandler);

            Press(_keyboard.eKey);
            yield return null;

            Assert.IsTrue(fallbackHandler.WasCalled(GameInputAction.Interact),
                "대상이 없으면 InteractionSystem 은 consumed 하지 않으므로 낮은 핸들러가 호출돼야 한다.");

            interactionSystem.Dispose();
        }

        // ── 동적 Register / Unregister ──────────────────────────────

        [UnityTest]
        public IEnumerator 등록_해제_후에는_핸들러가_호출되지_않는다()
        {
            var received = new List<GameInputAction>();
            var handler  = new LambdaHandler(10, a => { received.Add(a); return false; });
            _router.Register(handler);

            Press(_keyboard.lKey);
            yield return null;
            Assert.AreEqual(1, received.Count);

            _router.Unregister(handler);

            Press(_keyboard.lKey);
            yield return null;
            Assert.AreEqual(1, received.Count, "Unregister 후에는 핸들러가 호출되지 않아야 한다.");
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────

        private class LambdaHandler : IInputHandler
        {
            private readonly Func<GameInputAction, bool> _handle;
            private readonly List<GameInputAction> _received = new List<GameInputAction>();

            public int Priority { get; }

            public LambdaHandler(int priority, Func<GameInputAction, bool> handle)
            {
                Priority = priority;
                _handle  = handle;
            }

            public bool TryHandle(GameInputAction action)
            {
                _received.Add(action);
                return _handle(action);
            }

            public bool WasCalled(GameInputAction action) => _received.Contains(action);
        }

        private class FakeInteractable : IInteractable
        {
            public string       InteractionHint     => "Test";
            public bool         CanInteract         { get; }
            public int          InteractionPriority => 0;
            public Transform    Transform           => null;
            public bool         InteractCalled      { get; private set; }

            public FakeInteractable(bool canInteract) { CanInteract = canInteract; }
            public void Interact() => InteractCalled = true;
        }
    }
}
