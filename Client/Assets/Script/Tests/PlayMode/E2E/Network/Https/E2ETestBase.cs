using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Core;
using Game.Network.Https.Interfaces;
using Game.Network.Https.Services;
using GameServer.Grpc.Auth;
using GameServer.Grpc.User;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    public abstract class E2ETestBase
    {
        protected GrpcChannelProvider ChannelProvider;
        protected IAuthGrpcService AuthService;
        protected IUserGrpcService UserService;
        protected IDungeonLobbyGrpcService LobbyService;
        protected IChatGrpcService ChatService;

        protected string AccessToken;
        protected string RefreshToken;
        protected string SessionId;

        [UnitySetUp]
        public IEnumerator SetUp() => UniTask.ToCoroutine(async () =>
        {
            ChannelProvider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            ChannelProvider.AccessTokenProvider = () => AccessToken;

            AuthService = new AuthGrpcService(ChannelProvider);
            UserService = new UserGrpcService(ChannelProvider);
            LobbyService = new DungeonLobbyGrpcService(ChannelProvider);
            ChatService = new ChatGrpcService(ChannelProvider);
        });

        [UnityTearDown]
        public IEnumerator TearDown() => UniTask.ToCoroutine(async () =>
        {
            ChannelProvider?.Dispose();
        });

        protected static string UniqueEmail()
            => $"e2e_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}@test.com";

        protected static string UniqueNickname(string prefix = "Hero")
        {
            const int maxNicknameLength = 20;
            const int suffixLength = 6;

            var normalizedPrefix = string.IsNullOrWhiteSpace(prefix) ? "Hero" : prefix;
            var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            suffix = suffix.Substring(Math.Max(0, suffix.Length - suffixLength));

            var maxPrefixLength = maxNicknameLength - suffixLength;
            if (normalizedPrefix.Length > maxPrefixLength)
            {
                normalizedPrefix = normalizedPrefix.Substring(0, maxPrefixLength);
            }

            return $"{normalizedPrefix}{suffix}";
        }

        protected static CancellationToken Timeout()
            => new CancellationTokenSource(TimeSpan.FromSeconds(ServerConfig.TimeoutSeconds)).Token;

        protected async UniTask RegisterAndLoginAsync(string email, string password, string deviceId = "e2e-device")
        {
            var register = await AuthService.RegisterAsync(new RegisterRequest
            {
                Email = email,
                Password = password
            }, Timeout());

            Assert.IsTrue(register.Result.Success, $"회원가입 실패: {register.Result.Message}");

            await LoginAsync(email, password, deviceId);
        }

        protected async UniTask LoginAsync(string email, string password, string deviceId = "e2e-device")
        {
            var login = await AuthService.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = password,
                DeviceId = deviceId
            }, Timeout());

            Assert.IsTrue(login.Result.Success, $"로그인 실패: {login.Result.Message}");

            AccessToken = login.AccessToken;
            RefreshToken = login.RefreshToken;
            SessionId = login.SessionId;
        }

        protected async UniTask<string> RegisterLoginAndSetNicknameAsync(string email, string password, string nickname)
        {
            await RegisterAndLoginAsync(email, password);

            var response = await UserService.SetNickNameAsync(new SetNicknameRequest
            {
                Nickname = nickname
            }, Timeout());

            Assert.IsTrue(response.Result.Success, $"닉네임 설정 실패: {response.Result.Message}");
            return nickname;
        }
    }
}
