using System.Threading;
using Cysharp.Threading.Tasks;

namespace Script.System.Auth
{
    public interface IAuthService
    {
        public bool IsAuthenticated { get; }
        
        // 앱 시작 시 호출 — 자동 로그인 시도
        public UniTask<AuthResult> TryAutoLoginAsync(CancellationToken ct);

        // LoginWindow에서 호출
        // 로그인 실패(사용자 없음)면 자동 회원가입 후 재로그인
        public UniTask<AuthResult> LoginOrRegisterAsync(string email, string password, CancellationToken ct);

        public void Logout();
        
        public UniTask<AuthResult> RefreshTokenAsync(CancellationToken ct);

    
    }
}