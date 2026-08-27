using System;
using System.Collections.Generic;
using System.Text;
using Game.Network.Socket;
using Game.Presentation.InGame;
using Game.System.Auth;
using Game.System.Player;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// 파티 HP HUD Model 검증 — PartyAscRegistry(로컬+원격 ASC) 집계 + ASC HP 변경 시 Changed 발행.
    /// HP 진실원 = 서버 권위(GAS). 이 Model 은 신규 패킷 없이 기존 ASC 만 집계한다.
    /// </summary>
    public class PartyModelTests
    {
        private readonly List<GameObject> _objects = new();
        private PartyModel _model;

        [TearDown]
        public void TearDown()
        {
            _model?.Dispose();
            _model = null;
            foreach (var o in _objects)
                if (o != null) Object.Destroy(o);
            _objects.Clear();
        }

        [Test]
        public void 등록된_로컬과_원격_ASC의_HP를_집계한다()
        {
            var registry = new PartyAscRegistry();
            registry.Register(100, MakeAsc(100));
            registry.Register(200, MakeAsc(80));

            _model = new PartyModel(registry, new SocketPacketState(), MakeAuth(100));
            _model.Initialize();

            var party = _model.GetParty();
            Assert.AreEqual(2, party.Count);

            var local = Find(party, 100);
            Assert.IsTrue(local.IsLocal, "내 UserId 는 IsLocal 이어야 한다.");
            Assert.AreEqual(100, local.Hp);
            Assert.AreEqual(100, local.MaxHp);

            var remote = Find(party, 200);
            Assert.IsFalse(remote.IsLocal, "다른 UserId 는 원격이어야 한다.");
            Assert.AreEqual(80, remote.Hp);
        }

        [Test]
        public void 원격_ASC의_HP가_변하면_Changed가_발행되고_집계에_반영된다()
        {
            var registry = new PartyAscRegistry();
            var remote = MakeAsc(100);
            registry.Register(200, remote);

            _model = new PartyModel(registry, new SocketPacketState(), MakeAuth(100));
            _model.Initialize();

            int changes = 0;
            _model.Changed += () => changes++;

            // 서버 권위 피해가 도착해 원격 ASC HP 가 줄어드는 상황(EffectReceiver 가 SetCurrent/ApplyModifier 로 반영).
            remote.SetCurrent(EGameplayAttribute.Health, 70);

            Assert.GreaterOrEqual(changes, 1, "ASC HP 변경 시 Model.Changed 가 발행돼야 한다.");
            Assert.AreEqual(70, Find(_model.GetParty(), 200).Hp);
        }

        [Test]
        public void 원격_디스폰_해제_시_파티에서_빠진다()
        {
            var registry = new PartyAscRegistry();
            registry.Register(100, MakeAsc(100));
            registry.Register(200, MakeAsc(100));

            _model = new PartyModel(registry, new SocketPacketState(), MakeAuth(100));
            _model.Initialize();
            Assert.AreEqual(2, _model.GetParty().Count);

            registry.Unregister(200);
            Assert.AreEqual(1, _model.GetParty().Count, "디스폰한 원격은 파티 목록에서 빠져야 한다.");
        }

        // ── 헬퍼 ────────────────────────────────────────

        private GasComponent MakeAsc(int hp)
        {
            var go = new GameObject("ASC");
            _objects.Add(go);
            var asc = go.AddComponent<GasComponent>();
            asc.Attributes = new List<GameplayAttribute> { new(EGameplayAttribute.Health, hp, 100) };
            asc.InitializeAttributes();
            return asc;
        }

        private static PartyMemberInfo Find(IReadOnlyList<PartyMemberInfo> list, long userId)
        {
            foreach (var m in list)
                if (m.UserId == userId) return m;
            Assert.Fail($"UserId={userId} 를 파티 목록에서 찾지 못했다.");
            return default;
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
