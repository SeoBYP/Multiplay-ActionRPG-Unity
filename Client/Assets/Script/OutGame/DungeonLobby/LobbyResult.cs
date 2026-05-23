using System.Collections.Generic;
using GameServer.Grpc.DungeonLobby;

namespace Game.OutGame.DungeonLobby
{
    /// <summary>
    /// Effect(비동기 처리)의 출력물. Reducer의 입력.
    /// </summary>
    public abstract class LobbyResult
    {
        private LobbyResult() { }

        public sealed class Loading : LobbyResult
        {
            public static readonly Loading Instance = new Loading();
            private Loading() { }
        }

        public sealed class RoomsLoaded : LobbyResult
        {
            public readonly IReadOnlyList<RoomInfo> Rooms;
            public RoomsLoaded(IReadOnlyList<RoomInfo> rooms) => Rooms = rooms;
        }

        public sealed class RoomCreated : LobbyResult
        {
            public readonly RoomInfo Room;
            public RoomCreated(RoomInfo room) => Room = room;
        }

        public sealed class RoomJoined : LobbyResult
        {
            public readonly RoomInfo Room;
            public RoomJoined(RoomInfo room) => Room = room;
        }

        public sealed class Failed : LobbyResult
        {
            public readonly string Message;
            public Failed(string message) => Message = message;
        }

        /// <summary>방 선택 — State에 SelectedRoom 반영.</summary>
        public sealed class RoomSelected : LobbyResult
        {
            public readonly DungeonRoomModel Room;
            public RoomSelected(DungeonRoomModel room) => Room = room;
        }

        /// <summary>SubscribeRoom 스트림으로 방 정보가 갱신됐을 때.</summary>
        public sealed class RoomUpdated : LobbyResult
        {
            public readonly RoomInfo Room;
            public RoomUpdated(RoomInfo room) => Room = room;
        }

        public sealed class RoomLeft : LobbyResult
        {
            public static readonly RoomLeft Instance = new RoomLeft();
            private RoomLeft() { }
        }
    }
}
