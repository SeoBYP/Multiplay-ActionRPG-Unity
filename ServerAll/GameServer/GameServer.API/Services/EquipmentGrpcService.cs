using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Common;
using GameServer.Application.Domains.Equipment.Interfaces;
using GameServer.Grpc.Equipment;
using Grpc.Core;
using DomainType = Shared.Gameplay.Equipment.EquipmentType;
using ProtoType = GameServer.Grpc.Equipment.EquipmentType;
using EquipmentGrpc = GameServer.Grpc.Equipment.EquipmentService;

namespace GameServer.API.Services;

/// <summary>
/// 장비 gRPC 진입점(3.2). 착용/해제/조회. userId 는 JWT 에서 추출(AuthInterceptor 가 인증 보장).
/// 스탯 합산은 서버 GetStatsAsync 가 수행 — 여기선 착용 상태만 다룬다.
/// </summary>
public class EquipmentGrpcService(
    IEquipmentService equipmentService,
    ILogger<EquipmentGrpcService> logger) : EquipmentGrpc.EquipmentServiceBase
{
    public override async Task<EquipResponse> Equip(EquipRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
            return new EquipResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };

        var result = await equipmentService.EquipAsync(userId.Value, request.ItemId, context.CancellationToken);
        if (!result.Success)
        {
            logger.LogWarning("Equip failed for user {UserId} item {ItemId}: {Reason}", userId, request.ItemId, result.FailReason);
            return new EquipResponse
            {
                Result = Result.Failure(ErrorCodes.InvalidRequest, result.FailReason ?? "equip failed").ToGrpcResult(),
            };
        }

        logger.LogInformation("Equip succeeded: user {UserId} {Slot}={ItemId}", userId, result.Slot, result.ItemId);
        return new EquipResponse
        {
            Result = Result.Success().ToGrpcResult(),
            Slot = ToProtoSlot(result.Slot),
            ItemId = result.ItemId,
        };
    }

    public override async Task<UnequipResponse> Unequip(UnequipRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
            return new UnequipResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };

        if (!TryToDomainSlot(request.Slot, out var slot))
        {
            return new UnequipResponse
            {
                Result = Result.Failure(ErrorCodes.InvalidRequest, "unknown slot").ToGrpcResult(),
            };
        }

        var result = await equipmentService.UnequipAsync(userId.Value, slot, context.CancellationToken);
        logger.LogInformation("Unequip succeeded: user {UserId} {Slot}", userId, slot);
        return new UnequipResponse
        {
            Result = Result.Success().ToGrpcResult(),
            Slot = ToProtoSlot(result.Slot),
        };
    }

    public override async Task<GetEquipmentResponse> GetEquipment(GetEquipmentRequest request, ServerCallContext context)
    {
        var userId = context.GetUserId();
        if (userId is null)
            return new GetEquipmentResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };

        var equipped = await equipmentService.GetEquippedAsync(userId.Value, context.CancellationToken);

        var response = new GetEquipmentResponse { Result = Result.Success().ToGrpcResult() };
        foreach (var e in equipped)
            response.Items.Add(new EquippedItem { Slot = ToProtoSlot(e.Slot), ItemId = e.ItemId });

        return response;
    }

    // 도메인 EquipmentType ↔ proto EquipmentType — 정수값 1:1(공통 enum) 이므로 캐스팅 매핑.
    private static ProtoType ToProtoSlot(DomainType slot) => (ProtoType)(int)slot;

    // Unspecified(0)·정의되지 않은 값은 거부(해제할 슬롯이 유효해야 함).
    private static bool TryToDomainSlot(ProtoType slot, out DomainType domain)
    {
        domain = (DomainType)(int)slot;
        return slot != ProtoType.Unspecified && Enum.IsDefined(typeof(DomainType), domain);
    }
}
