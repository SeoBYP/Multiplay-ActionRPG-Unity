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

        /// <summary>장비 창 단독 토글(K키 전용). 실제 창 로드/토글은 EquipmentViewController가 담당.</summary>
        public sealed class ToggleEquipment : InGameIntent
        {
            public static readonly ToggleEquipment Instance = new ToggleEquipment();
            private ToggleEquipment() { }
        }

        public sealed class ToggleShop : InGameIntent
        {
            public static readonly ToggleShop Instance = new ToggleShop();
            private ToggleShop() { }
        }

        /// <summary>퀘스트 창 단독 토글(HUD 퀘스트버튼). 실제 창 로드/토글은 QuestViewController가 담당.</summary>
        public sealed class ToggleQuest : InGameIntent
        {
            public static readonly ToggleQuest Instance = new ToggleQuest();
            private ToggleQuest() { }
        }

        /// <summary>캐릭터 정보/스탯창 단독 토글(HUD Ability버튼·G키). 실제 창 로드/토글은 StatViewController가 담당.</summary>
        public sealed class ToggleAbility : InGameIntent
        {
            public static readonly ToggleAbility Instance = new ToggleAbility();
            private ToggleAbility() { }
        }
    }
}
