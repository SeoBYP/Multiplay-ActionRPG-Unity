using GameServer.Grpc.DungeonLobby;

namespace Game.System.DungeonLobby
{
    /// <summary>
    /// 현재 참가 중인 방의 런타임 상태를 보관한다.
    /// DungeonLobbyService가 유일한 소유자이며, UI는 읽기만 한다.
    /// </summary>
    public class DungeonLobbySession
    {
        public RoomInfo? CurrentRoom { get; private set; }

        public bool IsInRoom => CurrentRoom != null;

        public void SetRoom(RoomInfo room) => CurrentRoom = room;

        public void ClearRoom() => CurrentRoom = null;
    }
}
