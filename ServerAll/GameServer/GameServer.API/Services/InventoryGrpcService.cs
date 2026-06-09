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
    ILogger<InventoryGrpcService> logger) : InventoryGrpc.InventoryServiceBase
{
    /// <summary>
    /// 호출당 지급 수량 상한. Main 싱글 경로는 클라가 드랍을 결정하므로(클라 신뢰) 진입점에서 위조 폭을 제한한다.
    /// ※ 도메인 서비스(GrantItemAsync)에는 넣지 않는다 — 던전 서버 권위 경로(LootGrantConsumer)는 cap 무관.
    /// </summary>
    private const int MaxGrantPerCall = 99;

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

    public override async Task<GrantItemResponse> GrantItem(GrantItemRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("GrantItem rejected because user id was missing");
            return new GrantItemResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        }

        // 가드: 신뢰 못 하는 클라(싱글 Main) 진입점 → 호출당 수량 상한. (catalog 검증·amount≤0 은 GrantItemAsync 가 수행)
        if (request.Qty <= 0 || request.Qty > MaxGrantPerCall)
        {
            logger.LogWarning("GrantItem rejected: qty {Qty} out of range for user {UserId} item {ItemId}", request.Qty, userId, request.ItemId);
            return new GrantItemResponse
            {
                Result = Result.Failure(ErrorCodes.InvalidRequest, $"qty must be in 1..{MaxGrantPerCall}").ToGrpcResult(),
            };
        }

        var grant = await inventoryService.GrantItemAsync(userId.Value, request.ItemId, request.Qty, context.CancellationToken);
        if (!grant.Success)
        {
            logger.LogWarning("GrantItem failed for user {UserId} item {ItemId}: {Reason}", userId, request.ItemId, grant.FailReason);
            return new GrantItemResponse
            {
                Result = Result.Failure(ErrorCodes.InvalidRequest, grant.FailReason ?? "grant failed").ToGrpcResult(),
            };
        }

        logger.LogInformation("GrantItem succeeded: user {UserId} item {ItemId} +{Qty} -> {NewQuantity}", userId, request.ItemId, request.Qty, grant.NewQuantity);
        return new GrantItemResponse
        {
            Result = Result.Success().ToGrpcResult(),
            NewQuantity = grant.NewQuantity,
        };
    }
}
