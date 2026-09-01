using Game.Installers;
using Game.Presentation.Chat;
using Game.System.Input;
using NUnit.Framework;
using VContainer;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>
    /// 채팅 Model 의 **루트 DI 배선**. 생성자 인자를 컨테이너가 전부 해석할 수 있어야
    /// 첫 씬에서 루트 컨테이너가 죽지 않는다 — VContainer 는 C# 기본값을 채워주지 않으므로
    /// "선택 인자니까 없어도 되겠지"가 통하지 않는다.
    /// </summary>
    public class ChatDiWiringTests
    {
        /// <summary>ProjectLifetimeScope 가 채팅에 대해 제공하는 것과 같은 등록 조합.</summary>
        private static ContainerBuilder BuildRoot()
        {
            var builder = new ContainerBuilder();
            builder.Install(new NetworkInstaller()); // IChatGrpcService
            builder.Install(new AuthInstaller());    // AuthSession
            builder.Install(new DungeonLobbyInstaller()); // DungeonLobbySession(방 소속 판정)
            builder.Install(new ChatInstaller());

            // IInputContext 는 인스톨러가 아니라 ProjectLifetimeScope 가 직접 등록한다(입력은 전 씬 공유).
            // 실제 구현(InputContext)은 InputActionAsset 을 물고 있어 EditMode 에서 파괴할 수 없으므로
            // 여기서는 같은 계약의 스텁을 넣는다 — 이 테스트가 보는 것은 "인자가 해석되는가"다.
            builder.Register<IInputContext, StubInputContext>(Lifetime.Singleton);
            return builder;
        }

        [Test]
        public void ChatModel_은_루트_등록_조합으로_해석된다()
        {
            using var container = BuildRoot().Build();
            Assert.IsNotNull(container.Resolve<ChatModel>());
        }

        [Test]
        public void ChatModel_은_싱글턴이라_HUD가_다시_생겨도_같은_로그를_본다()
        {
            using var container = BuildRoot().Build();
            Assert.AreSame(container.Resolve<ChatModel>(), container.Resolve<ChatModel>());
        }

        private sealed class StubInputContext : IInputContext
        {
            public bool IsUiActive => false;
            public void EnterUi() { }
            public void ExitUi() { }
        }
    }
}
