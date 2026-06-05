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

            public const string AlertPopup       = "Assets/Prefabs/GUI/Common/Popups/AlertPopup.prefab";
            public const string ConfirmPopup     = "Assets/Prefabs/GUI/Common/Popups/ConfirmPopup.prefab";
            public const string WarningPopup     = "Assets/Prefabs/GUI/Common/Popups/WarningPopup.prefab";
        }
    }
}
