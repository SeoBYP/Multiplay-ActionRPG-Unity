using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Server.Room;

namespace Server.Monster;

/// <summary>
/// 서버 권위 몬스터 시뮬레이션 루프. 고정 10Hz 로 모든 방의 몬스터를 진행(이동/페이즈)하고
/// S_MonsterState 를 방에 브로드캐스트한다. AI 수식은 MonsterAiMath(순수), 상태/락은 Room.TickMonsters.
///
/// BackgroundService 예외가 호스트를 종료하지 않도록 틱 본문을 try/catch 로 감싼다(서버는 멈추면 안 됨).
/// </summary>
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
            // 정상 종료
        }
    }

    private void TickAllRooms()
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var room in roomManager.GetAllRooms())
        {
            var packets = room.TickMonsters(Dt, nowMs);
            foreach (var packet in packets)
                room.Broadcast(packet);
        }
    }
}
