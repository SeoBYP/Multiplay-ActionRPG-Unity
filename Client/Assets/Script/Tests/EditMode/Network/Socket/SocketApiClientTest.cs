using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using NUnit.Framework;
using VContainer;

namespace Game.Tests.EditMode.Socket
{
    [TestFixture]
    public class SocketApiClientTest
    {
        private IObjectResolver _container;

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            var installer = new SocketApiClient();
            installer.Install(builder);
            _container = builder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            (_container as IDisposable)?.Dispose();
        }

        [Test]
        public void SocketServices_등록됨()
        {
            Assert.IsNotNull(_container.Resolve<ISocketPacketState>());
            Assert.IsNotNull(_container.Resolve<ISocketPacketDispatcher>());
            Assert.IsNotNull(_container.Resolve<ISocketConnector>());
            Assert.IsNotNull(_container.Resolve<ISocketSession>());
        }

        /// <summary>
        /// 원격 공격 연출: 서버가 S_Attack{AttackerId,SkillId} 를 브로드캐스트하면 OnPlayerAttacked 로 발행된다.
        /// RemoteDriver 가 이 신호로 해당 UserId 의 스윙 애니를 재생한다(적중·데미지는 서버 권위 S_ApplyEffect).
        /// </summary>
        [Test]
        public async Task S_Attack_Dispatch_하면_OnPlayerAttacked_가_발행된다()
        {
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            long gotAttacker = 0;
            int gotSkill = -1;
            state.OnPlayerAttacked += (attackerId, skillId) => { gotAttacker = attackerId; gotSkill = skillId; };

            await dispatcher.DispatchAsync(new S_Attack { AttackerId = 777, SkillId = 1 });

            Assert.AreEqual(777, gotAttacker, "AttackerId 가 그대로 전달돼야 한다.");
            Assert.AreEqual(1, gotSkill, "SkillId 가 그대로 전달돼야 한다.");
        }

        /// <summary>
        /// 원격 회피 연출: 서버가 S_Dodge{UserId} 를 브로드캐스트하면 OnPlayerDodged 로 발행된다.
        /// RemoteDriver 가 이 신호로 해당 UserId 의 회피(구르기) 애니를 재생한다(무적은 서버 권위).
        /// </summary>
        [Test]
        public async Task S_Dodge_Dispatch_하면_OnPlayerDodged_가_발행된다()
        {
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            long gotUser = 0;
            float gotX = 0f, gotY = 0f;
            state.OnPlayerDodged += (userId, dirX, dirY) => { gotUser = userId; gotX = dirX; gotY = dirY; };

            await dispatcher.DispatchAsync(new S_Dodge { UserId = 888, DirX = -1f, DirY = 0f });

            Assert.AreEqual(888, gotUser, "S_Dodge.UserId 가 OnPlayerDodged 로 그대로 전달돼야 한다.");
            // 방향까지 전달돼야 원격이 8방향 구르기를 재생한다(예전엔 늘 정면으로 근사했다).
            Assert.AreEqual(-1f, gotX, 0.001f, "회피 방향 X 가 전달돼야 한다.");
            Assert.AreEqual(0f, gotY, 0.001f, "회피 방향 Y 가 전달돼야 한다.");
        }

        [Test]
        public async Task PlayerJoined_후_Move_Dispatch_플레이어_상태_갱신()
        {
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            await dispatcher.DispatchAsync(new S_PlayerJoined
            {
                Success = true,
                UserId = 101,
                Nickname = "alpha",
                PosX = 1.5f,
                PosY = 2.5f,
                PosZ = 3.5f,
                RotY = 45f
            });

            Assert.IsTrue(state.TryGetPlayer(101, out var joinedPlayer));
            Assert.AreEqual("alpha", joinedPlayer.Nickname);
            Assert.AreEqual(1.5f, joinedPlayer.PosX);
            Assert.AreEqual(45f, joinedPlayer.RotY);

            await dispatcher.DispatchAsync(new S_Move
            {
                UserId = 101,
                PosX = 10f,
                PosY = 20f,
                PosZ = 30f,
                RotY = 90f,
                TimeStamp = 777
            });

            Assert.IsTrue(state.TryGetPlayer(101, out var movedPlayer));
            Assert.AreEqual("alpha", movedPlayer.Nickname);
            Assert.AreEqual(10f, movedPlayer.PosX);
            Assert.AreEqual(20f, movedPlayer.PosY);
            Assert.AreEqual(30f, movedPlayer.PosZ);
            Assert.AreEqual(90f, movedPlayer.RotY);
            Assert.AreEqual(777L, movedPlayer.TimeStamp);
        }

