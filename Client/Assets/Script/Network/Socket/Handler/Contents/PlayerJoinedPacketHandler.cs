using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// 플레이어 입장 응답을 받아 로컬 플레이어 목록 상태를 갱신한다.
    /// </summary>
    public class PlayerJoinedPacketHandler : PacketHandlerBase<S_PlayerJoined>
    {
        private readonly ISocketPacketState _state;

        /// <summary>
        /// 입장 정보를 반영할 상태 저장소를 주입받는다.
        /// </summary>
        public PlayerJoinedPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        /// <summary>
        /// 방 입장 성공 시 플레이어 정보를 추가하고, 실패 시 상태 변경 없이 종료한다.
        /// </summary>
        public override UniTask HandleAsync(S_PlayerJoined packet)
        {
            // 실패 응답은 세션 상태만 상위에서 처리하고 플레이어 목록은 건드리지 않는다.
            if (!packet.Success)
            {
                return UniTask.CompletedTask;
            }

            // 성공 시 새 플레이어 스냅샷을 추가하거나 갱신한다.
            _state.UpsertPlayer(
                packet.UserId,
                packet.Nickname,
                packet.SpawnIndex,
                packet.MapId,
                packet.PosX,
                packet.PosY,
                packet.PosZ,
                packet.RotY);
            return UniTask.CompletedTask;
        }
    }
}
