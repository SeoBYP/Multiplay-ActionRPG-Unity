using System.Collections.Generic;
using System.Linq;
using GameServer.Grpc.DungeonLobby;

namespace Game.Presentation.DungeonLobby
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
        public readonly bool IsInRoom;             // 방 생성/입장 완료 상태

        /// <summary>
        /// 로그인한 나의 공개 식별자. 방 안에서 "내가 방장인가 / 내가 준비했는가"를 판정하는 기준이다.
        /// 서버가 플레이어를 public_id 로만 노출하므로 같은 키로 맞춘다. 인증 전에는 빈 문자열.
        /// </summary>
        public readonly string MyPublicId;

        public static readonly LobbyState Initial =
            new LobbyState(new DungeonRoomModel[0], false, null, null, false, "");

        public LobbyState(
            IReadOnlyList<DungeonRoomModel> rooms,
            bool isLoading,
            string errorMessage,
            DungeonRoomModel selectedRoom,
            bool isInRoom,
            string myPublicId)
        {
            Rooms        = rooms;
            IsLoading    = isLoading;
            ErrorMessage = errorMessage;
            SelectedRoom = selectedRoom;
            IsInRoom     = isInRoom;
            MyPublicId   = myPublicId;
        }

        /// <summary>인증 완료 후 내 공개 식별자를 심는다.</summary>
        public LobbyState WithIdentity(string myPublicId) =>
            new LobbyState(Rooms, IsLoading, ErrorMessage, SelectedRoom, IsInRoom, myPublicId);

        public LobbyState WithLoading() =>
            new LobbyState(Rooms, true, null, SelectedRoom, IsInRoom, MyPublicId);

        public LobbyState WithError(string message) =>
            new LobbyState(Rooms, false, message, SelectedRoom, IsInRoom, MyPublicId);

        public LobbyState WithRoomsLoaded(IReadOnlyList<RoomInfo> rooms) =>
            new LobbyState(
                rooms.Select(r => new DungeonRoomModel(r)).ToArray(),
                false, null, null, IsInRoom, MyPublicId);

        /// <summary>방 생성 또는 입장 성공 — IsInRoom = true, SelectedRoom 세팅.</summary>
        public LobbyState WithRoomJoined(RoomInfo room)
        {
            var next = new List<DungeonRoomModel>(Rooms);
            var idx  = next.FindIndex(m => m.Info.RoomId == room.RoomId);
            if (idx >= 0) next[idx] = new DungeonRoomModel(room);
            else          next.Add(new DungeonRoomModel(room));
            return new LobbyState(next, false, null, new DungeonRoomModel(room), true, MyPublicId);
        }

        /// <summary>방 나가기 성공 — IsInRoom = false, SelectedRoom 해제.</summary>
        public LobbyState WithRoomLeft() =>
            new LobbyState(Rooms, false, null, null, false, MyPublicId);

        public LobbyState WithRoomUpdated(RoomInfo room)
        {
            var next = new List<DungeonRoomModel>(Rooms);
            var idx  = next.FindIndex(m => m.Info.RoomId == room.RoomId);
            if (idx >= 0) next[idx] = new DungeonRoomModel(room);
            else          next.Add(new DungeonRoomModel(room));

            var nextSelected = SelectedRoom?.Info.RoomId == room.RoomId
                ? new DungeonRoomModel(room)
                : SelectedRoom;
            return new LobbyState(next, false, null, nextSelected, IsInRoom, MyPublicId);
        }

        public LobbyState WithRoomSelected(DungeonRoomModel room) =>
            new LobbyState(Rooms, IsLoading, ErrorMessage, room, IsInRoom, MyPublicId);
    }
}
