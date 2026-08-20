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
    /// GameHud.prefab 동적 생성 → DI 주입 → GAS 스탯 렌더링 통합 테스트 (PlayMode).
    ///
    /// 검증 범위:
    ///   GameHudController가 Addressable로 GameHud.prefab을 동적 로드·생성하고,
    ///   주입된 InGameModel이 로컬 ASC의 Attribute 변화를 받아 HP/MP 게이지(SliderBall)에 반영하는지.
    ///
    /// EditMode(InGameStatusRelayTests)는 순수 데이터 흐름만, 여기선 실제 prefab + MonoBehaviour 생명주기까지 본다.
    /// </summary>
    [TestFixture]
    public class GameHudIntegrationTests
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

            foreach (var obj in _objects)
                if (obj != null)
                    Object.Destroy(obj);
            _objects.Clear();
        }

        [UnityTest]
        public IEnumerator GameHud_동적생성_후_로컬_HP_MP가_게이지에_반영된다()
        {
            var localPlayer = BuildContainer();
            var model = _resolver.Resolve<InGameModel>();
            model.Initialize();

            var asc = CreateAsc(80, 100, 30, 60);
            localPlayer.Set(asc);

            // GameHud.prefab 동적 생성 (GameHudController가 실제 사용하는 경로)
            _controller = new GameHudController(_resolver);
            yield return _controller.StartAsync(CancellationToken.None).ToCoroutine();
            yield return null; // GameHud.Start → State 구독 → 초기 렌더

            var hud = Object.FindObjectOfType<GameHud>();
            Assert.IsNotNull(hud, "GameHud가 동적 생성되지 않았다.");

            var hp = FindSlider(hud, "HP_Ball");
            var mp = FindSlider(hud, "MP_Ball");
            Assert.IsNotNull(hp, "HP_Ball SliderBall을 찾지 못했다 (prefab 배선 확인).");
            Assert.IsNotNull(mp, "MP_Ball SliderBall을 찾지 못했다 (prefab 배선 확인).");

            Assert.AreEqual(0.8f, hp.FillAmount, 0.01f, "초기 HP 게이지 채움량이 80/100과 다르다.");
            Assert.AreEqual(0.5f, mp.FillAmount, 0.01f, "초기 MP 게이지 채움량이 30/60과 다르다.");

            // 데미지 적용 → 게이지 실시간 갱신 확인
            ApplyDamage(asc, EGameplayAttribute.Health, -40);
            yield return null;

            Assert.AreEqual(0.4f, hp.FillAmount, 0.01f, "데미지 후 HP 게이지가 40/100으로 갱신되지 않았다.");
        }

        // ── 헬퍼 ────────────────────────────────────────

        private LocalPlayerContext BuildContainer()
        {
            var localPlayer = new LocalPlayerContext();
            var builder = new ContainerBuilder();
            builder.RegisterInstance(localPlayer);
            builder.RegisterInstance(new FakeSocketSession()).As<ISocketSession>();
            builder.RegisterInstance<ISocketPacketState>(new SocketPacketState());
            builder.RegisterInstance(new GameplayEffectCatalog());
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

        private AbilitySystemComponent CreateAsc(int hp, int maxHp, int mp, int maxMp)
        {
            var go = new GameObject("LocalPlayer");
            _objects.Add(go);

            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.Attributes = new List<GameplayAttribute>
            {
                new(EGameplayAttribute.Health, hp, maxHp),
                new(EGameplayAttribute.Mana, mp, maxMp),
            };
            asc.InitializeAttributes();
            return asc;
        }

        private static void ApplyDamage(AbilitySystemComponent asc, EGameplayAttribute type, int amount)
        {
            var effect = new GameplayEffect(new List<GameplayAttributeModifier>
            {
                GameplayAttributeModifier.Create(type, amount, EModifierType.Additive),
            });
            AbilitySystemUtils.ApplyEffect(asc, effect);
        }

        private static SliderBall FindSlider(GameHud hud, string objectName)
        {
            foreach (var slider in hud.GetComponentsInChildren<SliderBall>(true))
                if (slider.gameObject.name == objectName)
                    return slider;
            return null;
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
