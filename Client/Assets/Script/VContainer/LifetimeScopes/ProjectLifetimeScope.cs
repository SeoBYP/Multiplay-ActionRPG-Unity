using Game.Gameplay.Input;
using Game.Presentation.GameScene;
using Game.System.GameScene;
using Game.System.Input;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 전역(씬 공통) DI 스코프.
    ///
    /// 각 도메인의 등록 책임은 전용 Installer에 위임한다.
    /// 이 파일은 Installer 조합만 담당한다.
    ///
    /// ┌─────────────────────────────────────┐
    /// │  NetworkInstaller     — gRPC + TCP  │
    /// │  AuthInstaller        — 인증 서비스  │
    /// │  DungeonLobbyInstaller — 로비 서비스 │
    /// │  GameSessionInstaller — 세션 서비스  │
    /// └─────────────────────────────────────┘
    /// </summary>
    public class ProjectLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder) => InstallRoot(builder);

        /// <summary>
        /// 루트 컨테이너의 **전체** 등록. `Configure` 와 DI 배선 테스트가 이 하나를 공유한다.
        ///
        /// 왜 static 으로 뽑았나: 테스트가 이 목록을 손으로 베끼면 여기에 등록이 추가될 때마다
        /// 사본이 조용히 어긋난다(실제로 `IGameSceneManager` 누락으로 그렇게 됐다).
        /// 미러링 대신 같은 함수를 부르게 해서 드리프트를 구조적으로 없앤다.
        ///
        /// 여기 있는 것은 전부 POCO 라 씬 없이도 해석된다 — MonoBehaviour/씬 의존 등록은 자식 스코프 몫이다.
        /// </summary>
        public static void InstallRoot(IContainerBuilder builder)
        {
            builder.Install(new NetworkInstaller());
            builder.Install(new AuthInstaller());
            builder.Install(new DungeonLobbyInstaller());
            builder.Install(new GameSessionInstaller());
            builder.Install(new InventoryInstaller());
            builder.Install(new EquipmentInstaller());
            builder.Install(new ProgressionInstaller());
            builder.Install(new WalletInstaller());
            builder.Install(new ShopInstaller());
            builder.Install(new QuestInstaller());
            builder.Install(new ChatInstaller());

            builder.Register<IGameSceneManager, GameSceneManager>(Lifetime.Singleton);

            // 입력은 전역 단일 인스턴스 — 모든 씬(Main/Dungeon)이 같은 PlayerInputActions를 공유한다.
            // Singleton + 자식 스코프 재등록 금지 → InjectGameObject가 이 루트 인스턴스를 주입한다.
            builder.Register<PlayerInputActions>(Lifetime.Singleton);
            // UI 점유 시 Player 맵 토글 — 전 씬 공통. 같은 PlayerInputActions를 끄고 켜야 하므로 루트에 둔다.
            builder.Register<IInputContext, InputContext>(Lifetime.Singleton);
            // 진입 시 입력 맵 활성화 1회(전역).
            builder.RegisterEntryPoint<GlobalInputInitializer>(Lifetime.Singleton);
        }
    }
}
