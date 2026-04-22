using System.Threading;
using Cysharp.Threading.Tasks;
using Script.System.Auth;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 타이틀 씬 진입 직후 자동 로그인 시도를 시작하는 초기화 엔트리포인트.
    /// </summary>
    public class TitleSceneInitializer : IInitializable
    {
        [Inject] private IAuthService _authService;

        /// <summary>
        /// 씬 초기화 시점에 저장된 토큰 기반 자동 로그인을 시도한다.
        /// 결과에 따른 UI 전환은 별도 타이틀 UI 흐름에서 처리한다.
        /// </summary>
        public void Initialize()
        {
            // Title 시작 직후 자동 로그인 비동기 흐름을 시작한다.
            var res = _authService.TryAutoLoginAsync(CancellationToken.None);
        }
    }
}
