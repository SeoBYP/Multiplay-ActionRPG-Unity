using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Application.Domains.Reward.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;

namespace GameServer.Infrastructure.Common.Consumer;

/// <summary>
/// SocketServer가 발행한 던전 클리어 이벤트를 소비해 참가자 전원에게 Exp 만 지급한다(인벤토리 제외).
/// 보상 값은 Shared 카탈로그(spawn-layouts.json 의 expReward)를 MapId 로 조회 — SocketServer 표시값과 같은 소스.
///
/// 멱등은 참가자 단위 원장(<see cref="IRewardLedger"/>, GrantKey = "dungeon:{roomId}:{userId}")이 담당한다.
/// 원장 INSERT 가 지급과 같은 트랜잭션이라, 재배달 시 이미 받은 사람은 건너뛰고 못 받은 사람만 마저 받는다.
/// </summary>
public sealed class DungeonResultConsumer(
    IMessageQueue<DungeonClearMessage> dungeonClearQueue,
    IServiceScopeFactory scopeFactory,
    ILogger<DungeonResultConsumer> logger) : BackgroundService
{
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

        foreach (var userId in message.Participants)
        {
            // 지급 1건 = 스코프 1개. 실패한 DbContext 의 변경 추적기가 다음 참가자 지급에 새어들면 안 된다.
            using var scope = scopeFactory.CreateScope();
            var ledger = scope.ServiceProvider.GetRequiredService<IRewardLedger>();
            var progressionService = scope.ServiceProvider.GetRequiredService<IProgressionService>();

            bool granted = await ledger.GrantOnceAsync(
                new RewardGrantRequest($"dungeon:{message.RoomId}:{userId}", userId, "exp", "", expReward),
                token => progressionService.AddExpAsync(userId, expReward, token),
                ct);

            if (granted)
                logger.LogInformation("[DungeonResult] Exp 지급 UserId={UserId} +{Exp} (RoomId={RoomId})",
                    userId, expReward, message.RoomId);
            else
                logger.LogInformation("[DungeonResult] UserId={UserId} 이미 지급됨 — 스킵 (RoomId={RoomId})",
                    userId, message.RoomId);
        }

        logger.LogInformation("[DungeonResult] 완료 RoomId={RoomId} MapId={MapId} 참가자={Count} 각 +{Exp}",
            message.RoomId, message.MapId, message.Participants.Length, expReward);
    }
}
