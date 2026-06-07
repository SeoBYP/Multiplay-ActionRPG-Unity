using Game.System.Inventory;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 인벤토리 서비스 등록(루트 스코프). 네트워크 gRPC 래퍼(IInventoryGrpcService)는 GameApiClient가 등록.
    /// </summary>
    public sealed class InventoryInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<IInventoryService, InventoryService>(Lifetime.Singleton);
        }
    }
}
