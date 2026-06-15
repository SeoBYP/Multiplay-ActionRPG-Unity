using Game.Network.Https.Core;
using Game.Network.Https.Interfaces;
using Game.Network.Https.Services;
using VContainer;
using VContainer.Unity;

namespace Game.Network.Https
{
    /// <summary>
    /// HTTP/gRPC 기반 API 서비스들을 VContainer에 등록하는 설치 엔트리포인트.
    /// </summary>
    public class GameApiClient : IInstaller
    {
        /// <summary>
        /// 공용 gRPC 채널과 각 도메인별 클라이언트 서비스를 등록한다.
        /// </summary>
        public void Install(IContainerBuilder builder)
        {
            // TODO: 추후 설정 파일 또는 환경별 설정에서 주소를 주입받도록 변경 필요.
            var address = "http://localhost:5132";

            // 모든 gRPC 서비스가 공유할 채널 공급자를 등록한다.
            builder.RegisterInstance(new GrpcChannelProvider(address));
            builder.RegisterBuildCallback(container =>
            {
                // 빌드 시점에 채널 공급자를 한 번 resolve 해서 생성 가능 여부를 조기 검증한다.
                container.Resolve<GrpcChannelProvider>();
            });

            // 각 서비스는 동일 채널을 공유하면서 도메인별 RPC 호출만 캡슐화한다.
            builder.Register<IUserGrpcService, UserGrpcService>(Lifetime.Singleton);
            builder.Register<IAuthGrpcService, AuthGrpcService>(Lifetime.Singleton);
            builder.Register<IChatGrpcService, ChatGrpcService>(Lifetime.Singleton);
            builder.Register<IDungeonLobbyGrpcService, DungeonLobbyGrpcService>(Lifetime.Singleton);
            builder.Register<IInventoryGrpcService, InventoryGrpcService>(Lifetime.Singleton);
            builder.Register<IEquipmentGrpcService, EquipmentGrpcService>(Lifetime.Singleton);
            builder.Register<IProgressionGrpcService, ProgressionGrpcService>(Lifetime.Singleton);
        }
    }
}
