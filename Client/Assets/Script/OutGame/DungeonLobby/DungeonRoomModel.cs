using System.Collections.Generic;
using System.Linq;
using GameServer.Grpc.DungeonLobby;

namespace Game.OutGame.DungeonLobby
{
    /// <summary>
    /// 로비 화면에서 방 1개를 표현하는 UI 모델.
    /// RoomInfo(proto)를 래핑하며, Game.GUI 레이어가 proto 타입에 직접 의존하지 않도록
    /// 도메인 타입 프로퍼티를 노출한다.
    ///
    /// Info 프로퍼티는 Game.OutGame 내부(LobbyState, LobbyResult 등)에서만 사용한다.
    /// Game.GUI View는 반드시 도메인 프로퍼티(RoomId, RoomName, Status, Players …)를 사용한다.
    /// </summary>
    public sealed class DungeonRoomModel
    {
        /// <summary>OutGame 레이어 내부용. Game.GUI에서 직접 접근 금지.</summary>
        public readonly RoomInfo Info;

        // ── GUI 레이어에서 사용하는 도메인 프로퍼티 ──────

        public long   RoomId     => Info.RoomId;
        public string RoomName   => Info.RoomName;
        public int    MaxPlayers => Info.MaxPlayers;
        public int    PlayerCount => Players.Count;
        public RoomStatus Status => MapStatus(Info.Status);
        public IReadOnlyList<RoomPlayerInfo> Players { get; }

        public DungeonRoomModel(RoomInfo info)
        {
            Info    = info;
            Players = info.CurrentPlayers
                .Select(u => new RoomPlayerInfo(u.PublicId, u.NickName))
                .ToArray();
        }

        private static RoomStatus MapStatus(RoomStatusType proto)
        {
            switch (proto)
            {
                case RoomStatusType.Waiting:  return RoomStatus.Waiting;
                case RoomStatusType.Starting: return RoomStatus.Starting;
                case RoomStatusType.Playing:  return RoomStatus.Playing;
                case RoomStatusType.Closed:   return RoomStatus.Closed;
                default:                      return RoomStatus.Waiting;
            }
        }
    }
}
