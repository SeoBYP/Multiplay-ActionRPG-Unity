using Game.System.Wallet;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 재화(골드) 서비스 등록(루트 스코프). 네트워크 gRPC 래퍼(IWalletGrpcService)는 GameApiClient가 등록.
    /// </summary>
    public sealed class WalletInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<IWalletService, WalletService>(Lifetime.Singleton);
        }
    }
}
