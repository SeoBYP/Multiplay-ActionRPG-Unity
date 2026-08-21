using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.GUI.OutGame;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using Game.Presentation.InGame;
using Game.System.Player;
using Game.System.Progression;
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
            builder.RegisterInstance<ISocketPacketState>(new SocketPacketState());
            builder.RegisterInstance(catalog);
            builder.RegisterInstance(ScriptableObject.CreateInstance<EffectIconCatalog>());
            // InGameModel ctor의 PlayerProgressionHolder는 C# 기본값이지만 VContainer는 기본값을 무시하므로
            // 명시 등록이 필요하다. Exp 게이지는 이 테스트 관심사가 아니라 미갱신(default) 홀더로 충분.
            builder.RegisterInstance(new PlayerProgressionHolder(new FakeProgressionService()));
            // InGameModel ctor의 IInputContext(C# 기본값이지만 VContainer가 무시) — no-op 으로 충족.
            builder.RegisterInstance<Game.System.Input.IInputContext>(new NoopInputContext());
            // 아이템 획득 토스트 의존(2026-07-12 추가). 역시 C# 기본값이지만 VContainer 가 무시하므로 등록 필요.
            builder.RegisterInstance(ScriptableObject.CreateInstance<Game.Presentation.Inventory.ItemDisplayCatalog>());
            builder.RegisterInstance(new Game.System.Player.ItemPickupNotifier());
            builder.RegisterInstance(new Game.System.Player.InteractionPromptNotifier()); // 상호작용 안내 채널
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

        private sealed class NoopInputContext : Game.System.Input.IInputContext
        {
            public void EnterUi() { }
            public void ExitUi() { }
            public bool IsUiActive => false;
        }

        private sealed class FakeSocketSession : ISocketSession
        {
            public SocketSessionState State => default;
            public string LastJoinFailureReason => null;
            public event global::System.Action OnDisconnected { add { } remove { } }
            public UniTask ConnectAsync(SocketConnectionInfo connectionInfo, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask JoinRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask LeaveRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendMoveAsync(C_Move packet, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendAsync(Packet packet, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask DisconnectAsync(CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class FakeProgressionService : IProgressionService
        {
            public UniTask<(ProgressionResult Result, ProgressionData Data)> GetProgressionAsync(CancellationToken ct = default)
                => UniTask.FromResult((ProgressionResult.Success, default(ProgressionData)));
        }
    }
}