        [Test]
        public async Task PlayerJoined_HP기준선이_스냅샷에_실리고_Move후에도_보존된다()
        {
            // 파티 HP HUD: S_PlayerJoined 의 Hp/MaxHp(서버 권위) → 스냅샷 → 원격 ASC 초기화 기준선.
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            await dispatcher.DispatchAsync(new S_PlayerJoined
            {
                Success = true,
                UserId = 202,
                Nickname = "bravo",
                SpawnIndex = 2,
                Hp = 110,
                MaxHp = 140
            });

            Assert.IsTrue(state.TryGetPlayer(202, out var joined));
            Assert.AreEqual(110, joined.Hp, "S_PlayerJoined.Hp 가 스냅샷에 실려야 한다.");
            Assert.AreEqual(140, joined.MaxHp, "S_PlayerJoined.MaxHp 가 스냅샷에 실려야 한다.");

            // 이동 후에도 HP 기준선은 유지돼야 한다(WithTransform 이 Hp/MaxHp 보존).
            await dispatcher.DispatchAsync(new S_Move { UserId = 202, PosX = 1f, PosY = 0f, PosZ = 1f, RotY = 0f, TimeStamp = 5 });

            Assert.IsTrue(state.TryGetPlayer(202, out var moved));
            Assert.AreEqual(110, moved.Hp, "이동 후에도 Hp 기준선이 보존돼야 한다.");
            Assert.AreEqual(140, moved.MaxHp, "이동 후에도 MaxHp 기준선이 보존돼야 한다.");
        }

        [Test]
        public async Task PlayerJoined_실패시_플레이어_추가되지_않음()
        {
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            await dispatcher.DispatchAsync(new S_PlayerJoined
            {
                Success = false,
                UserId = 404,
                Nickname = "blocked"
            });

            Assert.IsFalse(state.TryGetPlayer(404, out _));
        }

        [Test]
        public async Task PlayerLeft_Dispatch시_플레이어가_상태에서_제거된다()
        {
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            await dispatcher.DispatchAsync(new S_PlayerJoined
            {
                Success = true,
                UserId = 202,
                Nickname = "leaver"
            });
            Assert.IsTrue(state.TryGetPlayer(202, out _));

            await dispatcher.DispatchAsync(new S_PlayerLeft { UserId = 202 });

            Assert.IsFalse(state.TryGetPlayer(202, out _));
        }

        [Test]
        public void RemovePlayer_없는_유저여도_예외없이_무시된다()
        {
            var state = _container.Resolve<ISocketPacketState>();

            Assert.DoesNotThrow(() => state.RemovePlayer(999999));
            Assert.IsFalse(state.TryGetPlayer(999999, out _));
        }

        [Test]
        public void GetAllPlayers_업서트한_전원을_반환하고_제거되면_빠진다()
        {
            var state = _container.Resolve<ISocketPacketState>();

            state.UpsertPlayer(1, "a", 0, "dungeon_01", 0, 0, 0, 0);
            state.UpsertPlayer(2, "b", 1, "dungeon_01", 0, 0, 0, 0);
            CollectionAssert.AreEquivalent(
                new[] { 1L, 2L },
                state.GetAllPlayers().Select(p => p.UserId).ToArray());

            state.RemovePlayer(1);
            CollectionAssert.AreEquivalent(
                new[] { 2L },
                state.GetAllPlayers().Select(p => p.UserId).ToArray());
        }

        // ── M3 ⑥: 몬스터 상태 릴레이 (디스패처 → 핸들러 → 상태) ──

        [Test]
        public async Task SpawnMonster_Dispatch시_몬스터가_상태에_추가된다()
        {
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            await dispatcher.DispatchAsync(new S_SpawnMonster
            {
                InstanceId = 7,
                MonsterId = "creepy_demon",
                PosX = 6f, PosY = 0f, PosZ = 6f, RotY = 90f,
                Hp = 30, MaxHp = 30
            });

            Assert.IsTrue(state.TryGetMonster(7, out var m));
            Assert.AreEqual("creepy_demon", m.MonsterId);
            Assert.AreEqual(6f, m.PosX);
            Assert.AreEqual(30, m.Hp);
            Assert.AreEqual(30, m.MaxHp);
        }

        [Test]
        public async Task MonsterState_Dispatch시_위치HP페이즈가_갱신되고_식별정보는_유지된다()
        {
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            await dispatcher.DispatchAsync(new S_SpawnMonster { InstanceId = 7, MonsterId = "creepy_demon", Hp = 30, MaxHp = 30 });
            await dispatcher.DispatchAsync(new S_MonsterState
            {
                InstanceId = 7, PosX = 9f, PosY = 0f, PosZ = 5f, RotY = 45f, Hp = 18, Phase = 2, Seq = 1
            });

            Assert.IsTrue(state.TryGetMonster(7, out var m));
            Assert.AreEqual("creepy_demon", m.MonsterId); // 식별정보 유지
            Assert.AreEqual(30, m.MaxHp);          // 유지
            Assert.AreEqual(9f, m.PosX);
            Assert.AreEqual(18, m.Hp);
            Assert.AreEqual((byte)2, m.Phase);
            Assert.AreEqual(1, m.Seq);
        }

