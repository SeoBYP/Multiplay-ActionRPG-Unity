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
    /// 던전 결과(클리어/실패) 릴레이 단위 테스트 (EditMode).
    ///   - S_DungeonClear(보상 Exp) 신호 → State.IsDungeonCleared + RewardExp
    ///   - S_DungeonFailed 신호 → State.IsDungeonFailed
    ///   - 사망 감지는 **서버 권위**(authority-model §4) → 클라는 C_PlayerDead 를 보내지 않는다.
    /// </summary>
    [TestFixture]
    public class InGameDungeonResultRelayTests
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
        public void 클리어_신호시_State가_클리어이고_RewardExp가_반영된다()
        {
            var packetState = new SocketPacketState();
            _model = new InGameModel(new RecordingSocketSession(), new LocalPlayerContext(), null, null, packetState);
            _model.Initialize();

            Assert.IsFalse(_model.State.CurrentValue.IsDungeonCleared);

            packetState.MarkDungeonCleared(100);

            Assert.IsTrue(_model.State.CurrentValue.IsDungeonCleared);
            Assert.AreEqual(100, _model.State.CurrentValue.RewardExp);
        }

        [Test]
        public void 실패_신호시_State_IsDungeonFailed가_true가_된다()
        {
            var packetState = new SocketPacketState();
            _model = new InGameModel(new RecordingSocketSession(), new LocalPlayerContext(), null, null, packetState);
            _model.Initialize();

            Assert.IsFalse(_model.State.CurrentValue.IsDungeonFailed);

            packetState.MarkDungeonFailed();

            Assert.IsTrue(_model.State.CurrentValue.IsDungeonFailed);
        }

        [Test]
        public void 로컬_HP가_0이_되어도_C_PlayerDead를_송신하지_않는다()
        {
            // 플레이어 HP 서버 권위 승격(§4): 사망은 서버(Room.TickMonsters)가 자기 HP 로 직접 감지.
            // 클라는 더 이상 C_PlayerDead 를 보고하지 않는다(보내면 서버가 dedup, 보낼 필요 없음).
            var session = new RecordingSocketSession();
            var localPlayer = new LocalPlayerContext();
            _model = new InGameModel(session, localPlayer, null, null, new SocketPacketState());
            _model.Initialize();

            var asc = CreateAsc(100, 100);
            localPlayer.Set(asc);

            ApplyDamage(asc, EGameplayAttribute.Health, -100); // HP 0

            Assert.IsEmpty(session.SentPackets.FindAll(p => p is C_PlayerDead));
        }

        // ── 헬퍼 ────────────────────────────────────────

        private AbilitySystemComponent CreateAsc(int hp, int maxHp)
        {
            var go = new GameObject("LocalPlayer");
            _objects.Add(go);
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.Attributes = new List<GameplayAttribute>
            {
                new(EGameplayAttribute.Health, hp, maxHp),
                new(EGameplayAttribute.Mana, 0, 0),
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

        private sealed class RecordingSocketSession : ISocketSession
        {
            public readonly List<Packet> SentPackets = new();
            public SocketSessionState State => default;
            public event global::System.Action OnDisconnected { add { } remove { } }
            public UniTask ConnectAsync(SocketConnectionInfo connectionInfo, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask JoinRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask LeaveRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendMoveAsync(C_Move packet, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendAsync(Packet packet, CancellationToken ct) { SentPackets.Add(packet); return UniTask.CompletedTask; }
            public UniTask DisconnectAsync(CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
