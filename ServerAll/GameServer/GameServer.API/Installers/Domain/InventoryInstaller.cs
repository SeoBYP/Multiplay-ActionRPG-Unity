using GameServer.Application.Domains.Codex;
using GameServer.Application.Domains.Codex.Interfaces;
using GameServer.Application.Domains.Equipment;
using GameServer.Application.Domains.Equipment.Interfaces;
using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Application.Domains.Quest;
using GameServer.Application.Domains.Quest.Interfaces;
using GameServer.Application.Domains.Shop;
using GameServer.Application.Domains.Shop.Interfaces;
using GameServer.Application.Domains.Wallet;
using GameServer.Application.Domains.Wallet.Interfaces;
using GameServer.Infrastructure.Common.Consumer;
using GameServer.Infrastructure.Common.MessageQueue;
using GameServer.Infrastructure.Domains.Codex;
using GameServer.Infrastructure.Domains.Equipment;
using GameServer.Infrastructure.Domains.Inventory;
using GameServer.Infrastructure.Domains.Quest;
using GameServer.Infrastructure.Domains.Wallet;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.API.Installers.Domain;

public class InventoryInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryService, InventoryService>();

        // 도감: 발견은 GrantItemAsync funnel 에서 기록(서버 권위).
        services.AddScoped<ICodexRepository, CodexRepository>();
        services.AddScoped<ICodexService, CodexService>();

        // 퀘스트: 진행은 킬 클레임 경로(MainSpawnClaimService)에서 서버 권위 +1.
        services.AddScoped<IQuestRepository, QuestRepository>();
        services.AddScoped<IQuestService, QuestService>();

        // 장비: 소유는 IInventoryService 위임.
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IEquipmentService, EquipmentService>();

        // 재화/골드: 인벤토리와 분리된 통화, 증감은 서버 권위.
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IWalletService, WalletService>();

        // 상점: 정적 카탈로그(영속 없음), 가격은 서버만 안다.
        services.AddScoped<IShopService, ShopService>();

        // ClaimKill: 슬롯/쿨다운 검증 + 서버 roll + 지급.
        services.AddScoped<IMainSpawnClaimService, MainSpawnClaimService>();
        services.AddSingleton<IClaimCooldownStore, RedisClaimCooldownStore>();

        // SocketServer 줍기 확정 → 인벤토리 영속 지급.
        services.AddSingleton<IMessageQueue<ItemPickedUpMessage>, LootPickupMessageQueue>();
        services.AddHostedService<LootGrantConsumer>();

        // 소모품 소비 통지(GameServer→Socket). InventoryGrpcService.ConsumeItem 가 발행.
        services.AddSingleton<IMessageQueue<PlayerConsumedMessage>, PlayerConsumedMessageQueue>();
    }
}
