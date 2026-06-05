using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Game.Gameplay.Input
{
    /// <summary>
    /// New Input System(PlayerInputActions) 의 performed 이벤트를 받아
    /// 등록된 IInputHandler 체인에 우선순위 순으로 라우팅한다.
    ///
    /// 흐름:
    ///   PlayerInputActions.performed 콜백
    ///   → GameInputAction 으로 변환
    ///   → 높은 Priority 핸들러부터 TryHandle 호출
    ///   → true 반환 시 해당 액션 소비 (하위 핸들러 차단)
    ///
    /// 사용:
    ///   Register(handler)   — 핸들러 등록 (Initialize 에서 호출)
    ///   Unregister(handler) — 핸들러 해제 (Dispose 에서 호출)
    /// </summary>
    public sealed class InputRouter : IInputRouter, IInitializable, IDisposable
    {
        private readonly PlayerInputActions     _actions;
        private readonly List<IInputHandler>    _handlers = new List<IInputHandler>();
        private bool _dirty;

        /// <summary>
        /// PlayerInputActions 를 외부에서 주입받는다.
        /// DI 컨테이너(VContainer)에서 생성된 인스턴스를 사용하므로
        /// 테스트에서 InputTestFixture 의 가상 디바이스와 연결된 인스턴스를 주입할 수 있다.
        /// </summary>
        public InputRouter(PlayerInputActions actions)
        {
            _actions = actions;
        }

        // ── 초기화 / 해제 ──────────────────────────

        public void Initialize()
        {
            _actions.Player.Interact.performed    += ctx => Route(GameInputAction.Interact,    ctx);
            _actions.Player.Attack.performed      += ctx => Route(GameInputAction.Attack,      ctx);
            _actions.Player.Dodge.performed       += ctx => Route(GameInputAction.Dodge,       ctx);
            _actions.Player.ToggleLobby.performed += ctx => Route(GameInputAction.ToggleLobby, ctx);
            _actions.Player.Pause.performed       += ctx => Route(GameInputAction.Pause,       ctx);

            // 맵 활성화는 전역(GlobalInputInitializer)이 소유한다. 여기서 Enable/Disable 하지 않는다.
            // (InputRouter는 Main 스코프 전용 → Dispose에서 전역 맵을 끄면 다음 씬(던전) 입력이 죽는다.)
        }

        public void Dispose()
        {
            // 전역 PlayerInputActions의 맵을 끄지 않는다(다른 씬과 공유). 라우팅 핸들러만 비운다.
            _handlers.Clear();
        }

        // ── IInputRouter ──────────────────────────

        public void Register(IInputHandler handler)
        {
            Debug.Log($"Registering input handler: {handler.GetType().Name}");
            if (!_handlers.Contains(handler))
            {
                Debug.Assert(handler.Priority >= 0, "Priority must be non-negative.");
                _handlers.Add(handler);
                _dirty = true;
            }
        }

        public void Unregister(IInputHandler handler)
        {
            Debug.Assert(handler != null, "Handler cannot be null.");
            if (_handlers.Remove(handler))
                _dirty = true;
        }

        // ── 라우팅 ───────────────────────────────

        private void Route(GameInputAction action, InputAction.CallbackContext ctx)
        {
            // 마우스 입력이 UI 위에서 발생한 경우 게임 핸들러에 전달하지 않는다.
            // IsPointerOverGameObject()는 InputAction 콜백 내에서 직전 프레임 결과를 반환하므로
            // RaycastAll로 직접 히트 테스트한다.
            if (ctx.control.device is Mouse mouse && IsPointerOverUI(mouse.position.ReadValue()))
                return;

            if (_dirty)
            {
                _handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                _dirty = false;
            }

            foreach (var handler in _handlers)
            {
                if (handler.TryHandle(action)) return; // consumed
            }
        }

        private static bool IsPointerOverUI(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;
            var pointerData = new PointerEventData(EventSystem.current) { position = screenPos };
            var results     = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);
            return results.Count > 0;
        }
    }
}
