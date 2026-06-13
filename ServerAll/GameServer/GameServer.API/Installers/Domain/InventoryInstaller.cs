using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Infrastructure.Common.Consumer;
using GameServer.Infrastructure.Common.MessageQueue;
using GameServer.Infrastructure.Domains.Inventory;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.API.Installers.Domain;

/// <summary>
/// 인벤토리 도메인 DI. 향후 장비·상점·루트로 확장될 도메인이라 별도 Installer 로 응집.
/// </summary>
public class InventoryInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryService, InventoryService>();

        // Main 획득 서버 검증(B-lite): ClaimKill — 슬롯/쿨다운 검증 + 서버 roll + 지급. main-spawn-claim.md.
        services.AddScoped<IMainSpawnClaimService, MainSpawnClaimService>();
        services.AddSingleton<IClaimCooldownStore, RedisClaimCooldownStore>();

        // 루트/드랍(3.3): SocketServer 줍기 확정 → 인벤토리 영속 지급.
        services.AddSingleton<IMessageQueue<ItemPickedUpMessage>, LootPickupMessageQueue>();
        services.AddHostedService<LootGrantConsumer>();

        // 소모품 소비 통지(GameServer→Socket, 서버 권위 회복). InventoryGrpcService.ConsumeItem 가 발행.
        services.AddSingleton<IMessageQueue<PlayerConsumedMessage>, PlayerConsumedMessageQueue>();
    }
}
