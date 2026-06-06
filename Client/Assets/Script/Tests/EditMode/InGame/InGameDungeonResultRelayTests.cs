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
    /// 던전 결과(클리어/실패) 릴레이 + 로컬 사망 보고 단위 테스트 (EditMode).
    ///   - S_DungeonClear(보상 Exp) 신호 → State.IsDungeonCleared + RewardExp
    ///   - S_DungeonFailed 신호 → State.IsDungeonFailed
    ///   - 로컬 HP 0 → C_PlayerDead 1회 송신(플레이어 HP=클라 권위)
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
        public void 로컬_HP가_0이_되면_C_PlayerDead를_한_번만_송신한다()
        {
            var session = new RecordingSocketSession();
            var localPlayer = new LocalPlayerContext();
            _model = new InGameModel(session, localPlayer, null, null, new SocketPacketState());
            _model.Initialize();

            var asc = CreateAsc(100, 100);
            localPlayer.Set(asc);

            ApplyDamage(asc, EGameplayAttribute.Health, -100); // HP 0 → 사망 보고
            ApplyDamage(asc, EGameplayAttribute.Health, -10);  // 추가 데미지 — 중복 송신 없어야

            Assert.AreEqual(1, session.SentPackets.FindAll(p => p is C_PlayerDead).Count);
        }

        [Test]
        public void HP가_0보다_크면_C_PlayerDead를_송신하지_않는다()
        {
            var session = new RecordingSocketSession();
            var localPlayer = new LocalPlayerContext();
            _model = new InGameModel(session, localPlayer, null, null, new SocketPacketState());
            _model.Initialize();

            var asc = CreateAsc(100, 100);
            localPlayer.Set(asc);

            ApplyDamage(asc, EGameplayAttribute.Health, -30);

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
            public UniTask ConnectAsync(SocketConnectionInfo connectionInfo, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask JoinRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask LeaveRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendMoveAsync(C_Move packet, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendAsync(Packet packet, CancellationToken ct) { SentPackets.Add(packet); return UniTask.CompletedTask; }
            public UniTask DisconnectAsync(CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
