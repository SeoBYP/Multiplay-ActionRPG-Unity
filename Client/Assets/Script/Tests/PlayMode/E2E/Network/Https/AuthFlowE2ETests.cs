using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Core;
using Game.Network.Https.Interfaces;
using Game.Network.Https.Services;
using NUnit.Framework;
using Script.System.Auth;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    [TestFixture]
    public class AuthFlowE2ETests : E2ETestBase
    {
        private GrpcChannelProvider _authFlowChannelProvider;
        private AuthSession _authSession;
        private IAuthService _clientAuthService;

        [UnitySetUp]
        public System.Collections.IEnumerator AuthFlowSetUp() => UniTask.ToCoroutine(async () =>
        {
            TokenStorage.Clear();

            _authFlowChannelProvider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            _authSession = new AuthSession();
            _authFlowChannelProvider.AccessTokenProvider = () => _authSession.AccessToken;

            IAuthGrpcService authGrpcService = new AuthGrpcService(_authFlowChannelProvider);
            _clientAuthService = new AuthService(authGrpcService, _authFlowChannelProvider, _authSession);
        });

        [UnityTearDown]
        public System.Collections.IEnumerator AuthFlowTearDown() => UniTask.ToCoroutine(async () =>
        {
            TokenStorage.Clear();
            _authFlowChannelProvider?.Dispose();
        });

        [UnityTest]
        public System.Collections.IEnumerator LoginOrRegister_FirstLogin_PersistsTokens() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();

            var result = await _clientAuthService.LoginOrRegisterAsync(email, "Test1234!", CancellationToken.None);

            Assert.AreEqual(AuthResult.Success, result);
            Assert.IsTrue(_clientAuthService.IsAuthenticated);
            Assert.IsTrue(TokenStorage.TryLoad(out var accessToken, out var refreshToken, out var expiresAt));
            Assert.IsNotEmpty(accessToken);
            Assert.IsNotEmpty(refreshToken);
            Assert.Greater(expiresAt, 0);
        });

        [UnityTest]
        public System.Collections.IEnumerator TryAutoLogin_ValidStoredToken_SucceedsWithoutLoginUI() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            var loginResult = await _clientAuthService.LoginOrRegisterAsync(email, "Test1234!", CancellationToken.None);
            Assert.AreEqual(AuthResult.Success, loginResult);

            var coldStartSession = new AuthSession();
            using var coldStartProvider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            coldStartProvider.AccessTokenProvider = () => coldStartSession.AccessToken;
            var coldStartService = new AuthService(new AuthGrpcService(coldStartProvider), coldStartProvider, coldStartSession);

            var autoLoginResult = await coldStartService.TryAutoLoginAsync(CancellationToken.None);

            Assert.AreEqual(AuthResult.Success, autoLoginResult);
            Assert.IsTrue(coldStartService.IsAuthenticated);
        });

        [UnityTest]
        public System.Collections.IEnumerator TryAutoLogin_ExpiredStoredToken_RefreshesTokens() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            var loginResult = await _clientAuthService.LoginOrRegisterAsync(email, "Test1234!", CancellationToken.None);
            Assert.AreEqual(AuthResult.Success, loginResult);
            Assert.IsTrue(TokenStorage.TryLoad(out var oldAccessToken, out var oldRefreshToken, out _));

            TokenStorage.Save(oldAccessToken, oldRefreshToken, 1);

            var coldStartSession = new AuthSession();
            using var coldStartProvider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            coldStartProvider.AccessTokenProvider = () => coldStartSession.AccessToken;
            var coldStartService = new AuthService(new AuthGrpcService(coldStartProvider), coldStartProvider, coldStartSession);

            var autoLoginResult = await coldStartService.TryAutoLoginAsync(CancellationToken.None);

            Assert.AreEqual(AuthResult.Success, autoLoginResult);
            Assert.IsTrue(TokenStorage.TryLoad(out var newAccessToken, out var newRefreshToken, out var newExpiresAt));
            Assert.IsNotEmpty(newAccessToken);
            Assert.IsNotEmpty(newRefreshToken);
            Assert.AreNotEqual(oldAccessToken, newAccessToken);
            Assert.AreNotEqual(oldRefreshToken, newRefreshToken);
            Assert.Greater(newExpiresAt, 1);
        });

        [UnityTest]
        public System.Collections.IEnumerator TryAutoLogin_InvalidRefreshToken_ClearsStoredTokens() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            var loginResult = await _clientAuthService.LoginOrRegisterAsync(email, "Test1234!", CancellationToken.None);
            Assert.AreEqual(AuthResult.Success, loginResult);
            Assert.IsTrue(TokenStorage.TryLoad(out var accessToken, out _, out _));

            TokenStorage.Save(accessToken, "invalid-refresh-token", 1);

            var coldStartSession = new AuthSession();
            using var coldStartProvider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            coldStartProvider.AccessTokenProvider = () => coldStartSession.AccessToken;
            var coldStartService = new AuthService(new AuthGrpcService(coldStartProvider), coldStartProvider, coldStartSession);

            var autoLoginResult = await coldStartService.TryAutoLoginAsync(CancellationToken.None);

            Assert.AreEqual(AuthResult.NeedLogin, autoLoginResult);
            Assert.IsFalse(TokenStorage.TryLoad(out _, out _, out _));
            Assert.IsFalse(coldStartService.IsAuthenticated);
        });

        [UnityTest]
        public System.Collections.IEnumerator LoginOrRegister_ExistingUserWrongPassword_FailsWithoutRegisterFallback() => UniTask.ToCoroutine(async () =>
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
