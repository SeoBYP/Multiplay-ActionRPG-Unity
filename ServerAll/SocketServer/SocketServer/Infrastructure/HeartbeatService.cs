using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Server.Infrastructure;

public class HeartBeatService(
    SessionManager sessionManager,
    ILogger<HeartBeatService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan TimeoutLimit = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("HeartBeatService started. CheckInterval={CheckInterval}, TimeoutLimit={TimeoutLimit}", CheckInterval, TimeoutLimit);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);

                var sessions = sessionManager.GetSessionsSnapshot();
                var now = DateTime.UtcNow;

                foreach (var session in sessions)
                {
                    if (now - session.LastRecvAt > TimeoutLimit)
                    {
                        logger.LogWarning("Session {SessionId} (UserId: {UserId}) timed out. LastRecvAt: {LastRecvAt}", 
                            session.SessionId, session.UserId, session.LastRecvAt);
                        
                        session.Disconnect();
                    }
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
