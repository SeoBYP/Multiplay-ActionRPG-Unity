using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameServer.Infrastructure.Common;

/// <summary>
/// 유령 방(아무도 돌아오지 않는 방)을 주기적으로 정리한다.
/// 판정과 정리는 <see cref="IDungeonLobbyService.ReapRoomIfAbandonedAsync"/> 가 하고,
/// 여기는 "언제 도는가"와 "어떤 스코프에서 도는가"만 책임진다.
/// </summary>
public sealed class DungeonRoomReaper(
    IServiceScopeFactory scopeFactory,
    IOptions<DungeonRoomReaperOptions> options,
    ILogger<DungeonRoomReaper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 서버는 멈추면 안 된다 — 어떤 예외도 이 루프 밖으로 내보내지 않는다.
        try
        {
            using var timer = new PeriodicTimer(options.Value.Interval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var reaped = await RunPassAsync(stoppingToken);
                    if (reaped > 0)
                        logger.LogInformation("[Reaper] Reaped {Count} abandoned room(s)", reaped);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    // 한 번의 실패로 리퍼가 죽지 않는다. 다음 주기에 다시 시도한다.
                    logger.LogError(e, "[Reaper] Reap pass threw");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 정상 종료.
        }
        catch (Exception e)
        {
            logger.LogError(e, "[Reaper] Loop terminated unexpectedly");
        }
    }

    private async Task<int> RunPassAsync(CancellationToken ct)
    {
        List<long> roomIds;
        using (var listScope = scopeFactory.CreateScope())
        {
            var repository = listScope.ServiceProvider.GetRequiredService<IDungeonRoomRepository>();
            roomIds = (await repository.GetAllActiveRoomsAsync(ct)).Select(room => room.RoomId).ToList();
        }

        var reaped = 0;
        foreach (var roomId in roomIds)
        {
            if (ct.IsCancellationRequested) break;

            // 방마다 새 스코프 = 새 DbContext. 한 방의 저장 실패가 뒤따르는 방을 오염시키지 않는다.
            using var scope = scopeFactory.CreateScope();
            var lobbyService = scope.ServiceProvider.GetRequiredService<IDungeonLobbyService>();

            var result = await lobbyService.ReapRoomIfAbandonedAsync(roomId, ct);
            if (result.IsSuccess && result.Value)
                reaped++;
            else if (!result.IsSuccess)
                logger.LogWarning("[Reaper] Room {RoomId} reap failed: {Message}", roomId, result.Message);
        }

        return reaped;
    }
}
