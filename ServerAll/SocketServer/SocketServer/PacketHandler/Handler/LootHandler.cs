using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

/// <summary>
/// 루트/줍기 패킷 핸들러 (3.3 던전 경로 — 서버 권위).
///
/// C_PickupItem 수신 → Room.TryPickup(경쟁 중재·거리 검증, 1명만 성공) →
///   ① S_GroundItemRemoved 방 브로드캐스트(모든 클라 바닥서 제거)
///   ② S_ItemPickedUp 본인 송신(획득 토스트)
///   ③ ItemPickedUpMessage 발행(GameServer 가 인벤토리에 영속 지급)
///
/// 클라는 "먹을지" 의도만 보낸다 — roll·줍기 승자·지급은 전부 서버 권위(loot-drop.md §1.2).
/// </summary>
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

        // 시전자 위치를 먼저 스냅샷해 저장소에 넘긴다 — 액터 락과 아이템 락을 중첩하지 않기 위해서다.
        var picker = room.Actors.GetMember(session.UserId)?.Actor;
        if (picker is null)
            return ValueTask.CompletedTask;

        // 경쟁 중재: 제거 성공한 1명만 non-null. 패배/범위 밖/미존재면 조용히 무시.
        var item = room.Loot.TryPickup(picker.PosX, picker.PosZ, packet.GroundId);
        if (item is null)
            return ValueTask.CompletedTask;

        // ① 모든 클라 바닥서 제거
        room.Sessions.Broadcast(new S_GroundItemRemoved { GroundId = item.GroundId });

        // ② 줍은 본인에게 획득 토스트(push). 인벤토리 수치 갱신은 클라가 GetInventory(pull).
        _ = session.SendPacketAsync(new S_ItemPickedUp { ItemId = item.ItemId, Qty = item.Qty });

        // ③ GameServer 영속 지급(멱등). PickupId 구성·발행은 RoomManager 책임.
        session.RoomManager.PublishItemPickup(room, session.UserId, item);

        return ValueTask.CompletedTask;
    }
}
