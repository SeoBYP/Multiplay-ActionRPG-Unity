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
    /// GAS Attribute → InGameModel → InGameState 스탯 릴레이 단위 테스트 (EditMode).
    ///
    /// 검증 포인트:
    ///   - LocalPlayerContext.Set 시 현재 HP/MP가 State에 즉시 반영
    ///   - GameplayEffect로 Attribute가 변하면 State가 갱신
    ///
    /// prefab/렌더는 PlayMode(GameHudIntegrationTests)에서 검증한다. 여기선 순수 데이터 흐름만 본다.
    /// </summary>
    [TestFixture]
    public class InGameStatusRelayTests
    {
        private readonly List<GameObject> _objects = new();
        private InGameModel _model;

        [TearDown]
        public void TearDown()
        {
            _model?.Dispose();
            _model = null;

            foreach (var obj in _objects)
                if (obj != null)
                    Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        [Test]
        public void 로컬_바인딩시_현재_HP_MP가_State에_반영된다()
        {
            var localPlayer = BuildModel();
            var asc = CreateAsc(80, 100, 30, 60);

            localPlayer.Set(asc);

            var state = _model.State.CurrentValue;
            Assert.AreEqual(80, state.Hp);
            Assert.AreEqual(100, state.MaxHp);
            Assert.AreEqual(30, state.Mp);
            Assert.AreEqual(60, state.MaxMp);
        }

        [Test]
        public void Health_감소_효과시_State_Hp가_갱신된다()
        {
            var localPlayer = BuildModel();
            var asc = CreateAsc(100, 100, 50, 50);
            localPlayer.Set(asc);

            ApplyDamage(asc, EGameplayAttribute.Health, -30);

            Assert.AreEqual(70, _model.State.CurrentValue.Hp);
        }

        [Test]
        public void Mana_변경시_State_Mp가_갱신된다()
        {
            var localPlayer = BuildModel();
            var asc = CreateAsc(100, 100, 50, 80);
            localPlayer.Set(asc);

            ApplyDamage(asc, EGameplayAttribute.Mana, -20);

            Assert.AreEqual(30, _model.State.CurrentValue.Mp);
        }

        [Test]
        public void 바인딩_전에는_State가_초기값이다()
        {
            BuildModel();
            Assert.AreEqual(0, _model.State.CurrentValue.Hp);
            Assert.AreEqual(0, _model.State.CurrentValue.MaxHp);
        }

        [Test]
        public void 전원입장_신호시_State_IsDungeonReady가_true가_된다()
        {
            var packetState = new SocketPacketState();
            _model = new InGameModel(new FakeSocketSession(), new LocalPlayerContext(),
                null, null, packetState);
            _model.Initialize();

            Assert.IsFalse(_model.State.CurrentValue.IsDungeonReady, "초기엔 준비 전");

            // 서버 S_GameStatus(InProgress) → GameStatusPacketHandler → MarkDungeonReady 와 동일 경로
            packetState.MarkDungeonReady();

            Assert.IsTrue(_model.State.CurrentValue.IsDungeonReady, "전원 입장 신호 후 준비 완료");
        }

        // ── 헬퍼 ────────────────────────────────────────

        private LocalPlayerContext BuildModel()
        {
            var localPlayer = new LocalPlayerContext();
            _model = new InGameModel(new FakeSocketSession(), localPlayer);
            _model.Initialize();
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

        private sealed class FakeSocketSession : ISocketSession
        {
            public SocketSessionState State => default;
            public UniTask ConnectAsync(SocketConnectionInfo connectionInfo, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask JoinRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask LeaveRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendMoveAsync(C_Move packet, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendAsync(Packet packet, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask DisconnectAsync(CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
