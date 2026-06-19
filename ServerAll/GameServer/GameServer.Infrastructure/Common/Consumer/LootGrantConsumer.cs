using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Application.Domains.Wallet.Interfaces;
using GameServer.Domain;
using GameServer.Infrastructure.Domains;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Common.Consumer;

/// <summary>
/// SocketServer가 발행한 줍기 확정 이벤트를 소비해 인벤토리에 영속 지급한다(GrantItemAsync → Create/Update).
///
/// 경계(loot-drop.md §1.1): 월드(SocketServer)는 itemId 문자열만 알고, 정의 검증(ItemCatalog)·영속은 여기서.
/// 드리프트(없는 itemId)는 GrantItemAsync 가 "unknown item" 으로 실패 처리 → 안전.
///
/// 멱등(at-most-once): GrantItemAsync(AddQuantity)는 += 라 비멱등이므로 PickupId 를 claim-first 로 잠가
/// 중복 메시지(Consumer Group 재전달)에도 1회만 지급한다(DungeonResultConsumer 와 동일 패턴).
/// </summary>
public sealed class LootGrantConsumer(
    IMessageQueue<ItemPickedUpMessage> lootPickupQueue,
    IConnectionMultiplexer connectionMultiplexer,
    IServiceScopeFactory scopeFactory,
    ILogger<LootGrantConsumer> logger) : BackgroundService
{
    private readonly IDatabase _redis = connectionMultiplexer.GetDatabase();
    private static readonly TimeSpan ProcessedTtl = TimeSpan.FromHours(24);

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

        // 멱등: PickupId 를 처리완료 집합에 원자적으로 claim. 이미 있으면(재전달) 스킵.
        bool claimed = await _redis.SetAddAsync(RedisKeys.LootPickupProcessed(), message.PickupId);
        if (!claimed)
        {
            logger.LogInformation("[LootGrant] PickupId={PickupId} 이미 처리됨 — 스킵", message.PickupId);
            return;
        }
        await _redis.KeyExpireAsync(RedisKeys.LootPickupProcessed(), ProcessedTtl);

        using var scope = scopeFactory.CreateScope();

        // 골드는 통화 — 인벤토리가 아니라 지갑 잔액으로 적립(3.4). 그 외는 인벤토리 영속 지급.
        if (Currencies.IsCurrency(message.ItemId))
        {
            var walletService = scope.ServiceProvider.GetRequiredService<IWalletService>();
            var balance = await walletService.AddAsync(message.UserId, message.Qty, ct);
            logger.LogInformation(
                "[LootGrant] 골드 적립 UserId={UserId} +{Qty} → 잔액 {Balance} (PickupId={PickupId})",
                message.UserId, message.Qty, balance, message.PickupId);
            return;
        }

        var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

        var result = await inventoryService.GrantItemAsync(message.UserId, message.ItemId, message.Qty, ct);
        if (result.Success)
        {
            logger.LogInformation(
                "[LootGrant] 지급 UserId={UserId} ItemId={ItemId} +{Qty} → 보유 {NewQty} (PickupId={PickupId})",
                message.UserId, message.ItemId, message.Qty, result.NewQuantity, message.PickupId);
        }
        else
        {
            logger.LogWarning(
                "[LootGrant] 지급 실패 UserId={UserId} ItemId={ItemId} 사유={Reason} (PickupId={PickupId})",
                message.UserId, message.ItemId, result.FailReason, message.PickupId);
        }
    }
}
