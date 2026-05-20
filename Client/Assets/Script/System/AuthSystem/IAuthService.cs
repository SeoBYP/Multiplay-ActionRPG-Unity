using System.Threading;
using Cysharp.Threading.Tasks;

namespace Script.System.Auth
{
    public interface IAuthService
    {
        bool IsAuthenticated { get; }

        /// <summary>최초 인증 완료 시까지 대기. 이미 인증됐으면 즉시 반환.</summary>
        UniTask AuthenticatedAsync();

        // 앱 시작 시 호출 — 자동 로그인 시도
        UniTask<AuthResult> TryAutoLoginAsync(CancellationToken ct);

        // LoginWindow에서 호출
        // 로그인 실패(사용자 없음)면 자동 회원가입 후 재로그인
        UniTask<AuthResult> LoginOrRegisterAsync(string email, string password, CancellationToken ct);

        void Logout();

        UniTask<AuthResult> RefreshTokenAsync(CancellationToken ct);
    }
}