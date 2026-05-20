using System;
using VContainer.Unity;

namespace Game.Input
{
    /// <summary>
    /// 플레이어 주변의 IInteractable을 추적하고 F키 입력을 처리하는 시스템.
    ///
    /// 우선순위(Priority = 50): UI(100)보다 낮아서
    /// UI가 열려 있으면 F키를 UI가 먼저 소비하고 이 시스템은 동작하지 않는다.
    ///
    /// InteractionDetector(MonoBehaviour, 플레이어에 부착)가
    /// SetCurrent()로 가장 가까운 IInteractable을 설정한다.
    /// </summary>
    public sealed class InteractionSystem : IInputHandler, IInitializable, IDisposable
    {
        private readonly IInputRouter _router;

        private IInteractable _current;

        public int Priority => 50;

        /// <summary>범위 안 IInteractable이 바뀔 때 발생. UI 힌트 갱신에 사용.</summary>
        public event Action<IInteractable> OnInteractableChanged;

        /// <summary>현재 범위 내 IInteractable. null이면 근처에 없음.</summary>
        public IInteractable Current => _current;

        public InteractionSystem(IInputRouter router)
        {
            _router = router;
        }

        public void Initialize() => _router.Register(this);
        public void Dispose()    => _router.Unregister(this);

        // ── InteractionDetector가 호출 ────────────

        /// <summary>플레이어가 IInteractable 범위에 들어오거나 나갈 때 호출.</summary>
        public void SetCurrent(IInteractable interactable)
        {
            if (_current == interactable) return;
            _current = interactable;
            OnInteractableChanged?.Invoke(_current);
        }

        // ── IInputHandler ─────────────────────────

        public bool TryHandle(GameInputAction action)
        {
            if (action != GameInputAction.Interact) return false;
            if (_current == null || !_current.CanInteract) return false;

            _current.Interact();
            return true; // consumed
        }
    }
}
