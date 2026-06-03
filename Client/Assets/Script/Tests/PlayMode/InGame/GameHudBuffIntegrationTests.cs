using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.GUI.OutGame;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using Game.Presentation.InGame;
using Game.System.Player;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// 버프 적용 → GameHud(동적 생성) 버프 슬롯 렌더 통합 테스트 (PlayMode).
    /// EditMode(가역성·만료)와 달리 실제 prefab + 슬롯 풀 + MonoBehaviour 생명주기까지 본다.
    /// </summary>
    [TestFixture]
    public class GameHudBuffIntegrationTests
    {
        private IObjectResolver _resolver;
        private GameHudController _controller;
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            _controller?.Dispose();
            _controller = null;
            _resolver?.Dispose();
            _resolver = null;
            foreach (var o in _objects)
                if (o != null) Object.Destroy(o);
            _objects.Clear();
        }

        [UnityTest]
        public IEnumerator 버프_적용시_GameHud에_버프슬롯이_렌더된다()
        {
            var catalog = new GameplayEffectCatalog();
            var localPlayer = BuildContainer(catalog);
            var model = _resolver.Resolve<InGameModel>();
            model.Initialize();

            var asc = CreateAsc();
            localPlayer.Set(asc);

            _controller = new GameHudController(_resolver);
            yield return _controller.StartAsync(CancellationToken.None).ToCoroutine();
            yield return null; // Start → InitBuffPool + 초기 렌더(버프 0)

            var hud = Object.FindObjectOfType<GameHud>();
            Assert.IsNotNull(hud, "GameHud가 동적 생성되지 않았다.");
            Assert.AreEqual(0, ActiveSlotCount(hud), "버프 적용 전에는 활성 슬롯이 없어야 한다.");

            // 버프 적용 → 슬롯 1개 활성
            asc.ApplyEffect(catalog.Get("atk_up_20"));
            yield return null;

            Assert.AreEqual(1, ActiveSlotCount(hud), "버프 적용 후 활성 슬롯이 1개여야 한다.");
        }

        // ── 헬퍼 ────────────────────────────────────────

        private LocalPlayerContext BuildContainer(GameplayEffectCatalog catalog)
        {
            var localPlayer = new LocalPlayerContext();
            var builder = new ContainerBuilder();
            builder.RegisterInstance(localPlayer);
            builder.RegisterInstance(new FakeSocketSession()).As<ISocketSession>();
            builder.RegisterInstance(catalog);
            builder.RegisterInstance(ScriptableObject.CreateInstance<EffectIconCatalog>());
            builder.Register<InGameModel>(Lifetime.Singleton).AsSelf();
            _resolver = builder.Build();
            return localPlayer;
        }

        private AbilitySystemComponent CreateAsc()
        {
            var go = new GameObject("LocalPlayer");
            _objects.Add(go);
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.Attributes = new List<GameplayAttribute>
            {
                new(EGameplayAttribute.Health, 100, 100),
                new(EGameplayAttribute.AttackPower, 50, 100000, EAttributeKind.Stat),
            };
            asc.InitializeAttributes();
            return asc;
        }

        private static int ActiveSlotCount(GameHud hud)
        {
            int count = 0;
            foreach (var slot in hud.GetComponentsInChildren<BattleEffectSlot>(true))
                if (slot.IsActive)
                    count++;
            return count;
        }

        private sealed class FakeSocketSession : ISocketSession
        {
            public SocketSessionState State => default;
            public UniTask ConnectAsync(SocketConnectionInfo connectionInfo, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask JoinRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask LeaveRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendMoveAsync(C_Move packet, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask DisconnectAsync(CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
