using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Infrastructure.Domains;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Common.Consumer;

/// <summary>
/// SocketServer가 발행한 던전 클리어 이벤트를 소비해 참가자 전원에게 Exp 보상을 지급한다.
///
/// 던전별 보상은 Shared 카탈로그(spawn-layouts.json 의 expReward)에서 MapId 로 조회한다 —
/// SocketServer(S_DungeonClear 표시)와 동일 소스라 표시·지급 값이 일치한다.
/// 보상은 Exp 만(인벤토리 제외, 2026-06-06 범위 확정).
/// </summary>
public sealed class DungeonResultConsumer(
    IMessageQueue<DungeonClearMessage> dungeonClearQueue,
    IConnectionMultiplexer connectionMultiplexer,
    IServiceScopeFactory scopeFactory,
    ILogger<DungeonResultConsumer> logger) : BackgroundService
{
    private readonly IDatabase _redis = connectionMultiplexer.GetDatabase();
    private static readonly TimeSpan ProcessedTtl = TimeSpan.FromHours(24);

    // 복원력(일시적 Redis 오류에 죽지 않음)은 ResilientStreamConsumer 공통 루프가 담당.
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => ResilientStreamConsumer.RunAsync<DungeonClearMessage>(
            nameof(DungeonResultConsumer),
            dungeonClearQueue.DequeueAllAsync,
            ProcessAsync,
            logger,
            stoppingToken);

    private async Task ProcessAsync(DungeonClearMessage message, CancellationToken ct)
    {
        // 멱등: RoomId 를 처리완료 집합에 원자적으로 claim. 이미 있으면(Consumer Group 재전달) 스킵.
        // AddExp 는 멱등이 아니므로 claim-first(at-most-once)로 이중 지급을 차단한다.
        bool claimed = await _redis.SetAddAsync(RedisKeys.DungeonResultProcessed(), message.RoomId);
        if (!claimed)
        {
            logger.LogInformation("[DungeonResult] RoomId={RoomId} 이미 처리됨 — 스킵", message.RoomId);
            return;
        }
        await _redis.KeyExpireAsync(RedisKeys.DungeonResultProcessed(), ProcessedTtl);

        long expReward;
        try
        {
            expReward = SpawnLayoutTable.Get(message.MapId).ExpReward;
        }
        catch (KeyNotFoundException)
        {
            logger.LogWarning("[DungeonResult] 알 수 없는 MapId={MapId} — 보상 스킵 (RoomId={RoomId})",
                message.MapId, message.RoomId);
            return;
        }

        if (expReward <= 0 || message.Participants.Length == 0)
        {
            logger.LogInformation("[DungeonResult] RoomId={RoomId} MapId={MapId} 보상 없음(exp={Exp}, 참가자={Count})",
                message.RoomId, message.MapId, expReward, message.Participants.Length);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var progressionService = scope.ServiceProvider.GetRequiredService<IProgressionService>();

        foreach (var userId in message.Participants)
        {
            var total = await progressionService.AddExpAsync(userId, expReward, ct);
            logger.LogInformation("[DungeonResult] Exp 지급 UserId={UserId} +{Exp} → 누적 {Total} (RoomId={RoomId})",
                userId, expReward, total, message.RoomId);
        }

        logger.LogInformation("[DungeonResult] 완료 RoomId={RoomId} MapId={MapId} 참가자={Count} 각 +{Exp}",
            message.RoomId, message.MapId, message.Participants.Length, expReward);
    }
}
