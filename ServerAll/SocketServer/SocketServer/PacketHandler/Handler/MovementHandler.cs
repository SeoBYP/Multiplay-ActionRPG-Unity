using Serilog;
using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

public static class MovementHandler
{
    [PacketHandler(typeof(C_Move))]
    public static ValueTask HandleMove(Session session, C_Move packet, CancellationToken ct)
    {
        if (session.UserId <= 0)
        {
            return ValueTask.CompletedTask;
        }
        var room = session.Room;

        if (room is null)
        {
            return ValueTask.CompletedTask;
        }
        
        room.UpdatePlayerState(session.UserId, packet.PosX, packet.PosY, packet.PosZ, packet.RotY, packet.TimeStamp);
        room.Broadcast(BuildBroadcast(session.UserId, packet), session.SessionId);
        
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 브로드캐스트 패킷 조립 — 순수 변환이라 테스트로 고정한다(Broadcast 는 소켓 I/O 라 단위 테스트가 어렵다).
    /// <c>AnimState</c> 는 <b>해석 없이 그대로</b> 옮긴다: 연출은 클라 권위이고 서버는 중계만 한다.
    /// </summary>
    internal static S_Move BuildBroadcast(long userId, C_Move packet) => new()
    {
        UserId = userId,
        PosX = packet.PosX,
        PosY = packet.PosY,
        PosZ = packet.PosZ,
        RotY = packet.RotY,
        TimeStamp = packet.TimeStamp,
        AnimState = packet.AnimState,
    };
}