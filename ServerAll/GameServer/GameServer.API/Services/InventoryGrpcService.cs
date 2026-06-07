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
}
