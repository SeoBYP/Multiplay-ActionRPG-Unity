using System;
using Game.Network.Https;
using Game.Network.Https.Core;
using Game.Network.Https.Interfaces;
using Game.Network.Https.Services;
using NUnit.Framework;
using VContainer;

namespace Game.Tests.EditMode.Https
{
    [TestFixture]
    public class GameApiClientTest
    {
        private IObjectResolver _container;
        private GrpcChannelProvider _channelProvider;

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            var installer = new GameApiClient();
            installer.Install(builder);
            _container = builder.Build();
            _channelProvider = _container.Resolve<GrpcChannelProvider>();
        }

        [TearDown]
        public void TearDown()
        {
            (_container as IDisposable)?.Dispose();
            _channelProvider?.Dispose();
        }

        [Test]
        public void GrpcChannelProvider_등록됨()
        {
            Assert.IsNotNull(_container.Resolve<GrpcChannelProvider>());
        }

        [Test]
        public void IAuthGrpcService_등록됨()
        {
            Assert.IsNotNull(_container.Resolve<IAuthGrpcService>());
        }

        [Test]
        public void IUserGrpcService_등록됨()
        {
            Assert.IsNotNull(_container.Resolve<IUserGrpcService>());
        }

        [Test]
        public void IChatGrpcService_등록됨()
        {
            Assert.IsNotNull(_container.Resolve<IChatGrpcService>());
        }

        [Test]
        public void IDungeonLobbyGrpcService_등록됨()
        {
            Assert.IsNotNull(_container.Resolve<IDungeonLobbyGrpcService>());
        }

        [Test]
        public void IAuthGrpcService_실제_타입은_AuthGrpcService()
        {
            Assert.IsInstanceOf<AuthGrpcService>(_container.Resolve<IAuthGrpcService>());
        }

        [Test]
        public void IUserGrpcService_실제_타입은_UserGrpcService()
        {
            Assert.IsInstanceOf<UserGrpcService>(_container.Resolve<IUserGrpcService>());
        }

        [Test]
        public void IChatGrpcService_실제_타입은_ChatGrpcService()
        {
            Assert.IsInstanceOf<ChatGrpcService>(_container.Resolve<IChatGrpcService>());
        }

        [Test]
        public void IDungeonLobbyGrpcService_실제_타입은_DungeonLobbyGrpcService()
        {
            Assert.IsInstanceOf<DungeonLobbyGrpcService>(_container.Resolve<IDungeonLobbyGrpcService>());
        }

        [Test]
        public void IAuthGrpcService_Singleton_동일_인스턴스_반환()
        {
            var s1 = _container.Resolve<IAuthGrpcService>();
            var s2 = _container.Resolve<IAuthGrpcService>();
            Assert.AreSame(s1, s2);
        }

        [Test]
        public void IUserGrpcService_Singleton_동일_인스턴스_반환()
        {
            var s1 = _container.Resolve<IUserGrpcService>();
            var s2 = _container.Resolve<IUserGrpcService>();
            Assert.AreSame(s1, s2);
        }

        [Test]
        public void IChatGrpcService_Singleton_동일_인스턴스_반환()
        {
            var s1 = _container.Resolve<IChatGrpcService>();
            var s2 = _container.Resolve<IChatGrpcService>();
            Assert.AreSame(s1, s2);
        }

        [Test]
        public void IDungeonLobbyGrpcService_Singleton_동일_인스턴스_반환()
        {
            var s1 = _container.Resolve<IDungeonLobbyGrpcService>();
            var s2 = _container.Resolve<IDungeonLobbyGrpcService>();
            Assert.AreSame(s1, s2);
        }

        [Test]
        public void GrpcChannelProvider_RegisterInstance_항상_동일_인스턴스()
        {
            var p1 = _container.Resolve<GrpcChannelProvider>();
            var p2 = _container.Resolve<GrpcChannelProvider>();
            Assert.AreSame(p1, p2);
        }

        [Test]
        public void GrpcChannelProvider_Address_비어있지_않음()
        {
            var provider = _container.Resolve<GrpcChannelProvider>();
            Assert.IsFalse(string.IsNullOrWhiteSpace(provider.Address));
        }
    }
}
