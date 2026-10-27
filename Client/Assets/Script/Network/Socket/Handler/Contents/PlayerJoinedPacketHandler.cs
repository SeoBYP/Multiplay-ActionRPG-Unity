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

        public PlayerJoinedPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_PlayerJoined packet)
        {
            // 실패 응답은 세션 상태만 상위에서 처리하고 플레이어 목록은 건드리지 않는다.
            if (!packet.Success)
            {
                return UniTask.CompletedTask;
            }

            _state.UpsertPlayer(
                packet.UserId,
                packet.Nickname,
                packet.SpawnIndex,
                packet.MapId,
                packet.PosX,
                packet.PosY,
                packet.PosZ,
                packet.RotY,
                timeStamp: 0,
                hp: packet.Hp,
                maxHp: packet.MaxHp);
            return UniTask.CompletedTask;
        }
    }
}
