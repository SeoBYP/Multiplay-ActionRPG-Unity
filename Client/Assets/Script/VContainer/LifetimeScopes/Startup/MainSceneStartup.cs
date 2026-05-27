using System.Threading;
using Cysharp.Threading.Tasks;
using Script.System.Auth;
using Script.System.Startup;
using UnityEngine;
using VContainer.Unity;

namespace Game.Installers.Scenes.Startup
{
    /// <summary>
    /// Main 씬 진입 시 인증 완료를 보장한다.
    ///
    /// IAsyncStartable.StartAsync()는 IInitializable.Initialize()가 모두 끝난 뒤
    /// VContainer가 호출하므로, 여기서 await하면 이후 씬 로직이 인증 완료 후 실행된다.
    ///
    /// - 빌드: TitleScene에서 로그인 후 씬 전환 → AuthSession.Update()가 이미 호출됨 → 즉시 통과
    /// - 에디터: EditorAutoLoginInitializer(ProjectLifetimeScope)가 완료될 때까지 대기
    /// </summary>
    public class MainSceneStartup : IAsyncStartable
    {
        private readonly IAuthService       _authService;
        private readonly UserProfile        _userProfile;
        private readonly StartupIntentQueue _startupQueue;

        public MainSceneStartup(
            IAuthService       authService,
            UserProfile        userProfile,
            StartupIntentQueue startupQueue)
        {
            _authService  = authService;
            _userProfile  = userProfile;
            _startupQueue = startupQueue;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            await _authService.AuthenticatedAsync().AttachExternalCancellation(ct);

            Debug.Log("[MainSceneStartup] 인증 확인 완료 — 씬 초기화 진행");
            Debug.Log($"[MainSceneStartup] UserProfile — PublicId={_userProfile.PublicId} NickName={_userProfile.NickName} HasProfile={_userProfile.HasProfile}");
            Debug.Log($"[MainSceneStartup] StartupIntentQueue — HasPending={_startupQueue.HasPending}");
        }
    }
}
