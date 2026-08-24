using System.Collections;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.Auth;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    [TestFixture]
    public class AuthE2ETests : E2ETestBase
    {
        [UnityTest]
        public IEnumerator Register_새_계정_생성_성공() => UniTask.ToCoroutine(async () =>
        {
            var response = await AuthService.RegisterAsync(new RegisterRequest
            {
                Email = UniqueEmail(),
                Password = "Test1234!"
            }, Timeout());

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Result.Success, response.Result.Message);
        });

        [UnityTest]
        public IEnumerator Register_중복_이메일_실패() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();

            await AuthService.RegisterAsync(new RegisterRequest
            {
                Email = email,
                Password = "Test1234!"
            }, Timeout());

            var response = await AuthService.RegisterAsync(new RegisterRequest
            {
                Email = email,
                Password = "Test1234!"
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });

        [UnityTest]
        public IEnumerator Register_빈_이메일_실패() => UniTask.ToCoroutine(async () =>
        {
            var response = await AuthService.RegisterAsync(new RegisterRequest
            {
                Email = string.Empty,
                Password = "Test1234!"
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });

        [UnityTest]
        public IEnumerator Login_정상_토큰_반환() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await RegisterAndLoginAsync(email, "Test1234!");

            Assert.IsNotEmpty(AccessToken);
            Assert.IsNotEmpty(RefreshToken);
            Assert.IsNotEmpty(SessionId);
        });

        [UnityTest]
        public IEnumerator Login_잘못된_비밀번호_실패() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await AuthService.RegisterAsync(new RegisterRequest
            {
                Email = email,
                Password = "Test1234!"
            }, Timeout());

            var response = await AuthService.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = "WrongPass!",
                DeviceId = "e2e-device"
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });

        [UnityTest]
        public IEnumerator Login_없는_계정_실패() => UniTask.ToCoroutine(async () =>
        {
            var response = await AuthService.LoginAsync(new LoginRequest
            {
                Email = "nobody@nowhere.com",
                Password = "Test1234!",
                DeviceId = "e2e-device"
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });

        [UnityTest]
        public IEnumerator Login_빈_DeviceId_실패() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await AuthService.RegisterAsync(new RegisterRequest
            {
                Email = email,
                Password = "Test1234!"
            }, Timeout());

            var response = await AuthService.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = "Test1234!",
                DeviceId = string.Empty
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });

        [UnityTest]
        public IEnumerator Refresh_새_AccessToken_발급() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await RegisterAndLoginAsync(email, "Test1234!");
            var previousAccessToken = AccessToken;

            var response = await AuthService.RefreshAsync(new RefreshRequest
            {
                RefreshToken = RefreshToken,
                DeviceId = "e2e-device"
            }, Timeout());

            Assert.IsTrue(response.Result.Success, response.Result.Message);
            Assert.IsNotEmpty(response.AccessToken);
            Assert.AreNotEqual(previousAccessToken, response.AccessToken);
        });

        [UnityTest]
        public IEnumerator Refresh_잘못된_DeviceId_실패() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await RegisterAndLoginAsync(email, "Test1234!");

            var response = await AuthService.RefreshAsync(new RefreshRequest
            {
                RefreshToken = RefreshToken,
                DeviceId = "another-device"
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });

        [UnityTest]
        public IEnumerator Refresh_잘못된_DeviceId_실패해도_정상_기기는_갱신된다() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await RegisterAndLoginAsync(email, "Test1234!");

            var wrongDevice = await AuthService.RefreshAsync(new RefreshRequest
            {
                RefreshToken = RefreshToken,
                DeviceId = "another-device"
            }, Timeout());
            Assert.IsFalse(wrongDevice.Result.Success);

            // 소유 증명에 실패한 요청은 세션을 파괴하지 않는다 — 정상 기기가 그대로 갱신할 수 있어야 한다.
            var normalDevice = await AuthService.RefreshAsync(new RefreshRequest
            {
                RefreshToken = RefreshToken,
                DeviceId = "e2e-device"
            }, Timeout());

            Assert.IsTrue(normalDevice.Result.Success, normalDevice.Result.Message);
        });

        [UnityTest]
        public IEnumerator Refresh_위조된_리프레시_문자열은_세션을_끊지_못한다() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await RegisterAndLoginAsync(email, "Test1234!");

            // 유출된 accessToken 하나로 아무 문자열이나 던지는 DoS 시도(버전 역행 형식 포함)
            var forged = await AuthService.RefreshAsync(new RefreshRequest
            {
                RefreshToken = "aaa.0",
                DeviceId = "attacker-device"
            }, Timeout());
            Assert.IsFalse(forged.Result.Success);

            var normalDevice = await AuthService.RefreshAsync(new RefreshRequest
            {
                RefreshToken = RefreshToken,
                DeviceId = "e2e-device"
            }, Timeout());

            Assert.IsTrue(normalDevice.Result.Success, normalDevice.Result.Message);
        });

        [UnityTest]
        public IEnumerator Logout_정상_처리() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await RegisterAndLoginAsync(email, "Test1234!");

            var response = await AuthService.LogoutAsync(new LogoutRequest(), Timeout());

            Assert.IsTrue(response.Result.Success, response.Result.Message);
        });

        [UnityTest]
        public IEnumerator Logout_이후_Refresh_실패() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await RegisterAndLoginAsync(email, "Test1234!");

            var logout = await AuthService.LogoutAsync(new LogoutRequest(), Timeout());
            Assert.IsTrue(logout.Result.Success, logout.Result.Message);

            var refresh = await AuthService.RefreshAsync(new RefreshRequest
            {
                RefreshToken = RefreshToken,
                DeviceId = "e2e-device"
            }, Timeout());

            Assert.IsFalse(refresh.Result.Success);
        });

        [UnityTest]
        public IEnumerator 전체흐름_Register_Login_Refresh_Logout() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();

            var register = await AuthService.RegisterAsync(new RegisterRequest
            {
                Email = email,
                Password = "Test1234!"
            }, Timeout());
            Assert.IsTrue(register.Result.Success);

            var login = await AuthService.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = "Test1234!",
                DeviceId = "e2e-device"
            }, Timeout());
            Assert.IsTrue(login.Result.Success);

            AccessToken = login.AccessToken;
            RefreshToken = login.RefreshToken;
            SessionId = login.SessionId;

            var refresh = await AuthService.RefreshAsync(new RefreshRequest
            {
                RefreshToken = RefreshToken,
                DeviceId = "e2e-device"
            }, Timeout());
            Assert.IsTrue(refresh.Result.Success);

            AccessToken = refresh.AccessToken;
            RefreshToken = refresh.RefreshToken;
            SessionId = refresh.SessionId;

            var logout = await AuthService.LogoutAsync(new LogoutRequest(), Timeout());
            Assert.IsTrue(logout.Result.Success);
        });
    }
}
