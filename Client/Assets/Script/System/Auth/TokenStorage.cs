using UnityEngine;

namespace Game.System.Auth
{
    /// <summary>
    /// PlayerPrefs 기반 인증 토큰 저장소.
    /// 앱 재시작 후 자동 로그인을 위해 access/refresh token과 만료 시각을 저장한다.
    /// </summary>
    public static class TokenStorage
    {
        private const string AuthAccessTokenKey = "auth_access";
        private const string AuthRefreshTokenKey = "auth_refresh";
        private const string AuthExpiresAtKey = "auth_expires_at";

        /// <summary>
        /// 현재 토큰 세트를 영속 저장소에 저장한다.
        /// </summary>
        public static void Save(string accessToken, string refreshToken, long expiresAt)
        {
            PlayerPrefs.SetString(AuthAccessTokenKey, accessToken);
            PlayerPrefs.SetString(AuthRefreshTokenKey, refreshToken);
            PlayerPrefs.SetString(AuthExpiresAtKey, expiresAt.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 저장된 토큰을 읽어 온다.
        /// 필수 값이 비어 있거나 만료 시각 파싱이 실패하면 false를 반환한다.
        /// </summary>
        public static bool TryLoad(out string accessToken, out string refreshToken, out long expiresAt)
        {
            accessToken = PlayerPrefs.GetString(AuthAccessTokenKey, string.Empty);
            refreshToken = PlayerPrefs.GetString(AuthRefreshTokenKey, string.Empty);
            if (!long.TryParse(PlayerPrefs.GetString(AuthExpiresAtKey, "0"), out expiresAt))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(refreshToken);
        }

        /// <summary>
        /// 저장된 인증 정보를 모두 삭제한다.
        /// </summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(AuthAccessTokenKey);
            PlayerPrefs.DeleteKey(AuthRefreshTokenKey);
            PlayerPrefs.DeleteKey(AuthExpiresAtKey);
            PlayerPrefs.Save();
        }
    }
}
