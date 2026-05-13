using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Services;
using Game.Tests.EditMode.Network.Fakes;
using GameServer.Grpc.Common;
using GameServer.Grpc.User;
using Grpc.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Network
{
    [TestFixture]
    public class UserGrpcServiceTests
    {
        private FakeCallInvoker _invoker;
        private UserGrpcService _service;

        [SetUp]
        public void SetUp()
        {
            _invoker = new FakeCallInvoker();
            _service = new UserGrpcService(new FakeGrpcChannelProvider(_invoker));
        }

        // ─── SetNickNameAsync ────────────────────────────────────────

        [Test]
        public async Task SetNickNameAsync_성공응답_반환()
        {
            _invoker.SetResponse("SetNickName", new SetNicknameResponse
            {
                Result = new Result { Success = true }
            });

            var response = await _service.SetNickNameAsync(
                new SetNicknameRequest { Nickname = "Hero" }).AsTask();

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success);
        }

        [Test]
        public async Task SetNickNameAsync_닉네임_중복_RpcException_발생()
        {
            _invoker.SetError("SetNickName", StatusCode.AlreadyExists, "Nickname already taken.");

            var ex = await Throws<RpcException>(() =>
                _service.SetNickNameAsync(new SetNicknameRequest { Nickname = "Hero" }).AsTask());

            Assert.AreEqual(StatusCode.AlreadyExists, ex.StatusCode);
        }

        [Test]
        public async Task SetNickNameAsync_인증되지_않은_요청_RpcException_발생()
        {
            _invoker.SetError("SetNickName", StatusCode.Unauthenticated, "Not authenticated.");

            var ex = await Throws<RpcException>(() =>
                _service.SetNickNameAsync(new SetNicknameRequest { Nickname = "Hero" }).AsTask());

            Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
        }

        [Test]
        public async Task SetNickNameAsync_서버_불가_RpcException_발생()
        {
            _invoker.SetError("SetNickName", StatusCode.Unavailable, "Service unavailable.");

            var ex = await Throws<RpcException>(() =>
                _service.SetNickNameAsync(new SetNicknameRequest { Nickname = "Hero" }).AsTask());

            Assert.AreEqual(StatusCode.Unavailable, ex.StatusCode);
        }

        // ─── 헬퍼 ───────────────────────────────────────────────────

        private static async Task<T> Throws<T>(Func<Task> action) where T : Exception
        {
            try
            {
                await action();
                Assert.Fail($"{typeof(T).Name} 이 발생해야 했지만 발생하지 않았다.");
                return null;
            }
            catch (T ex)
            {
                return ex;
            }
        }
    }
}
