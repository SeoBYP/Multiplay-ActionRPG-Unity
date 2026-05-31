using Game.GUI.OutGame;
using Game.Presentation.InGame;
using VContainer;
using VContainer.Unity;

public class DungeonLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // InGame MVI Model 등록
        builder.Register<InGameModel>(Lifetime.Scoped)
            .AsImplementedInterfaces()
            .AsSelf();

        // GameHud는 씬에 미리 배치하지 않고 Addressable로 로드·생성한다.
        // GameHudController가 씬 진입 시 프리팹을 로드하고 InGameModel을 주입한다.
        builder.RegisterEntryPoint<GameHudController>(Lifetime.Scoped);
    }
}
