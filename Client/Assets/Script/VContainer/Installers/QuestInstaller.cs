using Game.System.Quest;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 퀘스트 서비스 등록(루트 스코프). 네트워크 gRPC 래퍼(IQuestGrpcService)는 GameApiClient가 등록.
    /// </summary>
    public sealed class QuestInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<IQuestService, QuestService>(Lifetime.Singleton);
        }
    }
}
