using Game.System.Shop;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 상점 서비스 등록(루트 스코프). 네트워크 gRPC 래퍼(IShopGrpcService)는 GameApiClient가 등록.
    /// </summary>
    public sealed class ShopInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<IShopService, ShopService>(Lifetime.Singleton);
        }
    }
}
