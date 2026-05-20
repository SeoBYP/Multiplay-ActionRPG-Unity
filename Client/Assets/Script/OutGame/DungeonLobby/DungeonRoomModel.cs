using GameServer.Grpc.DungeonLobby;

namespace Game.OutGame.DungeonLobby
{
    /// <summary>
    /// 로비 화면에서 방 1개를 표현하는 UI 모델.
    /// RoomInfo(proto)를 래핑하며, 파생값(인원수 텍스트 등)은 View에서 계산한다.
    /// </summary>
    public sealed class DungeonRoomModel
    {
        public readonly RoomInfo Info;

        public DungeonRoomModel(RoomInfo info)
        {
            Info = info;
        }
    }
}
