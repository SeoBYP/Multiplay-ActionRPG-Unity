using System.Collections.Generic;
using System.Linq;
using GameServer.Grpc.DungeonLobby;

namespace Game.Presentation.DungeonLobby
{
    /// <summary>
    /// 로비 화면에서 방 1개를 표현하는 UI 모델.
    /// RoomInfo(proto)를 래핑하며, Game.GUI 레이어가 proto 타입에 직접 의존하지 않도록
    /// 도메인 타입 프로퍼티를 노출한다.
    ///
    /// Info 프로퍼티는 Game.Presentation 내부(LobbyState, LobbyResult 등)에서만 사용한다.
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
        /// <summary>이 방의 던전 식별자(spawn-layouts.json 키). 표시이름 변환은 DungeonCatalog 사용.</summary>
        public string MapId      => Info.MapId;
        /// <summary>방장의 공개 식별자. 슬롯·자기자신 판정을 public_id 한 키로 통일하려고 서버가 함께 보낸다.</summary>
        public string HostPublicId => Info.HostPublicId;
        public IReadOnlyList<RoomPlayerInfo> Players { get; }

        /// <summary>방장을 뺀 전원이 준비됐는가. 호스트의 시작 버튼 활성 조건이다(판정 권위는 서버).</summary>
        public bool AllOthersReady { get; }

        public DungeonRoomModel(RoomInfo info)
        {
            Info = info;

            var readyIds = new HashSet<string>(info.ReadyPublicIds);
            Players = info.CurrentPlayers
                .Select(u => new RoomPlayerInfo(
                    u.PublicId,
                    u.NickName,
                    isHost: !string.IsNullOrEmpty(info.HostPublicId) && u.PublicId == info.HostPublicId,
                    isReady: readyIds.Contains(u.PublicId)))
                .ToArray();

            AllOthersReady = Players.All(p => p.IsHost || p.IsReady);
        }

        /// <summary>주어진 플레이어가 이 방의 방장인가.</summary>
        public bool IsHost(string publicId) =>
            !string.IsNullOrEmpty(publicId) && publicId == Info.HostPublicId;

        /// <summary>주어진 플레이어가 준비 상태인가. 방을 벗어난 식별자면 false.</summary>
        public bool IsReady(string publicId)
        {
            for (var i = 0; i < Players.Count; i++)
                if (Players[i].PublicId == publicId) return Players[i].IsReady;
            return false;
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
