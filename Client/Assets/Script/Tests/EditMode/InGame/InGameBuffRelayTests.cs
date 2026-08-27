using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using Game.Presentation.InGame;
using Game.System.Player;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.InGame
{
    /// <summary>
    /// ASC.ActiveEffects → InGameModel → InGameState.Buffs(BuffView) 변환 검증 (EditMode).
    /// 아이콘은 카테고리로 매칭, 색은 polarity(버프/디버프)로 결정되는지 확인.
    /// </summary>
    public class InGameBuffRelayTests
    {
        private readonly List<GameObject> _objects = new();
        private InGameModel _model;
        private EffectIconCatalog _icons;

        [TearDown]
        public void TearDown()
        {
            _model?.Dispose();
            _model = null;
            if (_icons != null) { Object.DestroyImmediate(_icons); _icons = null; }
            foreach (var o in _objects)
                if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        [Test]
        public void 버프_적용시_BuffView가_State에_반영된다()
        {
            var catalog = new GameplayEffectCatalog();
            var atkSprite = MakeSprite();
            var localPlayer = BuildModel(catalog, (EEffectCategory.AttackPower, atkSprite));
            var asc = CreateAsc();
            localPlayer.Set(asc);

            asc.ApplyEffect(catalog.Get("atk_up_20")); // AttackPower ×120, Duration 10s

            var buffs = _model.State.CurrentValue.Buffs;
            Assert.AreEqual(1, buffs.Count);
            Assert.AreSame(atkSprite, buffs[0].Icon, "카테고리(AttackPower) 아이콘이 매칭돼야 한다.");
            Assert.IsFalse(buffs[0].IsInfinite);
            Assert.AreEqual(10f, buffs[0].TotalSeconds, 0.01f);
            Assert.AreEqual(_icons.GetColor(true), buffs[0].Tint, "공격력 증가는 버프 색이어야 한다.");
        }

        [Test]
        public void 디버프는_debuff_색으로_변환된다()
        {
            var catalog = new GameplayEffectCatalog();
            var localPlayer = BuildModel(catalog, (EEffectCategory.Defense, MakeSprite()));
            var asc = CreateAsc();
            localPlayer.Set(asc);

            asc.ApplyEffect(catalog.Get("def_down_10")); // Defense -10

            var buffs = _model.State.CurrentValue.Buffs;
            Assert.AreEqual(1, buffs.Count);
            Assert.AreEqual(_icons.GetColor(false), buffs[0].Tint, "방어력 감소는 디버프 색이어야 한다.");
        }

        // ── 헬퍼 ────────────────────────────────────────

        private LocalPlayerContext BuildModel(GameplayEffectCatalog catalog, params (EEffectCategory cat, Sprite sprite)[] icons)
        {
            _icons = ScriptableObject.CreateInstance<EffectIconCatalog>();
            foreach (var (cat, sprite) in icons)
                _icons.RegisterIcon(cat, sprite);

            var localPlayer = new LocalPlayerContext();
            _model = new InGameModel(new FakeSocketSession(), localPlayer, catalog, _icons);
            _model.Initialize();
            return localPlayer;
        }

        private GasComponent CreateAsc()
        {
            var go = new GameObject("LocalPlayer");
            _objects.Add(go);
            var asc = go.AddComponent<GasComponent>();
            asc.Attributes = new List<GameplayAttribute>
            {
                new(EGameplayAttribute.Health, 100, 100),
                new(EGameplayAttribute.AttackPower, 50, 100000, EAttributeKind.Stat),
                new(EGameplayAttribute.Defense, 30, 100000, EAttributeKind.Stat),
            };
            asc.InitializeAttributes();
            return asc;
        }

        private static Sprite MakeSprite()
        {
            return Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
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
    }
}
