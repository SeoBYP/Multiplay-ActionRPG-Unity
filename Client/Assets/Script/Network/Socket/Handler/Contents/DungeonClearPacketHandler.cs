using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;
using UnityEngine;

namespace Game.Network.Socket
{
    /// <summary>
    /// 던전 클리어 패킷 처리. 서버가 방의 몬스터 전멸을 감지해 S_DungeonClear 를 브로드캐스트하면
    /// 클리어 신호(OnDungeonCleared)를 발행한다. Presentation(InGameModel)이 결과 화면→로비 복귀에 사용.
    /// </summary>
    public class DungeonClearPacketHandler : PacketHandlerBase<S_DungeonClear>
    {
        private readonly ISocketPacketState _state;

        public DungeonClearPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_DungeonClear packet)
        {
            Debug.Log($"[DungeonClearPacketHandler] S_DungeonClear 수신 — RoomId={packet.RoomId} RewardExp={packet.RewardExp}");
            _state.MarkDungeonCleared(packet.RewardExp);
            return UniTask.CompletedTask;
        }
    }
}
