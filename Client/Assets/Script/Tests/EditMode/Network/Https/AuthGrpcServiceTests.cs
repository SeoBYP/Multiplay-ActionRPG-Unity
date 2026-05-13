using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Services;
using Game.Tests.EditMode.Network.Fakes;
using GameServer.Grpc.Auth;
using GameServer.Grpc.Common;
using Grpc.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Network
{
    [TestFixture]
    public class AuthGrpcServiceTests
    {
        private FakeCallInvoker _invoker;
        private AuthGrpcService _service;

        [SetUp]
        public void SetUp()
        {
            _invoker = new FakeCallInvoker();
            _service = new AuthGrpcService(new FakeGrpcChannelProvider(_invoker));
        }

        // ─── RegisterAsync ──────────────────────────────────────────

        [Test]
        public async Task RegisterAsync_성공응답_반환()
        {
            _invoker.SetResponse("Register", new RegisterResponse
            {
                Result = new Result { Success = true }
            });

            var response = await _service.RegisterAsync(
                new RegisterRequest { Email = "test@test.com", Password = "password123" }).AsTask();

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success);
        }

        [Test]
        public async Task RegisterAsync_이미_존재하는_이메일_RpcException_발생()
        {
            _invoker.SetError("Register", StatusCode.AlreadyExists, "Email already registered.");

            var ex = await Throws<RpcException>(() =>
                _service.RegisterAsync(new RegisterRequest { Email = "dup@test.com", Password = "pass" }).AsTask());

            Assert.AreEqual(StatusCode.AlreadyExists, ex.StatusCode);
        }

        // ─── LoginAsync ─────────────────────────────────────────────

        [Test]
        public async Task LoginAsync_성공응답_AccessToken_반환()
        {
            _invoker.SetResponse("Login", new LoginResponse
            {
                Result = new Result { Success = true },
                AccessToken = "access-token-abc",
                RefreshToken = "refresh-token-xyz",
                SessionId = "session-001"
            });

            var response = await _service.LoginAsync(new LoginRequest
            {
                Email = "test@test.com",
                Password = "password123",
                DeviceId = "device-001"
            }).AsTask();

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success);
            Assert.AreEqual("access-token-abc", response.AccessToken);
            Assert.AreEqual("refresh-token-xyz", response.RefreshToken);
            Assert.AreEqual("session-001", response.SessionId);
        }

        [Test]
        public async Task LoginAsync_잘못된_비밀번호_RpcException_발생()
        {
            _invoker.SetError("Login", StatusCode.Unauthenticated, "Invalid credentials.");

            var ex = await Throws<RpcException>(() =>
                _service.LoginAsync(new LoginRequest
                {
                    Email = "test@test.com",
                    Password = "wrong",
                    DeviceId = "device-001"
                }).AsTask());

            Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
        }

        [Test]
        public async Task LoginAsync_서버_오류_RpcException_발생()
        {
            _invoker.SetError("Login", StatusCode.Internal, "Server error.");

            var ex = await Throws<RpcException>(() =>
                _service.LoginAsync(new LoginRequest()).AsTask());

            Assert.AreEqual(StatusCode.Internal, ex.StatusCode);
        }

        // ─── LogoutAsync ────────────────────────────────────────────

        [Test]
        public async Task LogoutAsync_성공응답_반환()
        {
            _invoker.SetResponse("Logout", new LogoutResponse
            {
                Result = new Result { Success = true }
            });

            var response = await _service.LogoutAsync(new LogoutRequest()).AsTask();

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success);
        }

        [Test]
        public async Task LogoutAsync_인증되지_않은_요청_RpcException_발생()
        {
            _invoker.SetError("Logout", StatusCode.Unauthenticated, "Token not found.");

            var ex = await Throws<RpcException>(() =>
                _service.LogoutAsync(new LogoutRequest()).AsTask());

            Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
        }

        // ─── RefreshAsync ────────────────────────────────────────────

        [Test]
        public async Task RefreshAsync_성공응답_새_AccessToken_반환()
        {
            _invoker.SetResponse("Refresh", new RefreshResponse
            {
                Result = new Result { Success = true },
                AccessToken = "new-access-token",
                RefreshToken = "new-refresh-token"
            });

            var response = await _service.RefreshAsync(new RefreshRequest
            {
                RefreshToken = "old-refresh-token",
                DeviceId = "device-001"
            }).AsTask();

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success);
            Assert.AreEqual("new-access-token", response.AccessToken);
            Assert.AreEqual("new-refresh-token", response.RefreshToken);
        }

        [Test]
        public async Task RefreshAsync_만료된_토큰_RpcException_발생()
        {
            _invoker.SetError("Refresh", StatusCode.Unauthenticated, "Refresh token expired.");

            var ex = await Throws<RpcException>(() =>
                _service.RefreshAsync(new RefreshRequest
                {
                    RefreshToken = "expired-token",
                    DeviceId = "device-001"
                }).AsTask());

            Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
        }

        [Test]
        public async Task RefreshAsync_디바이스_불일치_RpcException_발생()
        {
            _invoker.SetError("Refresh", StatusCode.PermissionDenied, "Device mismatch.");

            var ex = await Throws<RpcException>(() =>
                _service.RefreshAsync(new RefreshRequest
                {
                    RefreshToken = "valid-token",
                    DeviceId = "other-device"
                }).AsTask());

            Assert.AreEqual(StatusCode.PermissionDenied, ex.StatusCode);
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
