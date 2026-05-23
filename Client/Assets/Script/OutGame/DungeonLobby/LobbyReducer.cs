namespace Game.OutGame.DungeonLobby
{
    /// <summary>
    /// 순수 함수: (OldState, Result) → NewState.
    /// 네트워크 호출, Time.time, Random 등 부수 효과 절대 금지.
    /// </summary>
    public static class LobbyReducer
    {
        public static LobbyState Reduce(LobbyState state, LobbyResult result)
        {
            if (result is LobbyResult.Loading)
                return state.WithLoading();

            if (result is LobbyResult.RoomsLoaded loaded)
                return state.WithRoomsLoaded(loaded.Rooms);

            if (result is LobbyResult.RoomCreated created)
                return state.WithRoomJoined(created.Room);

            if (result is LobbyResult.RoomJoined joined)
                return state.WithRoomJoined(joined.Room);

            if (result is LobbyResult.RoomUpdated updated)
                return state.WithRoomUpdated(updated.Room);

            if (result is LobbyResult.RoomLeft)
                return state.WithRoomLeft();

            if (result is LobbyResult.Failed failed)
                return state.WithError(failed.Message);

            if (result is LobbyResult.RoomSelected selected)
                return state.WithRoomSelected(selected.Room);

            return state;
        }
    }
}
