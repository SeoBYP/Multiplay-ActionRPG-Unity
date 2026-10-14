using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Installers;
using NUnit.Framework;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.EditMode.VContainerWiring
{
    /// <summary>
    /// **루트 DI 배선이 실제로 해석되는지** 확인한다.
    ///
    /// 왜 필요한가: 2026-08-26 에 `SessionKeepAlive` 가 런타임에서만 터졌다 —
    /// `No such registration of type: System.Double`. 생성자에 C# 선택 인자(`double x = 0.6`)가
    /// 있었는데 **VContainer 는 기본값을 존중하지 않고 모든 인자를 해석하려 한다.**
    ///
    /// 컴파일도 통과하고 단위 테스트도 통과했다. 그 테스트들이 객체를 `new` 로 직접 만들었기 때문이다.
    /// 즉 **컨테이너를 실제로 빌드해 보지 않으면 잡을 수 없는 부류**다. 이 테스트가 그 자리를 메운다.
    ///
    /// MonoBehaviour/씬 의존이 있는 등록은 여기서 다루지 않는다 — 목적은 "생성자 인자를 해석할 수 있는가"다.
    /// **EditMode 에 두는 이유**: 순수 DI 배선 검사라 씬·플레이모드가 필요 없고,
    /// PlayMode 에서 루트 컨테이너를 세우면 NetworkInstaller 등이 실제 자원을 만들어 에디터를 불안정하게 만든다.
    /// </summary>
    public class RootContainerResolveTests
    {
        /// <summary>
        /// 루트 컨테이너를 세워 검증한 뒤 반드시 해제한다.
        ///
        /// 등록은 <see cref="ProjectLifetimeScope.InstallRoot"/> 를 **직접 호출**한다 — 목록을 베끼지 않는다.
        /// 베낀 사본은 조용히 어긋난다: 실제로 인스톨러 줄만 복사한 탓에
        /// 루트가 인스톨러 밖에서 등록하는 `IGameSceneManager` 가 빠져
        /// 여기서만 `GameSessionConnector` 해석이 실패했다. 그건 배선 결함이 아니라 테스트 결함이었다.
        /// </summary>
        private static void WithRootContainer(Action<IObjectResolver> assert)
        {
            var builder = new ContainerBuilder();
            ProjectLifetimeScope.InstallRoot(builder);
            var container = builder.Build();
            try
            {
                assert(container);
            }
            finally
            {
                // 루트의 PlayerInputActions 를 생성된 Dispose() 가 Object.Destroy(asset) 로 치우는데,
                // EditMode 에선 그게 에러 로그다. 생성 파일이라 고칠 수 없고,
                // asset 을 미리 DestroyImmediate 해도 Destroy 는 **이미 파괴된 객체에도** 같은 에러를 낸다(실측).
                // 즉 이 로그는 피할 수 없다. 그래서 무시(ignoreFailingMessages)가 아니라 "반드시 난다"고 선언한다 —
                // 무시는 로그가 사라져도 모르지만, Expect 는 안 나면 테스트를 빨갛게 만들어 알려준다.
                LogAssert.Expect(UnityEngine.LogType.Error, new Regex("Destroy may not be called from edit mode"));
                container.Dispose();
            }
        }

        [Test]
        public void 루트_인스톨러의_엔트리포인트를_전부_해석할_수_있다()
        {
            // 엔트리포인트 해석이 곧 "모든 생성자 인자가 등록돼 있는가" 검사다.
            // 하나라도 해석 못 하면 런타임 첫 씬에서 루트 컨테이너가 통째로 죽는다.
            WithRootContainer(container =>
            {
                var startables = container.Resolve<IReadOnlyList<IAsyncStartable>>();
                CollectionAssert.IsNotEmpty(startables);
            });
        }

        [Test]
        public void SessionKeepAlive_는_선택인자_없이_해석된다()
        {
            // VContainer 는 C# 기본값을 채워주지 않는다 — [Inject] 생성자가 튜닝 인자를 받지 않아야 한다.
            // RegisterEntryPoint 는 구현 타입으로 등록하지 않으므로 구체 타입 Resolve 가 아니라
            // 엔트리포인트 목록에서 찾는다. 목록 해석 자체가 곧 생성자 해석이다.
            WithRootContainer(container =>
            {
                var startables = container.Resolve<IReadOnlyList<IAsyncStartable>>();
                Assert.IsTrue(startables.Any(s => s is Game.System.Auth.SessionKeepAlive),
                    "SessionKeepAlive 가 엔트리포인트로 해석되지 않았다");
            });
        }
    }
}
