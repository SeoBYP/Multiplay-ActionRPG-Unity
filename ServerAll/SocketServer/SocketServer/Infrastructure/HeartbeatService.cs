using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Server.Room;

namespace Server.Infrastructure;

public class HeartBeatService(
    SessionManager sessionManager,
    RoomManager roomManager,
    ILogger<HeartBeatService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    /// <summary>방에 없는 세션 타임아웃 — 로비 대기 중인 유휴 연결 정리.</summary>
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(30);

    /// <summary>방 안 플레이어 타임아웃 — 1분간 패킷 없으면 퇴장 처리.</summary>
    private static readonly TimeSpan RoomPlayerTimeout = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "HeartBeatService started. CheckInterval={CheckInterval}, SessionTimeout={SessionTimeout}, RoomPlayerTimeout={RoomPlayerTimeout}",
            CheckInterval, SessionTimeout, RoomPlayerTimeout);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);

                var sessions = sessionManager.GetSessionsSnapshot();
                var now = DateTime.UtcNow;

                foreach (var session in sessions)
                {
                    var timeout = session.Room != null ? RoomPlayerTimeout : SessionTimeout;

                    if (now - session.LastRecvAt <= timeout)
                        continue;

                    if (session.Room != null)
                    {
                        // 방 안 플레이어 타임아웃:
                        // LeaveRoom을 먼저 호출해 S_PlayerLeft 브로드캐스트 + 빈 방 정리를 즉시 처리.
                        // 이후 Disconnect가 Session.RunAsync finally를 트리거해도 LeaveRoom은 멱등하다.
                        logger.LogWarning(
                            "Room player timed out — SessionId={SessionId} UserId={UserId} RoomId={RoomId} LastRecvAt={LastRecvAt}",
                            session.SessionId, session.UserId, session.Room.RoomId, session.LastRecvAt);

                        roomManager.LeaveRoom(session);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Session timed out — SessionId={SessionId} UserId={UserId} LastRecvAt={LastRecvAt}",
                            session.SessionId, session.UserId, session.LastRecvAt);
                    }

                    session.Disconnect();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error in HeartBeatService loop");
            }
        }
    }
}
