using Game.System.Equipment;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 장비 서비스 등록(루트 스코프). 네트워크 gRPC 래퍼(IEquipmentGrpcService)는 GameApiClient가 등록.
    /// </summary>
    public sealed class EquipmentInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<IEquipmentService, EquipmentService>(Lifetime.Singleton);
        }
    }
}
