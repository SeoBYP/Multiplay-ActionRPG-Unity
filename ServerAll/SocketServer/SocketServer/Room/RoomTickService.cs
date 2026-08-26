using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Server.Room;

/// <summary>
/// <b>서버 권위 틱 호스트</b>. 고정 10Hz 로 모든 방을 한 번씩 진행시키고 결과 패킷을 브로드캐스트한다.
///
/// <para>이 클래스가 하는 일은 <b>주기와 전송</b>뿐이다 — 무엇을 진행시킬지는 <see cref="Room.Tick"/>,
/// 몬스터 AI 수식은 <c>MonsterAiMath</c>(순수), 상태·락은 <c>ActorStore</c> 가 맡는다.
/// (원래는 몬스터 전용 루프라 <c>Monster/</c> 에 있었지만, 플레이어 마나 회복·재접속 유예 스윕까지
/// 흡수하면서 몬스터 타입을 하나도 참조하지 않게 됐다.)</para>
///
/// <para>BackgroundService 예외가 호스트를 종료하지 않도록 틱 본문을 try/catch 로 감싼다(서버는 멈추면 안 된다).</para>
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
            var packets = room.Tick(Dt, nowMs);
            foreach (var packet in packets)
                room.Sessions.Broadcast(packet);
        }

        // 재접속 유예 만료된 끊김 플레이어 정리(영구 퇴장 확정 + association 정리).
        roomManager.SweepDisconnectedPlayers(nowMs);
    }
}
