#if UNITY_EDITOR
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.System.Auth;
using Unity.Multiplayer.PlayMode;
using UnityEngine;
using VContainer.Unity;
using SystemInfo = UnityEngine.SystemInfo;

namespace Game.System.Auth
{
    /// <summary>
    /// 에디터에서 Title 씬을 거치지 않고 씬을 직접 실행할 때
    /// 자동으로 게스트 계정으로 로그인한다.
    ///
    /// ProjectLifetimeScope에 등록되어 전역에서 1회만 실행된다.
    /// 로그인 실패 시 LogError 후 Play 모드를 즉시 종료한다.
    ///
    /// MultiplayerPlayMode 가상 플레이어는 PlayerTag(Player_1, Player_2 등)로
    /// 계정을 분리한다. 같은 머신에서 여러 인스턴스를 실행해도 계정이 충돌하지 않는다.
    ///
    /// 빌드에는 포함되지 않는다(#if UNITY_EDITOR).
    /// </summary>
    public class EditorAutoLoginInitializer : IAsyncStartable
    {
        private readonly IAuthService _authService;

        public EditorAutoLoginInitializer(IAuthService authService)
        {
            _authService = authService;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            if (_authService.IsAuthenticated) return;

            var email    = BuildGuestEmail();
            const string password = "EditorGuest2024!";

            Debug.Log($"[EditorAutoLogin] 게스트 로그인 시도: {email}");

            try
            {
                var result = await _authService.LoginOrRegisterAsync(email, password, ct);

                if (result == AuthResult.Success)
                {
                    Debug.Log("[EditorAutoLogin] 게스트 로그인 성공");
                    return;
                }

                Debug.LogError($"[EditorAutoLogin] 로그인 실패 ({result}) — Play 모드를 종료합니다.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EditorAutoLogin] 예외 발생 — 서버가 실행 중인지 확인하세요.\n{ex.GetType().Name}: {ex.Message}");
            }

            UnityEditor.EditorApplication.isPlaying = false;
        }

        private static string BuildGuestEmail()
        {
            var hash = Mathf.Abs(SystemInfo.deviceUniqueIdentifier.GetHashCode()).ToString("x8");

            var tags = CurrentPlayer.Tags;
            if (tags is { Count: > 0 })
            {
                // MultiplayerPlayMode 가상 플레이어: PlayerTag로 계정 구분
                var tag = tags[0].ToLower().Replace(" ", "_");
                return $"guest_{hash}_{tag}@editor.test";
            }

            // 메인 에디터: 기기 해시만 사용
            return $"guest_{hash}@editor.test";
        }
    }
}
#endif
