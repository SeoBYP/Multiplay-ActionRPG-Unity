using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// S_SpawnGroundItem 수신 → 상태 저장소에 바닥 아이템 추가(이벤트 발행). GroundItemSpawner가 엔티티를 스폰한다.
    /// (네트워크 레이어는 프리팹/씬을 모른다 — 몬스터 동기화와 동일한 state 경유 패턴)
    /// </summary>
    public class SpawnGroundItemPacketHandler : PacketHandlerBase<S_SpawnGroundItem>
    {
        private readonly ISocketPacketState _state;

        public SpawnGroundItemPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_SpawnGroundItem packet)
        {
            _state.AddGroundItem(new SocketGroundItemSnapshot(
                packet.GroundId,
                packet.ItemId,
                packet.Qty,
                packet.PosX, packet.PosY, packet.PosZ));
            return UniTask.CompletedTask;
        }
    }

    /// <summary>S_GroundItemRemoved 수신 → 상태 저장소에서 제거 + OnGroundItemRemoved 발행(GroundItemSpawner 디스폰).</summary>
    public class GroundItemRemovedPacketHandler : PacketHandlerBase<S_GroundItemRemoved>
    {
        private readonly ISocketPacketState _state;

        public GroundItemRemovedPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_GroundItemRemoved packet)
        {
            _state.RemoveGroundItem(packet.GroundId);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>S_ItemPickedUp 수신(줍은 본인) → OnItemPickedUp 발행(획득 토스트 표시).</summary>
    public class ItemPickedUpPacketHandler : PacketHandlerBase<S_ItemPickedUp>
    {
        private readonly ISocketPacketState _state;

        public ItemPickedUpPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_ItemPickedUp packet)
        {
            _state.NotifyItemPickedUp(packet.ItemId, packet.Qty);
            return UniTask.CompletedTask;
        }
    }
}
