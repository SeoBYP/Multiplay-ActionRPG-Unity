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
        public System.Collections.IEnumerator Register_새_계정_생성_성공() => UniTask.ToCoroutine(async () =>
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
        public System.Collections.IEnumerator Register_중복_이메일_실패() => UniTask.ToCoroutine(async () =>
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
        public System.Collections.IEnumerator Register_빈_이메일_실패() => UniTask.ToCoroutine(async () =>
        {
            var response = await AuthService.RegisterAsync(new RegisterRequest
            {
                Email = string.Empty,
                Password = "Test1234!"
            }, Timeout());

            Assert.IsFalse(response.Result.Success);
        });

        [UnityTest]
        public System.Collections.IEnumerator Login_정상_토큰_반환() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await RegisterAndLoginAsync(email, "Test1234!");

            Assert.IsNotEmpty(AccessToken);
            Assert.IsNotEmpty(RefreshToken);
            Assert.IsNotEmpty(SessionId);
        });

        [UnityTest]
        public System.Collections.IEnumerator Login_잘못된_비밀번호_실패() => UniTask.ToCoroutine(async () =>
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
        public System.Collections.IEnumerator Login_없는_계정_실패() => UniTask.ToCoroutine(async () =>
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
        public System.Collections.IEnumerator Login_빈_DeviceId_실패() => UniTask.ToCoroutine(async () =>
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
        public System.Collections.IEnumerator Refresh_새_AccessToken_발급() => UniTask.ToCoroutine(async () =>
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
        public System.Collections.IEnumerator Refresh_잘못된_DeviceId_실패() => UniTask.ToCoroutine(async () =>
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
        public System.Collections.IEnumerator Logout_정상_처리() => UniTask.ToCoroutine(async () =>
        {
            var email = UniqueEmail();
            await RegisterAndLoginAsync(email, "Test1234!");

            var response = await AuthService.LogoutAsync(new LogoutRequest(), Timeout());

            Assert.IsTrue(response.Result.Success, response.Result.Message);
        });

        [UnityTest]
        public System.Collections.IEnumerator Logout_이후_Refresh_실패() => UniTask.ToCoroutine(async () =>
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
        public System.Collections.IEnumerator 전체흐름_Register_Login_Refresh_Logout() => UniTask.ToCoroutine(async () =>
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
