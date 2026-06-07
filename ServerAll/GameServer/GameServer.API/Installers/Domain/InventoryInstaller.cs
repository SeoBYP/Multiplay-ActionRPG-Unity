using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Infrastructure.Domains.Inventory;

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
    }
}
