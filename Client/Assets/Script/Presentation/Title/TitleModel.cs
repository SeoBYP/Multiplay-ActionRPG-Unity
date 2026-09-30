using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Game.System.Auth;
using VContainer.Unity;

namespace Game.Presentation.Title
{
    /// <summary>
    /// 타이틀 화면의 MVI Model.
    ///
    /// 역할:
    ///   - 앱 시작 시 자동 로그인 시도 (StartAsync)
    ///   - 수동 로그인·회원가입 처리 (Accept → Login / Register intent, 입력 검증 포함)
    ///   - View에 인증 상태(IsAuthenticated, IsLoading, Error) 발행
    ///
    /// GUI 레이어는 IAuthService를 직접 참조하지 않는다.
    /// LoginWindow / MainMenuWindow 는 이 Model만 주입받는다.
    /// </summary>
    public sealed class TitleModel : IAsyncStartable, IDisposable
    {
        private readonly IAuthService _authService;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly ReactiveProperty<TitleState> _state
            = new ReactiveProperty<TitleState>(TitleState.Initial);

        public ReadOnlyReactiveProperty<TitleState> State =>
            _state.ToReadOnlyReactiveProperty();

        public TitleModel(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// 자동 로그인을 시도하고 결과를 State에 반영한다.
        ///
        /// - 에디터: EditorAutoLoginInitializer(ProjectLifetimeScope)가 이미 인증을 완료했을 수 있으므로
        ///   IsAuthenticated 확인 후 즉시 반환.
        /// - 빌드: TryAutoLoginAsync를 직접 호출해 저장된 토큰으로 자동 로그인 시도.
        ///   실패 시 IsAuthenticated = false 유지 → LoginWindow에서 수동 로그인 대기.
        /// </summary>
        public async UniTask StartAsync(CancellationToken ct)
        {
            try
            {
                // 에디터 자동 로그인이 이미 완료된 경우 즉시 반영
                if (_authService.IsAuthenticated)
                {
                    _state.Value = _state.Value.WithAuthenticated();
                    return;
                }

                var result = await _authService.TryAutoLoginAsync(ct);
                if (result == AuthResult.Success)
                    _state.Value = _state.Value.WithAuthenticated();
                // 실패 시: IsAuthenticated = false 유지, LoginWindow에서 수동 로그인 대기
            }
            catch (OperationCanceledException)
            {
                // 씬 전환 또는 정상 취소 — 처리 불필요
            }
        }

        // ── View의 단일 진입점 ────────────────────────

        public const string EmptyInputMessage       = "이메일과 비밀번호를 입력하세요.";
        public const string PasswordMismatchMessage = "비밀번호가 일치하지 않습니다.";
        public const string LoginFailedMessage      = "이메일 또는 비밀번호가 올바르지 않습니다.";
        public const string RegisterFailedMessage   = "회원가입에 실패했습니다. 이미 사용 중인 이메일일 수 있습니다.";
        public const string ConnectionFailedMessage = "서버에 연결할 수 없습니다.";

        public void Accept(TitleIntent intent)
        {
            switch (intent)
            {
                case TitleIntent.Login login:
                    if (IsBlank(login.Email, login.Password)) { _state.Value = _state.Value.WithError(EmptyInputMessage); return; }
                    RunAuthAsync(ct => _authService.LoginOrRegisterAsync(login.Email, login.Password, ct), LoginFailedMessage).Forget();
                    break;

                case TitleIntent.Register register:
                    if (IsBlank(register.Email, register.Password)) { _state.Value = _state.Value.WithError(EmptyInputMessage); return; }
                    if (register.Password != register.PasswordConfirm) { _state.Value = _state.Value.WithError(PasswordMismatchMessage); return; }
                    RunAuthAsync(ct => _authService.RegisterAsync(register.Email, register.Password, ct), RegisterFailedMessage).Forget();
                    break;
            }
        }

        private static bool IsBlank(string email, string password)
            => string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password);

        // ── Effect ───────────────────────────────────

        private async UniTaskVoid RunAuthAsync(Func<CancellationToken, UniTask<AuthResult>> call, string failedMessage)
        {
            _state.Value = _state.Value.WithLoading();
            try
            {
                var result = await call(_cts.Token);

                _state.Value = result == AuthResult.Success
                    ? _state.Value.WithAuthenticated()
                    : _state.Value.WithError(failedMessage);
            }
            catch (OperationCanceledException)
            {
                _state.Value = TitleState.Initial;
            }
            catch (Exception e)
            {
                // 서버 미기동·네트워크 단절 — 로딩에 갇히지 않도록 에러 상태로 되돌린다.
                UnityEngine.Debug.LogWarning($"[TitleModel] 인증 요청 실패: {e.Message}");
                _state.Value = _state.Value.WithError(ConnectionFailedMessage);
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _state.Dispose();
        }
    }
}
