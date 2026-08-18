using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Common;
using GameServer.Application.Domains.Shop.Interfaces;
using Shared.Infrastructure.Items;
using GameServer.Grpc.Shop;
using Grpc.Core;
using ShopGrpc = GameServer.Grpc.Shop.ShopService;
using DomainShopCategory = Shared.Gameplay.Items.ShopCategory;
using ProtoShopCategory = GameServer.Grpc.Shop.ShopCategory;

namespace GameServer.API.Services;

/// <summary>
/// 상점 gRPC. 진열 조회 + 구매/판매. 가격·검증은 서버 권위(IShopService). userId 는 JWT 추출.
/// 스탯 미리보기는 EquipmentCatalog 에서 파생해 채운다(공개 표시 정보). 3.5 Shop.
/// </summary>
public class ShopGrpcService(
    IShopService shopService,
    ILogger<ShopGrpcService> logger) : ShopGrpc.ShopServiceBase
{
    public override Task<GetShopResponse> GetShop(GetShopRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
            return Task.FromResult(new GetShopResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() });

        var response = new GetShopResponse { Result = Result.Success().ToGrpcResult() };
        foreach (var def in shopService.GetItems())
        {
            var info = new ShopItemInfo
            {
                ItemId = def.NumericId,
                BuyPrice = def.BuyPrice,
                SellPrice = def.SellPrice,
                Category = ToProtoCategory(def.Category),
            };
            AppendStatPreview(info, def.NumericId);
            response.Items.Add(info);
        }

        return Task.FromResult(response);
    }

    public override async Task<BuyResponse> Buy(BuyRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
            return new BuyResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };

        var result = await shopService.BuyAsync(userId.Value, request.ItemId, request.Qty, context.CancellationToken);
        if (!result.Success)
        {
            logger.LogInformation("Buy rejected for user {User} item {Item} x{Qty}: {Reason}", userId, request.ItemId, request.Qty, result.FailReason);
            return new BuyResponse { Result = Result.Failure(ErrorCodes.InvalidRequest, result.FailReason ?? "buy failed").ToGrpcResult() };
        }

        logger.LogInformation("Buy succeeded for user {User} item {Item} x{Qty} → gold {Gold}", userId, request.ItemId, request.Qty, result.Gold);
        return new BuyResponse
        {
            Result = Result.Success().ToGrpcResult(),
            Gold = result.Gold,
            NewQuantity = result.NewQuantity,
        };
    }

    public override async Task<SellResponse> Sell(SellRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
            return new SellResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };

        var result = await shopService.SellAsync(userId.Value, request.ItemId, request.Qty, context.CancellationToken);
        if (!result.Success)
        {
            logger.LogInformation("Sell rejected for user {User} item {Item} x{Qty}: {Reason}", userId, request.ItemId, request.Qty, result.FailReason);
            return new SellResponse { Result = Result.Failure(ErrorCodes.InvalidRequest, result.FailReason ?? "sell failed").ToGrpcResult() };
        }

        logger.LogInformation("Sell succeeded for user {User} item {Item} x{Qty} → gold {Gold}", userId, request.ItemId, request.Qty, result.Gold);
        return new SellResponse
        {
            Result = Result.Success().ToGrpcResult(),
            Gold = result.Gold,
            RemainingQuantity = result.RemainingQuantity,
        };
    }

    private static ProtoShopCategory ToProtoCategory(DomainShopCategory category) => category switch
    {
        DomainShopCategory.Weapon => ProtoShopCategory.Weapon,
        DomainShopCategory.Armor => ProtoShopCategory.Armor,
        DomainShopCategory.Accessory => ProtoShopCategory.Accessory,
        DomainShopCategory.Potion => ProtoShopCategory.Potion,
        _ => ProtoShopCategory.Unspecified,
    };

    /// <summary>장비면 EquipmentCatalog 의 가산 스탯(비0)을 미리보기로 채운다. 소모품이면 빈 목록.</summary>
    private static void AppendStatPreview(ShopItemInfo info, int itemId)
    {
        var def = EquipmentCatalog.Get(itemId);
        if (def is null)
            return;

        var s = def.Stats;
        AddStat(info, "AttackPower", s.AttackPower);
        AddStat(info, "Defense", s.Defense);
        AddStat(info, "MaxHealth", s.MaxHealth);
        AddStat(info, "MaxMana", s.MaxMana);
        AddStat(info, "Strength", s.Strength);
        AddStat(info, "Dexterity", s.Dexterity);
        AddStat(info, "Intelligence", s.Intelligence);
    }

    private static void AddStat(ShopItemInfo info, string stat, int amount)
    {
        if (amount != 0)
            info.Stats.Add(new StatPreview { Stat = stat, Amount = amount });
    }
}
