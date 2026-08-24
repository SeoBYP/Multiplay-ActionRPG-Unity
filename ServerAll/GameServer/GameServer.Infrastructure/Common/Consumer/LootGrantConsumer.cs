using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Application.Domains.Reward.Interfaces;
using GameServer.Application.Domains.Wallet.Interfaces;
using GameServer.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.Infrastructure.Common.Consumer;

/// <summary>
/// SocketServer가 발행한 줍기 확정 이벤트를 소비해 인벤토리에 영속 지급한다(GrantItemAsync → Create/Update).
///
/// 경계(loot-drop.md §1.1): 월드(SocketServer)는 itemId 문자열만 알고, 정의 검증(ItemCatalog)·영속은 여기서.
///
/// 멱등(exactly-once): `GrantItemAsync`(AddQuantity)·지갑 적립은 += 라 비멱등이므로
/// **지급과 같은 트랜잭션**에 원장(<see cref="IRewardLedger"/>, GrantKey = "pickup:{pickupId}")을 남긴다.
/// 예전 Redis claim-first 는 지급 도중 실패하면 claim 만 남아 영구 미지급이 됐다(DungeonResultConsumer 와 동일 문제).
/// </summary>
public sealed class LootGrantConsumer(
    IMessageQueue<ItemPickedUpMessage> lootPickupQueue,
    IServiceScopeFactory scopeFactory,
    ILogger<LootGrantConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => ResilientStreamConsumer.RunAsync<ItemPickedUpMessage>(
            nameof(LootGrantConsumer),
            lootPickupQueue.DequeueAllAsync,
            ProcessAsync,
            logger,
            stoppingToken);

    private async Task ProcessAsync(ItemPickedUpMessage message, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(message.PickupId))
        {
            logger.LogWarning("[LootGrant] PickupId 누락 — 스킵 (UserId={UserId} ItemId={ItemId})",
                message.UserId, message.ItemId);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IRewardLedger>();
        var grantKey = $"pickup:{message.PickupId}";

        // 골드는 통화 — 인벤토리가 아니라 지갑 잔액으로 적립(3.4). 그 외는 인벤토리 영속 지급.
        if (Currencies.IsCurrency(message.ItemId))
        {
            var walletService = scope.ServiceProvider.GetRequiredService<IWalletService>();
            bool credited = await ledger.GrantOnceAsync(
                new RewardGrantRequest(grantKey, message.UserId, "currency", message.ItemId.ToString(), message.Qty),
                token => walletService.AddAsync(message.UserId, message.Qty, token),
                ct);

            logger.LogInformation(
                credited
                    ? "[LootGrant] 골드 적립 UserId={UserId} +{Qty} (PickupId={PickupId})"
                    : "[LootGrant] 골드 이미 적립됨 — 스킵 UserId={UserId} +{Qty} (PickupId={PickupId})",
                message.UserId, message.Qty, message.PickupId);
            return;
        }

        var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

        bool granted = await ledger.GrantOnceAsync(
            new RewardGrantRequest(grantKey, message.UserId, "item", message.ItemId.ToString(), message.Qty),
            async token =>
            {
                var result = await inventoryService.GrantItemAsync(message.UserId, message.ItemId, message.Qty, token);
                if (!result.Success)
                {
                    // 던져야 원장도 함께 롤백된다 — 지급되지 않은 것을 "지급했음" 으로 남기지 않는다.
                    // 카탈로그 드리프트(없는 itemId) 처럼 영구 실패면 재시도 상한에서 드롭된다.
                    throw new InvalidOperationException(
                        $"GrantItem failed for user {message.UserId} item {message.ItemId}: {result.FailReason}");
                }
            },
            ct);

        logger.LogInformation(
            granted
                ? "[LootGrant] 지급 UserId={UserId} ItemId={ItemId} +{Qty} (PickupId={PickupId})"
                : "[LootGrant] 이미 지급됨 — 스킵 UserId={UserId} ItemId={ItemId} +{Qty} (PickupId={PickupId})",
            message.UserId, message.ItemId, message.Qty, message.PickupId);
    }
}
