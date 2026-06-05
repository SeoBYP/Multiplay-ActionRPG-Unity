using System;
using UnityEngine;

namespace Game.GUI
{
    /// <summary>
    /// 붙은 GameObject가 활성인 동안 게임플레이 입력 점유를 잡는다.
    ///
    /// 핵심: 점유 해제를 <c>OnDisable</c>에 묶는다 → SetActive(false)로 숨기든(예: 로비 X 버튼),
    /// Destroy로 닫든 항상 해제된다. 버튼 onClick 리스너에 의존하지 않아 안전하다
    /// (X 버튼의 SetActive가 onClick 도중 계층을 끄면 뒤에 등록한 리스너가 잘릴 수 있음).
    ///
    /// 캡처는 Action 쌍(begin/end)으로 주입 — IInputContext(System)를 직접 참조하지 않아
    /// GUI 레이어에서 사용 가능하고, 테스트에서 카운터/가짜로 교체하기 쉽다.
    /// 호출처는 보통 Presentation Model의 BeginUiCapture/EndUiCapture를 넘긴다.
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
