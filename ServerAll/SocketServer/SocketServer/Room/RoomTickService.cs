using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Server.Room;

// 주기와 전송만 — 진행은 Room.Tick. 틱 본문을 try/catch 로 감싸 예외가 호스트를 종료하지 않게 한다.
public class RoomTickService(
    RoomManager roomManager,
    ILogger<RoomTickService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100); // 10Hz
    private const float Dt = 0.1f;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RoomTickService started (interval={Interval}ms)", TickInterval.TotalMilliseconds);

        using var timer = new PeriodicTimer(TickInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    TickAllRooms();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error in RoomTickService tick");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private void TickAllRooms()
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var room in roomManager.GetAllRooms())
        {
            var packets = room.Tick(Dt, nowMs);
            foreach (var packet in packets)
                room.Sessions.Broadcast(packet);
        }

        roomManager.SweepDisconnectedPlayers(nowMs);
    }
}
