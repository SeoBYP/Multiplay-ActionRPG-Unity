using Game.Network.Https.Core;
using Game.Network.Https.Interfaces;
using Game.Network.Https.Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Network.Https
{
    public class GameApiClient : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            // TODO: 외부 설정에서 가져오도록 수정 필요
            var address = "http://localhost:5132"; 
            
            builder.RegisterInstance(new GrpcChannelProvider(address));
            builder.RegisterBuildCallback(container =>
            {
                // 컨테이너 빌드 후 필요한 초기화 로직이 있다면 여기서 수행
                // 예: container.Resolve<GrpcChannelProvider>();
            });
            
            builder.Register<IUserGrpcService, UserGrpcService>(Lifetime.Singleton);
            builder.Register<IAuthGrpcService, AuthGrpcService>(Lifetime.Singleton);
            builder.Register<IChatGrpcService, ChatGrpcService>(Lifetime.Singleton);
            builder.Register<IDungeonLobbyGrpcService, DungeonLobbyGrpcService>(Lifetime.Singleton);
        }
    }
}
