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

    /// <summary>
    /// Main(싱글) 획득 — B-lite 서버 검증. 클라는 (mapId, slotId)만 보고. 서버가 슬롯/쿨다운 검증 + 권위 roll + 지급.
    /// 보상 내용은 서버가 결정(클라가 itemId/qty 지정하던 무한파밍 핵 = 구 GrantItem 차단). main-spawn-claim.md.
    /// </summary>
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

        // 성공(쿨다운 중이면 granted 비어있음 = 보상 없음, 에러 아님). 경험치는 ClaimMonsterExp 가 별도.
        var response = new ClaimKillResponse { Result = Result.Success().ToGrpcResult() };
        foreach (var g in claim.Granted)
            response.Granted.Add(new GrantedItem { ItemId = g.ItemId, Qty = g.Qty, NewQuantity = g.NewQuantity });

        logger.LogInformation("ClaimKill succeeded: user {UserId} map {Map} slot {Slot} → {Count} item(s)", userId, request.MapId, request.SlotId, response.Granted.Count);
        return response;
    }

    /// <summary>
    /// Main 킬 경험치(킬 즉시) — 슬롯 검증 + exp 전용 쿨다운(아이템 줍기와 독립) → 몬스터 정의 ExpReward 적립.
    /// 줍기 여부와 무관하게 죽이면 즉시 exp. 쿨다운으로 exp 파밍도 상한.
    /// </summary>
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

        // 성공(쿨다운 중이면 exp 0). 클라는 ExpGained 로 "+N exp" 로그.
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

        // 서버 권위 = 보유/수량 검증·차감. 회복 효과 적용은 클라(GAS) — 서버는 차감만(plan 3.8).
        // 보유 검증·원자적 차감은 ConsumeItemAsync(저장소)가 수행 → 미보유/부족이면 실패.
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

        // 서버 권위 회복(던전): 검증·차감 성공을 SocketServer 에 통지 → 던전이면 서버 HP 에 회복 적용
        // + S_ApplyEffect 브로드캐스트. EffectId == itemId 규칙. 던전 밖(방 없음)이면 SocketServer 가 no-op
        // (Main 솔로 회복은 클라 로컬). authority-model §4. 발행 실패가 차감을 막지 않도록 try-catch.
        try
        {
            await playerConsumedQueue.EnqueueAsync(new Shared.Infrastructure.Messages.PlayerConsumedMessage
            {
                // 소비 1건 = 고유 id. SocketServer 가 재배달 시 이중 회복을 막는 데 쓴다(at-least-once).
                ConsumeId = Guid.NewGuid().ToString("N"),
                UserId = userId.Value,
                // effectId == itemId 규칙(ItemCatalogData). 소비 통지는 문자열 effectId 계약이라
                // numericId 를 문자열로 싣는다 — GAS 효과 id 체계는 별도(전환 대상 아님).
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
