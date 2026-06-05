namespace Game.System.Input
{
    /// <summary>
    /// 입력 컨텍스트 스위치 — UI가 입력을 점유하는 동안 게임플레이(Player) 액션 맵을 끈다.
    ///
    /// UI 측(모달을 여는 View가 자기 Model을 통해)이 EnterUi/ExitUi 로 점유를 push/pop 하고,
    /// 구현체(Gameplay.Input.InputContext)가 그 깊이(refcount)에 따라
    /// PlayerInputActions.Player 맵을 Enable/Disable 한다.
    ///
    /// 게임플레이 입력 어댑터(PlayerInputComponent/InputRouter)는 아무것도 안 한다 —
    /// 맵이 꺼지면 콜백 자체가 안 오기 때문(Unity Input System 기본기).
    ///
    /// 위치: 인터페이스는 소비자(Presentation Model)가 닿는 Game.System에 둔다(DIP).
    ///       구현은 PlayerInputActions가 있는 Game.Gameplay.Input에 둔다.
    /// </summary>
    public interface IInputContext
    {
        /// <summary>UI 입력 점유 시작(모달 열림). 첫 진입에서 Player 맵을 끈다.</summary>
        void EnterUi();

        /// <summary>UI 입력 점유 종료. 마지막 해제에서 Player 맵을 원래 상태로 되돌린다.</summary>
        void ExitUi();

        /// <summary>현재 UI가 입력을 점유 중인가(중첩 포함).</summary>
        bool IsUiActive { get; }
    }
}
