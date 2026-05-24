using System;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;

namespace Script.System.Auth
{
    /// <summary>
    /// 메모리에 유지되는 현재 로그인 세션 상태.
    /// 토큰이 갱신될 때마다 PlayerPrefs 저장소와 함께 갱신된다.
    /// </summary>
    public class AuthSession
    {
        private readonly UniTaskCompletionSource _authenticatedTcs = new();

        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }
        public long ExpiresAt { get; private set; }
        /// <summary>JWT "sub" 클레임에서 파싱한 서버 내부 유저 ID.</summary>
        public long UserId { get; private set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

        /// <summary>
        /// 최초 인증 완료 시까지 대기한다.
        /// 이미 인증된 상태라면 즉시 반환된다.
        /// </summary>
        public UniTask AuthenticatedAsync() => _authenticatedTcs.Task;

        /// <summary>
        /// 서버에서 받은 최신 토큰 세트를 메모리와 영속 저장소에 반영한다.
        /// </summary>
        public void Update(string accessToken, string refreshToken, long expiresAt)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            ExpiresAt = expiresAt;
            UserId = ParseUserIdFromToken(accessToken);
            TokenStorage.Save(accessToken, refreshToken, expiresAt);
            _authenticatedTcs.TrySetResult(); // 최초 인증 시 1회 신호 — 이후 호출은 무시됨
        }

        private static long ParseUserIdFromToken(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken)) return 0;
            var parts = accessToken.Split('.');
            if (parts.Length < 2) return 0;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "=";  break;
            }
            try
            {
                var json  = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var match = Regex.Match(json, "\"sub\"\\s*:\\s*\"(?<id>\\d+)\"");
                return match.Success ? long.Parse(match.Groups["id"].Value) : 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// 메모리 세션과 저장된 토큰을 모두 제거한다.
        /// </summary>
        public void Clear()
        {
            AccessToken = null;
            RefreshToken = null;
            ExpiresAt = 0;
            TokenStorage.Clear();
        }
    }
}
