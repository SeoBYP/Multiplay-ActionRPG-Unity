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

    private static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(30);

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
                        // 무응답 = 네트워크 끊김 → graceful 퇴장. 이후 Disconnect 가 RunAsync finally 를 트리거해도 LeaveRoom 은 멱등.
                        logger.LogWarning(
                            "Room player timed out — SessionId={SessionId} UserId={UserId} RoomId={RoomId} LastRecvAt={LastRecvAt}",
                            session.SessionId, session.UserId, session.Room.RoomId, session.LastRecvAt);

                        roomManager.LeaveRoom(session, graceful: true);
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
