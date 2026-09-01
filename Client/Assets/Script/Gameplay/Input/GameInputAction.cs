namespace Game.Gameplay.Input
{
    /// <summary>
    /// 게임 전체에서 사용하는 입력 액션 목록.
    ///
    /// UnityEngine.InputSystem.InputAction 과 이름 충돌을 피하기 위해 GameInputAction 으로 명명.
    /// PlayerInputActions(generated wrapper) 이벤트를 수신한 InputRouter 가
    /// 이 enum 으로 변환 후 IInputHandler 체인에 전달한다.
    /// </summary>
    public enum GameInputAction
    {
        // ── UI ──────────────────────────────────────
        ToggleLobby,       // L
        ToggleInventory,   // I  — 인벤토리+장비 쌍 토글
        ToggleEquipment,   // K  — 장비창 단독 토글
        ToggleQuest,       // Q  — 퀘스트창 단독 토글
        ToggleAbility,     // G  — 어빌리티창 단독 토글
        Chat,              // Enter — 채팅 입력 열기(전송 Enter 는 UI 맵의 InputField 가 받는다)

        // ── 월드 인터랙션 ─────────────────────────
        Interact,          // E  — 아이템 줍기 / 문 열기 / NPC 대화

        // ── 전투 ────────────────────────────────────
        Attack,            // 마우스 좌클릭
        Dodge,             // LeftCtrl

        // ── 시스템 ──────────────────────────────────
        Pause,             // Escape
    }
}
