using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Presentation.Title;
using Game.System.Auth;
using NUnit.Framework;

namespace Game.Tests.EditMode.Presentation
{
    public class TitleModelTests
    {
        [Test]
        public void 회원가입_비밀번호_확인이_다르면_서비스를_호출하지_않고_에러를_표시한다()
        {
            var auth = new FakeAuthService();
            using var model = new TitleModel(auth);

            model.Accept(new TitleIntent.Register("a@test.com", "pw1234", "pw9999"));

            Assert.That(auth.RegisterCalls, Is.EqualTo(0));
            Assert.That(model.State.CurrentValue.ErrorMessage, Is.EqualTo(TitleModel.PasswordMismatchMessage));
        }

        [Test]
        public void 회원가입에_성공하면_입력한_계정으로_가입하고_인증_상태가_된다()
        {
            var auth = new FakeAuthService { RegisterResult = AuthResult.Success };
            using var model = new TitleModel(auth);

            model.Accept(new TitleIntent.Register("a@test.com", "pw1234", "pw1234"));

            Assert.That(auth.RegisterCalls, Is.EqualTo(1));
            Assert.That(auth.LastEmail, Is.EqualTo("a@test.com"));
            Assert.That(model.State.CurrentValue.IsAuthenticated, Is.True);
        }

        [Test]
        public void 회원가입에_실패하면_에러를_표시한다()
        {
            var auth = new FakeAuthService { RegisterResult = AuthResult.Failed };
            using var model = new TitleModel(auth);

            model.Accept(new TitleIntent.Register("a@test.com", "pw1234", "pw1234"));

            Assert.That(model.State.CurrentValue.IsAuthenticated, Is.False);
            Assert.That(model.State.CurrentValue.ErrorMessage, Is.EqualTo(TitleModel.RegisterFailedMessage));
        }

        [Test]
        public void 빈_입력으로_로그인하면_서비스를_호출하지_않고_에러를_표시한다()
        {
            var auth = new FakeAuthService();
            using var model = new TitleModel(auth);

            model.Accept(new TitleIntent.Login("", "pw1234"));

            Assert.That(auth.LoginCalls, Is.EqualTo(0));
            Assert.That(model.State.CurrentValue.ErrorMessage, Is.EqualTo(TitleModel.EmptyInputMessage));
        }

        [Test]
        public void 로그인에_실패하면_사람이_읽을_수_있는_에러를_표시한다()
        {
            var auth = new FakeAuthService { LoginResult = AuthResult.Failed };
            using var model = new TitleModel(auth);

            model.Accept(new TitleIntent.Login("a@test.com", "wrong"));

            Assert.That(model.State.CurrentValue.IsLoading, Is.False);
            Assert.That(model.State.CurrentValue.ErrorMessage, Is.EqualTo(TitleModel.LoginFailedMessage));
        }

        [Test]
        public void 로그인_중_예외가_나면_로딩이_풀리고_에러를_표시한다()
        {
            var auth = new FakeAuthService { LoginThrows = true };
            using var model = new TitleModel(auth);

            model.Accept(new TitleIntent.Login("a@test.com", "pw1234"));

            Assert.That(model.State.CurrentValue.IsLoading, Is.False);
            Assert.That(model.State.CurrentValue.ErrorMessage, Is.EqualTo(TitleModel.ConnectionFailedMessage));
        }

        private sealed class FakeAuthService : IAuthService
        {
            public AuthResult LoginResult = AuthResult.Success;
            public AuthResult RegisterResult = AuthResult.Success;
            public bool LoginThrows;
            public int LoginCalls;
            public int RegisterCalls;
            public string LastEmail;

            public bool IsAuthenticated => false;
            public UniTask AuthenticatedAsync() => UniTask.CompletedTask;
            public UniTask<AuthResult> TryAutoLoginAsync(CancellationToken ct) => UniTask.FromResult(AuthResult.NeedLogin);

            public UniTask<AuthResult> LoginOrRegisterAsync(string email, string password, CancellationToken ct)
            {
                LoginCalls++;
                LastEmail = email;
                return LoginThrows
                    ? UniTask.FromException<AuthResult>(new Exception("connection refused"))
                    : UniTask.FromResult(LoginResult);
            }

            public UniTask<AuthResult> RegisterAsync(string email, string password, CancellationToken ct)
            {
                RegisterCalls++;
                LastEmail = email;
                return UniTask.FromResult(RegisterResult);
            }

            public void Logout() { }
            public UniTask<AuthResult> RefreshTokenAsync(CancellationToken ct) => UniTask.FromResult(AuthResult.Success);
        }
    }
}
