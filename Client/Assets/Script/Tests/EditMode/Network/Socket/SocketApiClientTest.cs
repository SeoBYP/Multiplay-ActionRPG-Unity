using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using NUnit.Framework;
using VContainer;

namespace Game.Tests.EditMode.Socket
{
    [TestFixture]
    public class SocketApiClientTest
    {
        private IObjectResolver _container;

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            var installer = new SocketApiClient();
            installer.Install(builder);
            _container = builder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            (_container as IDisposable)?.Dispose();
        }

        [Test]
        public void SocketServices_등록됨()
        {
            Assert.IsNotNull(_container.Resolve<ISocketPacketState>());
            Assert.IsNotNull(_container.Resolve<ISocketPacketDispatcher>());
            Assert.IsNotNull(_container.Resolve<ISocketConnector>());
            Assert.IsNotNull(_container.Resolve<ISocketSession>());
        }

        [Test]
        public async Task PlayerJoined_후_Move_Dispatch_플레이어_상태_갱신()
        {
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            await dispatcher.DispatchAsync(new S_PlayerJoined
            {
                Success = true,
                UserId = 101,
                Nickname = "alpha",
                PosX = 1.5f,
                PosY = 2.5f,
                PosZ = 3.5f,
                RotY = 45f
            });

            Assert.IsTrue(state.TryGetPlayer(101, out var joinedPlayer));
            Assert.AreEqual("alpha", joinedPlayer.Nickname);
            Assert.AreEqual(1.5f, joinedPlayer.PosX);
            Assert.AreEqual(45f, joinedPlayer.RotY);

            await dispatcher.DispatchAsync(new S_Move
            {
                UserId = 101,
                PosX = 10f,
                PosY = 20f,
                PosZ = 30f,
                RotY = 90f,
                TimeStamp = 777
            });

            Assert.IsTrue(state.TryGetPlayer(101, out var movedPlayer));
            Assert.AreEqual("alpha", movedPlayer.Nickname);
            Assert.AreEqual(10f, movedPlayer.PosX);
            Assert.AreEqual(20f, movedPlayer.PosY);
            Assert.AreEqual(30f, movedPlayer.PosZ);
            Assert.AreEqual(90f, movedPlayer.RotY);
            Assert.AreEqual(777L, movedPlayer.TimeStamp);
        }

        [Test]
        public async Task PlayerJoined_실패시_플레이어_추가되지_않음()
        {
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            await dispatcher.DispatchAsync(new S_PlayerJoined
            {
                Success = false,
                UserId = 404,
                Nickname = "blocked"
            });

            Assert.IsFalse(state.TryGetPlayer(404, out _));
        }

        [Test]
        public async Task PlayerLeft_Dispatch시_플레이어가_상태에서_제거된다()
        {
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            await dispatcher.DispatchAsync(new S_PlayerJoined
            {
                Success = true,
                UserId = 202,
                Nickname = "leaver"
            });
            Assert.IsTrue(state.TryGetPlayer(202, out _));

            await dispatcher.DispatchAsync(new S_PlayerLeft { UserId = 202 });

            Assert.IsFalse(state.TryGetPlayer(202, out _));
        }

        [Test]
        public void RemovePlayer_없는_유저여도_예외없이_무시된다()
        {
            var state = _container.Resolve<ISocketPacketState>();

            Assert.DoesNotThrow(() => state.RemovePlayer(999999));
            Assert.IsFalse(state.TryGetPlayer(999999, out _));
        }

        [Test]
        public void GetAllPlayers_업서트한_전원을_반환하고_제거되면_빠진다()
        {
            var state = _container.Resolve<ISocketPacketState>();

            state.UpsertPlayer(1, "a", 0, 0, 0, 0);
            state.UpsertPlayer(2, "b", 0, 0, 0, 0);
            CollectionAssert.AreEquivalent(
                new[] { 1L, 2L },
                state.GetAllPlayers().Select(p => p.UserId).ToArray());

            state.RemovePlayer(1);
            CollectionAssert.AreEquivalent(
                new[] { 2L },
                state.GetAllPlayers().Select(p => p.UserId).ToArray());
        }
    }
}
