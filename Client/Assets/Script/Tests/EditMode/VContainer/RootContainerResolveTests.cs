using System;
using System.Collections.Generic;
using System.Linq;
using Game.Installers;
using Game.Gameplay.Input;
using NUnit.Framework;
using UnityEngine;
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
                // 루트에 PlayerInputActions 가 있고, 생성된 Dispose() 는 Object.Destroy(asset) 를 부른다.
                // EditMode 에선 그게 "Destroy may not be called from edit mode" 에러를 콘솔에 남긴다.
                // 생성 파일이라 못 고치니, 컨테이너가 해제되기 전에 우리가 DestroyImmediate 로 먼저 치운다.
                // (로그를 무시하는 방식은 테스트만 통과시키고 콘솔 에러는 그대로 남겨 둔다 — 그래서 바꿨다.)
                var actions = container.Resolve<PlayerInputActions>();
                actions.Disable();
                if (actions.asset != null)
                    Object.DestroyImmediate(actions.asset);

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
