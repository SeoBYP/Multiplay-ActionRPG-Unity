#if UNITY_EDITOR
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Script.System.Auth;
using UnityEngine;
using VContainer.Unity;
using SystemInfo = UnityEngine.SystemInfo;

namespace Game.System.AuthSystem
{
    /// <summary>
    /// 에디터에서 Title 씬을 거치지 않고 씬을 직접 실행할 때
    /// 자동으로 게스트 계정으로 로그인한다.
    ///
    /// ProjectLifetimeScope에 등록되어 전역에서 1회만 실행된다.
    /// 로그인 실패 시 LogError 후 Play 모드를 즉시 종료한다.
    ///
    /// 기기별 고유 해시(deviceUniqueIdentifier)로 계정을 분리하므로
    /// 여러 개발자가 같은 서버를 공유해도 계정이 충돌하지 않는다.
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

            var hash     = Mathf.Abs(SystemInfo.deviceUniqueIdentifier.GetHashCode()).ToString("x8");
            var email    = $"guest_{hash}@editor.test";
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
    }
}
#endif
