using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Game.Network.Socket;
using Game.Presentation.InGame;
using Game.System.Auth;
using Game.System.Player;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.InGame
{
    /// <summary>
    /// EF-2d 클라 수신 경로: ISocketPacketState effect 이벤트 → EffectReceiver → 대상 ASC 적용/제거.
    /// 타겟 라우팅(내 UserId만 로컬 적용) 검증.
    /// </summary>
    public class EffectReceiverTests
    {
        private const long LocalUserId = 100;
        private readonly List<GameObject> _objects = new();
        private EffectReceiver _receiver;

        [TearDown]
        public void TearDown()
        {
            _receiver?.Dispose();
            _receiver = null;
            foreach (var o in _objects)
                if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        [Test]
        public void 내_대상_Effect_수신시_로컬_ASC에_서버_InstanceId로_적용된다()
        {
            var state = new SocketPacketState();
            var asc = CreateAsc();
            var localPlayer = new LocalPlayerContext();
            localPlayer.Set(asc);
            _receiver = Build(state, localPlayer);

            state.ApplyEffect(new SocketEffectApply("def_down_10", instanceId: 42, targetId: LocalUserId, sourceId: 200, startTick: 0, stacks: 1));

            Assert.AreEqual(1, asc.ActiveEffects.Count);
            Assert.AreEqual(42, asc.ActiveEffects[0].InstanceId, "서버 InstanceId를 키로 적용해야 한다.");

            state.RemoveEffect(42);
            Assert.AreEqual(0, asc.ActiveEffects.Count, "서버 InstanceId로 제거되어야 한다.");
        }

        [Test]
        public void 다른_플레이어_대상_Effect는_로컬에_적용되지_않는다()
        {
            var state = new SocketPacketState();
            var asc = CreateAsc();
            var localPlayer = new LocalPlayerContext();
            localPlayer.Set(asc);
            _receiver = Build(state, localPlayer);

            state.ApplyEffect(new SocketEffectApply("def_down_10", instanceId: 7, targetId: 999, sourceId: 200, startTick: 0, stacks: 1));

            Assert.AreEqual(0, asc.ActiveEffects.Count, "내가 아닌 대상의 Effect는 로컬 ASC에 적용되면 안 된다.");
        }

        [Test]
        public void 알수없는_EffectId는_무시된다()
        {
            var state = new SocketPacketState();
            var asc = CreateAsc();
            var localPlayer = new LocalPlayerContext();
            localPlayer.Set(asc);
            _receiver = Build(state, localPlayer);

            LogAssert.Expect(LogType.Warning, new Regex("EffectReceiver"));
            state.ApplyEffect(new SocketEffectApply("nope_unknown", instanceId: 1, targetId: LocalUserId, sourceId: 0, startTick: 0, stacks: 1));

            Assert.AreEqual(0, asc.ActiveEffects.Count);
        }

        [Test]
        public void 서버_basic_attack_dmg_수신시_로컬_HP가_즉발_감소한다()
        {
            var state = new SocketPacketState();
            var asc = CreateAsc();
            var localPlayer = new LocalPlayerContext();
            localPlayer.Set(asc);
            _receiver = Build(state, localPlayer);

            // CA-3: 서버가 적중 판정 후 보낸 basic_attack_dmg(Instant Health -10) 적용 → HP 감소.
            state.ApplyEffect(new SocketEffectApply("basic_attack_dmg", instanceId: 1, targetId: LocalUserId, sourceId: 200, startTick: 0, stacks: 1));

            Assert.AreEqual(90, asc.GetAttribute(EGameplayAttribute.Health).CurrentValue);
            Assert.AreEqual(0, asc.ActiveEffects.Count, "즉발 피해는 ActiveEffect로 추적되지 않는다");
        }

        // ── 헬퍼 ────────────────────────────────────────

        private EffectReceiver Build(SocketPacketState state, LocalPlayerContext localPlayer)
        {
            var receiver = new EffectReceiver(state, new GameplayEffectCatalog(), localPlayer, MakeAuth(LocalUserId));
            receiver.Initialize();
            return receiver;
        }

        private AbilitySystemComponent CreateAsc()
        {
            var go = new GameObject("LocalPlayer");
            _objects.Add(go);
            var asc = go.AddComponent<AbilitySystemComponent>();
            asc.Attributes = new List<GameplayAttribute>
            {
                new(EGameplayAttribute.Health, 100, 100),
                new(EGameplayAttribute.Defense, 30, 100000, EAttributeKind.Stat),
            };
            asc.InitializeAttributes();
            return asc;
        }

        private static AuthSession MakeAuth(long userId)
        {
            string payloadB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"sub\":\"{userId}\"}}"));
            var auth = new AuthSession();
            auth.Update($"header.{payloadB64}.sig", "refresh", 0);
            return auth;
        }
    }
}
