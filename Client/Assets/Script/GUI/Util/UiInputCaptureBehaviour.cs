using System;
using UnityEngine;

namespace Game.GUI
{
    /// <summary>
    /// 붙은 GameObject가 활성인 동안 게임플레이 입력 점유를 잡는다.
    /// 해제를 <c>OnDisable</c>에 묶어 SetActive(false)·Destroy 어느 쪽이든 해제된다 — 버튼 onClick 리스너에 의존하지 않는다
    /// (onClick 도중 계층을 끄면 뒤에 등록한 리스너가 잘릴 수 있다).
    /// begin/end 를 Action 쌍으로 주입하므로 GUI 레이어가 IInputContext(System)를 참조하지 않는다(보통 Presentation Model 의 BeginUiCapture/EndUiCapture).
    /// </summary>
    public sealed class UiInputCaptureBehaviour : MonoBehaviour
    {
        private Action _begin;
        private Action _end;
        private bool _bound;
        private bool _captured;

        /// <summary>점유 시작/해제 동작을 주입. 이미 활성이면 즉시 획득(주입이 OnEnable보다 늦어도 안전).</summary>
        public void Bind(Action begin, Action end)
        {
            _begin = begin;
            _end   = end;
            _bound = true;

            if (isActiveAndEnabled)
                Acquire();
        }

        private void OnEnable()  => Acquire();
        private void OnDisable() => Release();

        private void Acquire()
        {
            if (!_bound || _captured) return;
            _captured = true;
            _begin?.Invoke();
        }

        private void Release()
        {
            if (!_captured) return;
            _captured = false;
            _end?.Invoke();
        }
    }
}
