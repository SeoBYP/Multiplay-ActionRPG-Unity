using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Game.Input
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
            _actions.Player.Interact.performed    += _ => Route(GameInputAction.Interact);
            _actions.Player.Attack.performed      += _ => Route(GameInputAction.Attack);
            _actions.Player.Dodge.performed       += _ => Route(GameInputAction.Dodge);
            _actions.Player.ToggleLobby.performed += _ => Route(GameInputAction.ToggleLobby);
            _actions.Player.Pause.performed       += _ => Route(GameInputAction.Pause);

            _actions.Player.Enable();
        }

        public void Dispose()
        {
            _actions.Player.Disable();
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

        private void Route(GameInputAction action)
        {
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
    }
}
