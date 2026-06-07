namespace Game.Presentation.InGame
{
    /// <summary>
    /// 인게임 화면에서 발생할 수 있는 사용자 의도의 닫힌 집합.
    /// View는 이것만 생성한다 — 처리 방법은 모른다.
    /// </summary>
    public abstract class InGameIntent
    {
        private InGameIntent() { }

        /// <summary>던전 → Main 씬으로 복귀.</summary>
        public sealed class ReturnToLobby : InGameIntent
        {
            public static readonly ReturnToLobby Instance = new ReturnToLobby();
            private ReturnToLobby() { }
        }

        /// <summary>인벤토리 창 토글(HUD 버튼·I키 공용 신호). 실제 창 로드/토글은 InventoryViewController가 담당.</summary>
        public sealed class ToggleInventory : InGameIntent
        {
            public static readonly ToggleInventory Instance = new ToggleInventory();
            private ToggleInventory() { }
        }
    }
}