        [Test]
        public async Task 뒤늦게_도착한_옛_상태는_Seq로_버려진다_AC_C3()
        {
            // D2 재현: 서버 틱이 HP30 패킷을 **먼저 만들고**(Seq=1) 데미지가 HP18 패킷을 나중에 만든다(Seq=2).
            // 송신은 데미지가 먼저 → 옛 HP30(Seq=1)이 **나중에 도착**. 버리지 않으면 HP 가 되돌아간다.
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            await dispatcher.DispatchAsync(new S_SpawnMonster { InstanceId = 7, MonsterId = "creepy_demon", Hp = 30, MaxHp = 30 });
            await dispatcher.DispatchAsync(new S_MonsterState { InstanceId = 7, Hp = 18, Phase = 3, Seq = 2 }); // 데미지(신)
            await dispatcher.DispatchAsync(new S_MonsterState { InstanceId = 7, Hp = 30, Phase = 2, Seq = 1 }); // 틱(스테일) — 뒤늦게 도착

            Assert.IsTrue(state.TryGetMonster(7, out var m));
            Assert.AreEqual(18, m.Hp);            // HP 가 30 으로 되돌아가면 D2 재발
            Assert.AreEqual((byte)3, m.Phase);    // 페이즈도 함께 스테일이면 안 된다
            Assert.AreEqual(2, m.Seq);
        }

        [Test]
        public async Task 같은_Seq_재전송도_버린다_AC_C3()
        {
            // 중복 전달(재전송·큐 재시도)로 같은 Seq 가 또 오면 무시한다 — `<=` 판정의 근거.
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            await dispatcher.DispatchAsync(new S_SpawnMonster { InstanceId = 7, MonsterId = "creepy_demon", Hp = 30, MaxHp = 30 });
            await dispatcher.DispatchAsync(new S_MonsterState { InstanceId = 7, Hp = 18, Seq = 5 });

            int moved = 0;
            state.OnMonsterMoved += _ => moved++;
            await dispatcher.DispatchAsync(new S_MonsterState { InstanceId = 7, Hp = 99, Seq = 5 }); // 같은 버전 = 무시

            Assert.IsTrue(state.TryGetMonster(7, out var m));
            Assert.AreEqual(18, m.Hp);
            Assert.AreEqual(0, moved); // 버렸으면 보간 이벤트도 발행되지 않아야 한다
        }

        [Test]
        public async Task 새_Seq는_정상_반영된다_AC_C3()
        {
            // 스테일 드롭이 **정상 갱신까지 막지 않는지** 확인(과잉 드롭 = 몬스터 정지 버그).
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            await dispatcher.DispatchAsync(new S_SpawnMonster { InstanceId = 7, MonsterId = "creepy_demon", Hp = 30, MaxHp = 30 });
            await dispatcher.DispatchAsync(new S_MonsterState { InstanceId = 7, Hp = 25, Seq = 1 });
            await dispatcher.DispatchAsync(new S_MonsterState { InstanceId = 7, Hp = 20, Seq = 2 });
            await dispatcher.DispatchAsync(new S_MonsterState { InstanceId = 7, Hp = 15, Seq = 3 });

            Assert.IsTrue(state.TryGetMonster(7, out var m));
            Assert.AreEqual(15, m.Hp);
            Assert.AreEqual(3, m.Seq);
        }

        [Test]
        public async Task MonsterDead_Dispatch시_몬스터가_제거된다()
        {
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            await dispatcher.DispatchAsync(new S_SpawnMonster { InstanceId = 7, MonsterId = "creepy_demon", Hp = 30, MaxHp = 30 });
            Assert.IsTrue(state.TryGetMonster(7, out _));

            await dispatcher.DispatchAsync(new S_MonsterDead { InstanceId = 7 });

            Assert.IsFalse(state.TryGetMonster(7, out _));
        }

        [Test]
        public async Task OnMonsterSpawned_이벤트가_발행된다()
        {
            var dispatcher = _container.Resolve<ISocketPacketDispatcher>();
            var state = _container.Resolve<ISocketPacketState>();

            SocketMonsterSnapshot spawned = null;
            state.OnMonsterSpawned += s => spawned = s;

            await dispatcher.DispatchAsync(new S_SpawnMonster { InstanceId = 3, MonsterId = "creepy_demon", Hp = 30, MaxHp = 30 });

            Assert.IsNotNull(spawned);
            Assert.AreEqual(3, spawned.InstanceId);
        }
    }
}
