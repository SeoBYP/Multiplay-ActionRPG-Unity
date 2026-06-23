using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Core;
using Game.Network.Https.Interfaces;
using Game.Network.Https.Services;
using GameServer.Grpc.Auth;
using GameServer.Grpc.User;
using NUnit.Framework;

namespace Game.Tests.PlayMode.E2E
{
    public abstract class E2ETestBase
    {
        protected GrpcChannelProvider ChannelProvider;
        protected IAuthGrpcService AuthService;
        protected IUserGrpcService UserService;
        protected IDungeonLobbyGrpcService LobbyService;
        protected IChatGrpcService ChatService;
        protected IInventoryGrpcService InventoryService;
        protected IEquipmentGrpcService EquipmentService;
        protected IProgressionGrpcService ProgressionService;
        protected IWalletGrpcService WalletService;
        protected IQuestGrpcService QuestService;

        protected string AccessToken;
        protected string RefreshToken;
        protected string SessionId;

        // [UnitySetUp] / [UnityTearDown] + UniTask.ToCoroutine 대신 일반 [SetUp] / [TearDown] 사용.
        // SetUp/TearDown은 동기 코드만 포함하므로 코루틴 상태머신이 불필요하다.
        // [UnitySetUp]을 쓰면 EditMode 테스트 실행 후 EditModePcHelper가
        // null enumerator를 복원하려다 NullReferenceException을 던지는 문제가 생긴다.
        [SetUp]
        public void SetUp()
        {
            // 각 테스트는 미인증 상태에서 시작한다. 리셋하지 않으면 같은 픽스처의 앞선 테스트가
            // 로그인해 둔 토큰이 누수돼, "미인증 거부" 테스트가 인증 상태로 실행되는 순서 의존 버그가 생긴다.
            AccessToken = null;
            RefreshToken = null;
            SessionId = null;

            ChannelProvider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            ChannelProvider.AccessTokenProvider = () => AccessToken;

            AuthService = new AuthGrpcService(ChannelProvider);
            UserService = new UserGrpcService(ChannelProvider);
            LobbyService = new DungeonLobbyGrpcService(ChannelProvider);
            ChatService = new ChatGrpcService(ChannelProvider);
            InventoryService = new InventoryGrpcService(ChannelProvider);
            EquipmentService = new EquipmentGrpcService(ChannelProvider);
            ProgressionService = new ProgressionGrpcService(ChannelProvider);
            WalletService = new WalletGrpcService(ChannelProvider);
            QuestService = new QuestGrpcService(ChannelProvider);
        }

        [TearDown]
        public void TearDown()
        {
            ChannelProvider?.Dispose();
        }

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
