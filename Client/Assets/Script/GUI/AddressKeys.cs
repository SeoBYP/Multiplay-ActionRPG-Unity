namespace Game.GUI
{
    /// <summary>
    /// Addressables 로드 키 상수.
    /// Inspector의 Address 필드와 반드시 일치해야 한다.
    /// </summary>
    public static class AddressKeys
    {
        public static class UI
        {
            public const string LobbyView        = "Assets/Prefabs/GUI/DungeonLobby/DungeonRoomLobbyView.prefab";
            public const string CreateRoomPopup  = "Assets/Prefabs/GUI/DungeonLobby/CreateDungeonRoomPopup.prefab";
            public const string RoomDetailView   = "Assets/Prefabs/GUI/DungeonLobby/DungeonRoomDetail.prefab";
            public const string GameHud          = "Assets/Prefabs/GUI/HUD/GameHud.prefab";
            public const string Inventory        = "Assets/Prefabs/GUI/Inventory/Inventory.prefab";
            public const string Equipment        = "Assets/Prefabs/GUI/Equipment/Equipment.prefab";
            public const string ItemActionPanel  = "Assets/Prefabs/GUI/Inventory/ItemActionPanel.prefab";
            public const string Shop             = "Assets/Prefabs/GUI/Shop/Shop.prefab";
            public const string Quest = "Assets/Prefabs/GUI/Quest/Quest.prefab";
            public const string StatWindow       = "Assets/Prefabs/GUI/Stat/StatWindow.prefab";
            public const string Dialogue         = "Assets/Prefabs/GUI/Dialogue/Dialogue.prefab";
            
            // 상점 슬롯(동적 생성용 prefab — Addressable 로드).
            public const string ShopItem         = "Assets/Prefabs/GUI/Shop/Shop_Item.prefab";
            public const string ShopStatusSlot   = "Assets/Prefabs/GUI/Shop/Status_Slot.prefab";

            // 퀘스트 슬롯(동적 생성용 prefab — Addressable 로드).
            public const string QUEST_SLOT_KEY = "Assets/Prefabs/GUI/Quest/Quest_Slot.prefab";
            public const string QUEST_CONDITION_SLOT_KEY = "Assets/Prefabs/GUI/Quest/Quest_Condition_Slot.prefab";
            public const string QUEST_REWARD_SLOT_KEY = "Assets/Prefabs/GUI/Quest/Quest_Reward_Slot.prefab";
            
            // 인벤토리 슬롯(동적 생성용 prefab — Addressable 로드).
            public const string UniversalSlot    = "Assets/Prefabs/GUI/Common/Slot/UniversalSlot.prefab";
            public const string ItemContentsSlot = "Assets/Prefabs/GUI/Common/Slot/Contents/ItemContentsSlot.prefab";

            public const string AlertPopup       = "Assets/Prefabs/GUI/Common/Popups/AlertPopup.prefab";
            public const string ConfirmPopup     = "Assets/Prefabs/GUI/Common/Popups/ConfirmPopup.prefab";
            public const string WarningPopup     = "Assets/Prefabs/GUI/Common/Popups/WarningPopup.prefab";
        }

        /// <summary>
        /// 게임 데이터 SO Addressable 주소(= 에셋 경로). Resources 폐기 후 Addressables 로드용.
        /// LifetimeScope 가 동기 등록 시 LoadAssetAsync(...).WaitForCompletion() 으로 읽는다(로컬 번들).
        /// </summary>
        public static class Data
        {
            public const string EffectIconCatalog  = "Assets/GameData/Effects/EffectIconCatalog.asset";
            public const string ItemDisplayCatalog = "Assets/GameData/Item/ItemDisplayCatalog.asset";
            public const string GradeSpriteCatalog = "Assets/GameData/Item/GradeSpriteCatalog.asset";
            public const string ConsumableCatalog  = "Assets/GameData/Consumable/ConsumableCatalog.asset";
            public const string DialogueCatalog    = "Assets/GameData/Dialogue/DialogueCatalog.asset";
            public const string SkillCatalog       = "Assets/GameData/Skill/SkillCatalogDefinition.asset";
        }
    }
}
