using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

// 서버 권위: C_PickupItem → Room.TryPickup(경쟁 중재) → 방 브로드캐스트 + 본인 토스트 + GameServer 영속 지급 발행.
public static class LootHandler
{
    [PacketHandler(typeof(C_PickupItem))]
    public static ValueTask HandlePickup(Session session, C_PickupItem packet, CancellationToken ct)
    {
        if (session.UserId <= 0)
            return ValueTask.CompletedTask;

        var room = session.Room;
        if (room is null)
            return ValueTask.CompletedTask;

        // 시전자 위치를 먼저 스냅샷 — 액터 락과 아이템 락을 중첩하지 않기 위해서.
        var picker = room.Actors.GetMember(session.UserId)?.Actor;
        if (picker is null)
            return ValueTask.CompletedTask;

        var item = room.Loot.TryPickup(picker.PosX, picker.PosZ, packet.GroundId);
        if (item is null)
            return ValueTask.CompletedTask;

        room.Sessions.Broadcast(new S_GroundItemRemoved { GroundId = item.GroundId });

        _ = session.SendPacketAsync(new S_ItemPickedUp { ItemId = item.ItemId, Qty = item.Qty });

        session.RoomManager.PublishItemPickup(room, session.UserId, item);

        return ValueTask.CompletedTask;
    }
}
