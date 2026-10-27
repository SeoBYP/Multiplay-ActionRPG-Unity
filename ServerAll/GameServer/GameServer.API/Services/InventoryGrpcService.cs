using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Common;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Grpc.Inventory;
using Grpc.Core;
using InventoryGrpc = GameServer.Grpc.Inventory.InventoryService;

namespace GameServer.API.Services;

public class InventoryGrpcService(
    IInventoryService inventoryService,
    IMainSpawnClaimService mainSpawnClaimService,
    Shared.Infrastructure.MessageQueue.IMessageQueue<Shared.Infrastructure.Messages.PlayerConsumedMessage> playerConsumedQueue,
    ILogger<InventoryGrpcService> logger) : InventoryGrpc.InventoryServiceBase
{
    public override async Task<GetInventoryResponse> GetInventory(GetInventoryRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("GetInventory rejected because user id was missing");
            return new GetInventoryResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        }

        var items = await inventoryService.GetInventoryAsync(userId.Value, context.CancellationToken);

        var response = new GetInventoryResponse { Result = Result.Success().ToGrpcResult() };
        foreach (var item in items)
            response.Items.Add(new InventoryItemInfo { ItemId = item.ItemId, Quantity = item.Quantity });

        logger.LogInformation("GetInventory succeeded for user {UserId} with {Count} items", userId, response.Items.Count);
        return response;
    }

    /// <summary>클라는 (mapId, slotId)만 보고하고 보상은 서버가 roll — 클라가 itemId/qty 를 지정하면 무한 파밍이 된다.</summary>
    public override async Task<ClaimKillResponse> ClaimKill(ClaimKillRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("ClaimKill rejected because user id was missing");
            return new ClaimKillResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        }

        var claim = await mainSpawnClaimService.ClaimKillAsync(userId.Value, request.MapId, request.SlotId, context.CancellationToken);
        if (!claim.Success)
        {
            // 위조 슬롯/맵 — 정상 플레이에선 안 나옴(치팅/버그 신호).
            logger.LogWarning("ClaimKill rejected for user {UserId} map {Map} slot {Slot}: {Reason}", userId, request.MapId, request.SlotId, claim.FailReason);
            return new ClaimKillResponse
            {
                Result = Result.Failure(ErrorCodes.InvalidRequest, claim.FailReason ?? "claim failed").ToGrpcResult(),
            };
        }

        // 쿨다운 중이면 granted 가 비지만 에러는 아니다. 경험치는 ClaimMonsterExp 가 별도.
        var response = new ClaimKillResponse { Result = Result.Success().ToGrpcResult() };
        foreach (var g in claim.Granted)
            response.Granted.Add(new GrantedItem { ItemId = g.ItemId, Qty = g.Qty, NewQuantity = g.NewQuantity });

        logger.LogInformation("ClaimKill succeeded: user {UserId} map {Map} slot {Slot} → {Count} item(s)", userId, request.MapId, request.SlotId, response.Granted.Count);
        return response;
    }

    /// <summary>킬 즉시 exp 적립. exp 전용 쿨다운(아이템 줍기와 독립)으로 파밍 상한을 둔다.</summary>
    public override async Task<ClaimMonsterExpResponse> ClaimMonsterExp(ClaimMonsterExpRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("ClaimMonsterExp rejected because user id was missing");
            return new ClaimMonsterExpResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        }

        var claim = await mainSpawnClaimService.ClaimExpAsync(userId.Value, request.MapId, request.SlotId, context.CancellationToken);
        if (!claim.Success)
        {
            logger.LogWarning("ClaimMonsterExp rejected for user {UserId} map {Map} slot {Slot}: {Reason}", userId, request.MapId, request.SlotId, claim.FailReason);
            return new ClaimMonsterExpResponse
            {
                Result = Result.Failure(ErrorCodes.InvalidRequest, claim.FailReason ?? "claim failed").ToGrpcResult(),
            };
        }

        // 쿨다운 중이면 exp 0.
        logger.LogInformation("ClaimMonsterExp succeeded: user {UserId} map {Map} slot {Slot} → +{Exp} exp", userId, request.MapId, request.SlotId, claim.ExpGained);
        return new ClaimMonsterExpResponse
        {
            Result = Result.Success().ToGrpcResult(),
            ExpGained = claim.ExpGained,
        };
    }

    public override async Task<ConsumeItemResponse> ConsumeItem(ConsumeItemRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("ConsumeItem rejected because user id was missing");
            return new ConsumeItemResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        }

        // 서버는 보유/수량 검증과 원자적 차감만 한다(미보유/부족이면 실패). 회복 효과는 클라(GAS)가 적용.
        var consume = await inventoryService.ConsumeItemAsync(userId.Value, request.ItemId, request.Qty, context.CancellationToken);
        if (!consume.Success)
        {
            logger.LogWarning("ConsumeItem failed for user {UserId} item {ItemId}: {Reason}", userId, request.ItemId, consume.FailReason);
            return new ConsumeItemResponse
            {
                Result = Result.Failure(ErrorCodes.InvalidRequest, consume.FailReason ?? "consume failed").ToGrpcResult(),
            };
        }

        logger.LogInformation("ConsumeItem succeeded: user {UserId} item {ItemId} -{Qty} -> {Remaining}", userId, request.ItemId, request.Qty, consume.RemainingQuantity);

        // 차감 성공을 SocketServer 에 통지 → 던전이면 서버 HP 회복 + S_ApplyEffect 브로드캐스트, 방이 없으면 no-op.
        // 발행 실패가 이미 끝난 차감을 되돌리지 않도록 try-catch.
        try
        {
            await playerConsumedQueue.EnqueueAsync(new Shared.Infrastructure.Messages.PlayerConsumedMessage
            {
                // 소비 1건 = 고유 id. at-least-once 재배달 시 SocketServer 가 이중 회복을 막는 데 쓴다.
                ConsumeId = Guid.NewGuid().ToString("N"),
                UserId = userId.Value,
                // effectId == itemId 규칙. 통지 계약이 문자열 effectId 라 문자열로 싣는다.
                EffectId = request.ItemId.ToString(),
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ConsumeItem: failed to publish PlayerConsumed for user {UserId} item {ItemId}", userId, request.ItemId);
        }

        return new ConsumeItemResponse
        {
            Result = Result.Success().ToGrpcResult(),
            RemainingQuantity = consume.RemainingQuantity,
        };
    }
}
