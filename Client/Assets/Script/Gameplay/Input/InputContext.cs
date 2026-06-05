using Game.System.Input;

namespace Game.Gameplay.Input
{
    /// <summary>
    /// <see cref="IInputContext"/> 구현. UI 점유 동안 게임플레이(Player) 액션 맵을 통째로 끈다.
    ///
    /// - ToggleLobby/Pause 포함 Player 맵 전체를 끈다 → 메뉴/모달 중에는 이동·점프뿐 아니라
    ///   L 토글, 단축키가 전부 죽는다(방 이름에 'L' 타이핑해도 로비가 닫히지 않는다).
    ///   UI 네비게이션/타이핑/Esc(Cancel)는 별도 UI 맵(항상 ON)이 담당하므로 영향 없음.
    /// - refcount(_uiDepth)로 중첩 모달 처리 — 마지막 하나가 닫힐 때만 복구.
    /// - 첫 진입 시 맵 활성 여부를 기억해 마지막 해제 시 그 상태로 복구(캐릭터 없는 씬 보호).
    /// - 맵 Disable 시 진행 중 입력에 canceled가 발사돼 CharacterInputBuffer가 자동 0이 된다.
    /// </summary>
    public sealed class InputContext : IInputContext
    {
        private readonly PlayerInputActions _actions;

        private int _uiDepth;
        private bool _restoreEnabled;

        public InputContext(PlayerInputActions actions)
        {
            _actions = actions;
        }

        public bool IsUiActive => _uiDepth > 0;

        public void EnterUi()
        {
            _uiDepth++;
            if (_uiDepth != 1)
                return; // 이미 점유 중

            _restoreEnabled = _actions.Player.enabled;
            _actions.Player.Disable();
            UnityEngine.Debug.Log($"[InputContext] EnterUi → Player 맵 OFF (was enabled={_restoreEnabled})");
        }

        public void ExitUi()
        {
            if (_uiDepth == 0)
                return; // underflow 방어

            _uiDepth--;
            if (_uiDepth != 0)
                return; // 아직 다른 UI가 점유 중

            if (_restoreEnabled)
                _actions.Player.Enable();
            UnityEngine.Debug.Log($"[InputContext] ExitUi → Player 맵 복구 (restore={_restoreEnabled})");
        }
    }
}
