using Game.Presentation.Chat;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 채팅 Model 등록(루트 스코프).
    ///
    /// 씬 스코프가 아닌 이유: 채팅 스트림은 로그인~종료가 수명이다. 씬에 묶으면
    /// Main↔Dungeon 왕복마다 재연결되고 그 사이 메시지를 놓치며 로그도 사라진다.
    /// gRPC 래퍼(IChatGrpcService)는 GameApiClient 가 등록한다.
    /// </summary>
    public sealed class ChatInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            // AsSelf = HUD(ChatView)가 주입받는 타입 / AsImplementedInterfaces = IAsyncStartable·IDisposable 진입점.
            builder.Register<ChatModel>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();
        }
    }
}
