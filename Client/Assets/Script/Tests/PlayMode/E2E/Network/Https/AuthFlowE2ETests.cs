using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections;
using Game.Network.Https.Core;
using Game.Network.Https.Interfaces;
using Game.Network.Https.Services;
using NUnit.Framework;
using Game.System.Auth;
using Script.System.Startup;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    [TestFixture]
    public class AuthFlowE2ETests : E2ETestBase
    {
        /// <summary>
        /// 이 픽스처 전용 저장소. 예전엔 프로세스 전역 PlayerPrefs 를 프로덕션 코드와 함께 써서
        /// 다른 곳(에디터 자동 로그인 등)이 같은 키를 덮어쓸 수 있었고, 전체 실행에서만 흔들렸다.
        /// </summary>
        private sealed class InMemoryTokenStore : ITokenStore
        {
            private string _access, _refresh;
            private long _expiresAt;

            public void Save(string accessToken, string refreshToken, long expiresAt)
            {
                _access = accessToken; _refresh = refreshToken; _expiresAt = expiresAt;
            }

            public bool TryLoad(out string accessToken, out string refreshToken, out long expiresAt)
            {
                accessToken = _access; refreshToken = _refresh; expiresAt = _expiresAt;
                return !string.IsNullOrWhiteSpace(_access) && !string.IsNullOrWhiteSpace(_refresh);
            }

            public void Clear() { _access = null; _refresh = null; _expiresAt = 0; }
        }

        private GrpcChannelProvider _authFlowChannelProvider;
        private AuthSession _authSession;
        private IAuthService _clientAuthService;
        private InMemoryTokenStore _tokenStore;

        [SetUp]
        public void 인증흐름_테스트_준비()
        {
            _tokenStore = new InMemoryTokenStore();

            _authFlowChannelProvider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            _authSession = new AuthSession(_tokenStore);
            _authFlowChannelProvider.AccessTokenProvider = () => _authSession.AccessToken;

            IAuthGrpcService authGrpcService = new AuthGrpcService(_authFlowChannelProvider);
            _clientAuthService = new AuthService(authGrpcService, _authFlowChannelProvider, _authSession, new UserProfile(), new StartupIntentQueue());
        }

        [TearDown]
        public void 인증흐름_테스트_정리()
        {
            _tokenStore?.Clear();
            _authFlowChannelProvider?.Dispose();
        }

        [UnityTest]
        public IEnumerator 로그인또는회원가입_최초로그인시_토큰을저장한다() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();

            var result = await _clientAuthService.LoginOrRegisterAsync(email, "Test1234!", CancellationToken.None);

            Assert.AreEqual(AuthResult.Success, result);
            Assert.IsTrue(_clientAuthService.IsAuthenticated);
            Assert.IsTrue(_tokenStore.TryLoad(out var accessToken, out var refreshToken, out var expiresAt));
            Assert.IsNotEmpty(accessToken);
            Assert.IsNotEmpty(refreshToken);
            Assert.Greater(expiresAt, 0);
        });

        [UnityTest]
        public IEnumerator 자동로그인_유효한저장토큰이있으면_성공한다() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            var loginResult = await _clientAuthService.LoginOrRegisterAsync(email, "Test1234!", CancellationToken.None);
            Assert.AreEqual(AuthResult.Success, loginResult);

            var coldStartSession = new AuthSession(_tokenStore);
            using var coldStartProvider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            coldStartProvider.AccessTokenProvider = () => coldStartSession.AccessToken;
            var coldStartService = new AuthService(new AuthGrpcService(coldStartProvider), coldStartProvider, coldStartSession, new UserProfile(), new StartupIntentQueue());

            var autoLoginResult = await coldStartService.TryAutoLoginAsync(CancellationToken.None);

            Assert.AreEqual(AuthResult.Success, autoLoginResult);
            Assert.IsTrue(coldStartService.IsAuthenticated);
        });

        [UnityTest]
        public IEnumerator 자동로그인_만료된토큰이면_리프레시한다() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            var loginResult = await _clientAuthService.LoginOrRegisterAsync(email, "Test1234!", CancellationToken.None);
            Assert.AreEqual(AuthResult.Success, loginResult);
            Assert.IsTrue(_tokenStore.TryLoad(out var oldAccessToken, out var oldRefreshToken, out _));

            _tokenStore.Save(oldAccessToken, oldRefreshToken, 1);

            var coldStartSession = new AuthSession(_tokenStore);
            using var coldStartProvider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            coldStartProvider.AccessTokenProvider = () => coldStartSession.AccessToken;
            var coldStartService = new AuthService(new AuthGrpcService(coldStartProvider), coldStartProvider, coldStartSession, new UserProfile(), new StartupIntentQueue());

            var autoLoginResult = await coldStartService.TryAutoLoginAsync(CancellationToken.None);

            Assert.AreEqual(AuthResult.Success, autoLoginResult);
            Assert.IsTrue(_tokenStore.TryLoad(out var newAccessToken, out var newRefreshToken, out var newExpiresAt));
            Assert.IsNotEmpty(newAccessToken);
            Assert.IsNotEmpty(newRefreshToken);
            Assert.AreNotEqual(oldAccessToken, newAccessToken);
            Assert.AreNotEqual(oldRefreshToken, newRefreshToken);
            Assert.Greater(newExpiresAt, 1);
        });

        [UnityTest]
        public IEnumerator 자동로그인_잘못된리프레시토큰이면_저장토큰을삭제한다() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            var loginResult = await _clientAuthService.LoginOrRegisterAsync(email, "Test1234!", CancellationToken.None);
            Assert.AreEqual(AuthResult.Success, loginResult);
            Assert.IsTrue(_tokenStore.TryLoad(out var accessToken, out _, out _));

            _tokenStore.Save(accessToken, "invalid-refresh-token", 1);

            var coldStartSession = new AuthSession(_tokenStore);
            using var coldStartProvider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            coldStartProvider.AccessTokenProvider = () => coldStartSession.AccessToken;
            var coldStartService = new AuthService(new AuthGrpcService(coldStartProvider), coldStartProvider, coldStartSession, new UserProfile(), new StartupIntentQueue());

            var autoLoginResult = await coldStartService.TryAutoLoginAsync(CancellationToken.None);

            Assert.AreEqual(AuthResult.NeedLogin, autoLoginResult);
            Assert.IsFalse(_tokenStore.TryLoad(out _, out _, out _));
            Assert.IsFalse(coldStartService.IsAuthenticated);
        });

        [UnityTest]
        public IEnumerator 로그인또는회원가입_기존유저가비밀번호를틀리면_회원가입없이실패한다() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await RegisterAndLoginAsync(email, "Test1234!");

            _clientAuthService.Logout();

            var result = await _clientAuthService.LoginOrRegisterAsync(email, "WrongPass!", CancellationToken.None);

            Assert.AreEqual(AuthResult.Failed, result);
            Assert.IsFalse(_clientAuthService.IsAuthenticated);
        });
    }
}
