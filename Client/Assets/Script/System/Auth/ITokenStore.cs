using UnityEngine;

namespace Game.System.Auth
{
    /// <summary>
    /// 인증 토큰의 영속 저장소.
    /// </summary>
    /// <remarks>
    /// 인터페이스로 뺀 이유는 딱 하나 — <b>테스트가 실제로 교체</b>한다.
    /// 이전엔 <c>PlayerPrefs</c> 를 감싼 정적 클래스 하나를 프로덕션과 E2E 픽스처가 함께 썼다.
    /// 프로세스 전역이라 다른 곳(에디터 자동 로그인·keep-alive)이 같은 키를 덮어쓸 수 있었고,
    /// `AuthFlowE2ETests` 가 전체 실행에서만 흔들리는 원인 후보가 됐다.
    /// 세션마다 저장소를 들고 있게 하면 그 간섭 경로 자체가 사라진다.
    /// </remarks>
    public interface ITokenStore
    {
        void Save(string accessToken, string refreshToken, long expiresAt);
        bool TryLoad(out string accessToken, out string refreshToken, out long expiresAt);
        void Clear();
    }

    /// <summary>
    /// 앱이 실제로 쓰는 저장소. 앱 재시작 후 자동 로그인을 위해 PlayerPrefs 에 남긴다.
    /// </summary>
    public sealed class PlayerPrefsTokenStore : ITokenStore
    {
        private const string AuthAccessTokenKey = "auth_access";
        private const string AuthRefreshTokenKey = "auth_refresh";
        private const string AuthExpiresAtKey = "auth_expires_at";

        public void Save(string accessToken, string refreshToken, long expiresAt)
        {
            PlayerPrefs.SetString(AuthAccessTokenKey, accessToken);
            PlayerPrefs.SetString(AuthRefreshTokenKey, refreshToken);
            PlayerPrefs.SetString(AuthExpiresAtKey, expiresAt.ToString());
            PlayerPrefs.Save();
        }

        public bool TryLoad(out string accessToken, out string refreshToken, out long expiresAt)
        {
            accessToken = PlayerPrefs.GetString(AuthAccessTokenKey, string.Empty);
            refreshToken = PlayerPrefs.GetString(AuthRefreshTokenKey, string.Empty);
            if (!long.TryParse(PlayerPrefs.GetString(AuthExpiresAtKey, "0"), out expiresAt))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(refreshToken);
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(AuthAccessTokenKey);
            PlayerPrefs.DeleteKey(AuthRefreshTokenKey);
            PlayerPrefs.DeleteKey(AuthExpiresAtKey);
            PlayerPrefs.Save();
        }
    }
}
