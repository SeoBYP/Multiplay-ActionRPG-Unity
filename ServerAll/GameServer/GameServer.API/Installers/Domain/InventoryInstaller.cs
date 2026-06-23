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

/// <summary>
/// 인벤토리 도메인 DI. 향후 장비·상점·루트로 확장될 도메인이라 별도 Installer 로 응집.
/// </summary>
public class InventoryInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryService, InventoryService>();

        // 도감(3.7): 발견 기록 영속(DB-only) + 조회. 발견은 GrantItemAsync funnel 에서 기록(서버 권위).
        services.AddScoped<ICodexRepository, CodexRepository>();
        services.AddScoped<ICodexService, CodexService>();

        // 퀘스트(4.4): 수주/진행/보상(DB-only). 진행은 킬 클레임 경로(MainSpawnClaimService)에서 서버 권위 +1.
        services.AddScoped<IQuestRepository, QuestRepository>();
        services.AddScoped<IQuestService, QuestService>();

        // 장비(3.2): 착용 상태 영속(Cache-Aside) + 장착/해제/스탯합산. 소유는 IInventoryService 위임.
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IEquipmentService, EquipmentService>();

        // 재화/골드(3.4): 잔액 영속(Cache-Aside) + 증감(서버 권위). 골드=통화(인벤토리와 분리). 상점(3.5) 전제.
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IWalletService, WalletService>();

        // 상점(3.5): 정적 카탈로그(영속 없음) + 구매/판매(지갑·인벤 조합, 서버 권위). 가격은 서버만.
        services.AddScoped<IShopService, ShopService>();

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
