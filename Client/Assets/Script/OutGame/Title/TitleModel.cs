using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Script.System.Auth;
using VContainer.Unity;

namespace Game.OutGame.Title
{
    /// <summary>
    /// 타이틀 화면의 MVI Model.
    ///
    /// 역할:
    ///   - 앱 시작 시 자동 로그인 시도 (StartAsync)
    ///   - 수동 로그인 처리 (Accept → Login intent)
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

        public void Accept(TitleIntent intent)
        {
            switch (intent)
            {
                case TitleIntent.Login login:
                    LoginAsync(login).Forget();
                    break;
            }
        }

        // ── Effect ───────────────────────────────────

        private async UniTaskVoid LoginAsync(TitleIntent.Login intent)
        {
            _state.Value = _state.Value.WithLoading();
            try
            {
                var result = await _authService.LoginOrRegisterAsync(
                    intent.Email, intent.Password, _cts.Token);

                _state.Value = result == AuthResult.Success
                    ? _state.Value.WithAuthenticated()
                    : _state.Value.WithError(result.ToString());
            }
            catch (OperationCanceledException)
            {
                _state.Value = TitleState.Initial;
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
