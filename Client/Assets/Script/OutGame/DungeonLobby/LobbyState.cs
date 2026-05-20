using System.Collections.Generic;
using System.Linq;
using GameServer.Grpc.DungeonLobby;

namespace Game.OutGame.DungeonLobby
{
    /// <summary>
    /// 로비 화면의 불변 상태 스냅샷.
    /// WithXxx 메서드로만 새 State를 생성한다 — 직접 필드 수정 불가.
    /// </summary>
    public sealed class LobbyState
    {
        public readonly IReadOnlyList<DungeonRoomModel> Rooms;
        public readonly bool IsLoading;
        public readonly string ErrorMessage;       // null = 에러 없음
        public readonly DungeonRoomModel SelectedRoom; // null = 선택 없음

        public static readonly LobbyState Initial =
            new LobbyState(new DungeonRoomModel[0], false, null, null);

        public LobbyState(
            IReadOnlyList<DungeonRoomModel> rooms,
            bool isLoading,
            string errorMessage,
            DungeonRoomModel selectedRoom)
        {
            Rooms        = rooms;
            IsLoading    = isLoading;
            ErrorMessage = errorMessage;
            SelectedRoom = selectedRoom;
        }

        public LobbyState WithLoading() =>
            new LobbyState(Rooms, true, null, SelectedRoom);

        public LobbyState WithError(string message) =>
            new LobbyState(Rooms, false, message, SelectedRoom);

        public LobbyState WithRoomsLoaded(IReadOnlyList<RoomInfo> rooms) =>
            new LobbyState(
                rooms.Select(r => new DungeonRoomModel(r)).ToArray(),
                false, null, null); // 목록 새로 로드 시 선택 초기화

        public LobbyState WithRoomAdded(RoomInfo room)
        {
            var next = new List<DungeonRoomModel>(Rooms) { new DungeonRoomModel(room) };
            return new LobbyState(next, false, null, SelectedRoom);
        }

        public LobbyState WithRoomUpdated(RoomInfo room)
        {
            var next = new List<DungeonRoomModel>(Rooms);
            var idx  = next.FindIndex(m => m.Info.RoomId == room.RoomId);
            if (idx >= 0) next[idx] = new DungeonRoomModel(room);
            else          next.Add(new DungeonRoomModel(room));

            // 선택된 방이 업데이트됐으면 SelectedRoom도 같이 갱신
            var nextSelected = SelectedRoom?.Info.RoomId == room.RoomId
                ? new DungeonRoomModel(room)
                : SelectedRoom;
            return new LobbyState(next, false, null, nextSelected);
        }

        public LobbyState WithRoomSelected(DungeonRoomModel room) =>
            new LobbyState(Rooms, IsLoading, ErrorMessage, room);
    }
}
