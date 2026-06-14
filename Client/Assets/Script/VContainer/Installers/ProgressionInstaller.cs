using Game.System.Progression;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 진행(레벨/스탯) 서비스 등록(루트 스코프). 네트워크 gRPC 래퍼(IProgressionGrpcService)는 GameApiClient가 등록.
    /// Presentation ProgressionModel 은 스탯창을 띄우는 씬 스코프(Main/Dungeon)에서 등록.
    /// </summary>
    public sealed class ProgressionInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<IProgressionService, ProgressionService>(Lifetime.Singleton);
        }
    }
}
