using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Core;
using Game.Network.Https.Services;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using GameServer.Grpc.Auth;
using GameServer.Grpc.DungeonLobby;
using GameServer.Grpc.Inventory;
using GameServer.Grpc.User;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.E2E
{
    [TestFixture]
    public class SocketE2ETests : E2ETestBase
    {
        [UnityTest]
        public IEnumerator SocketSession_두_클라이언트_인증후_입장_성공() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();
            var hostClient = await ConnectJoinedSessionAsync(room.RoomId, room.HostUserId, Timeout());
            var guestClient = await ConnectJoinedSessionAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                Assert.AreEqual(SocketSessionState.Joined, hostClient.Session.State);
                Assert.IsTrue(hostClient.State.TryGetPlayer(room.HostUserId, out var hostPlayer));
                Assert.AreEqual(room.HostNickname, hostPlayer.Nickname);
                // 파티 HP HUD: S_PlayerJoined 가 서버 권위 HP 기준선을 실어야 한다(원격 ASC 초기화 소스).
                Assert.Greater(hostPlayer.MaxHp, 0, "S_PlayerJoined 에 서버 권위 MaxHp(레벨 MaxHealth)가 실려야 한다");
                Assert.AreEqual(hostPlayer.MaxHp, hostPlayer.Hp, "입장 시 만피(Hp==MaxHp)로 실려야 한다");

                Assert.AreEqual(SocketSessionState.Joined, guestClient.Session.State);
                Assert.IsTrue(guestClient.State.TryGetPlayer(room.GuestUserId, out var guestPlayer));
                Assert.AreEqual(room.GuestNickname, guestPlayer.Nickname);
                Assert.Greater(guestPlayer.MaxHp, 0, "게스트 S_PlayerJoined 에도 서버 권위 MaxHp 가 실려야 한다");
                Assert.AreEqual(guestPlayer.MaxHp, guestPlayer.Hp, "게스트도 입장 시 만피로 실려야 한다");
            }
            finally
            {
                await hostClient.Session.DisconnectAsync(CancellationToken.None);
                await guestClient.Session.DisconnectAsync(CancellationToken.None);
                await hostClient.Connector.DisposeAsync();
                await guestClient.Connector.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_전원_입장하면_양쪽_S_GameStatus_InProgress_수신() => UniTask.ToCoroutine(async () =>
        {
            // 전원(2/2) 입장 시 서버 RoomJoinLeaveHandler 가 방에 S_GameStatus(InProgress) 브로드캐스트.
            // = 클라 "전원 입장(던전 준비 완료)" 신호. 로딩 해제 → Fader 전환의 트리거.
            var room = await CreateStartedTwoPlayerRoomAsync();
            var hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
            var guestCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                var guestStatus = await guestCollector.WaitForPacketAsync<S_GameStatus>(
                    packet => packet.RoomId == room.RoomId && packet.GameStatus == EGameStatus.InProgress,
                    Timeout());
                Assert.AreEqual(EGameStatus.InProgress, guestStatus.GameStatus, "게스트가 전원 입장 신호를 받아야 한다");

                var hostStatus = await hostCollector.WaitForPacketAsync<S_GameStatus>(
                    packet => packet.RoomId == room.RoomId && packet.GameStatus == EGameStatus.InProgress,
                    Timeout());
                Assert.AreEqual(EGameStatus.InProgress, hostStatus.GameStatus, "호스트도 전원 입장 신호를 받아야 한다");
            }
            finally
            {
                await hostCollector.DisposeAsync();
                await guestCollector.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_호스트가_Move_전송하면_게스트가_S_Move_수신() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();
            var hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
            var guestCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                const float moveX = 10f;
                const float moveY = 0f;
                const float moveZ = 5f;
                const float rotY = 90f;

                await hostCollector.SendAsync(new C_Move
                {
                    PosX = moveX,
                    PosY = moveY,
                    PosZ = moveZ,
                    RotY = rotY
                }, Timeout());

                var move = await guestCollector.WaitForPacketAsync<S_Move>(
                    packet => packet.UserId == room.HostUserId
                           && packet.PosX == moveX
                           && packet.PosY == moveY
                           && packet.PosZ == moveZ
                           && packet.RotY == rotY,
                    Timeout());

                Assert.AreEqual(room.HostUserId, move.UserId);
                Assert.AreEqual(moveX, move.PosX);
                Assert.AreEqual(moveY, move.PosY);
                Assert.AreEqual(moveZ, move.PosZ);
                Assert.AreEqual(rotY, move.RotY);
            }
            finally
            {
                await hostCollector.DisposeAsync();
                await guestCollector.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_호스트가_정면의_게스트를_공격하면_서버권위_적중_S_ApplyEffect() => UniTask.ToCoroutine(async () =>
        {
            // CA-3: 서버가 hitbox로 적중을 재계산(권위). 호스트를 원점·+Z 정면, 게스트를 정면 1유닛 앞으로
            // 이동시켜 basic_swing hitbox 안에 들어가게 한 뒤 C_Attack → 서버가 적중 판정 → S_ApplyEffect 브로드캐스트.
            var room = await CreateStartedTwoPlayerRoomAsync();
            var hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
            var guestCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                // 위치 세팅: 서버 Room이 두 이동을 반영하도록 전송 후 잠시 대기.
                await hostCollector.SendAsync(new C_Move { PosX = 0, PosY = 0, PosZ = 0, RotY = 0 }, Timeout());
                await guestCollector.SendAsync(new C_Move { PosX = 0, PosY = 0, PosZ = 1, RotY = 0 }, Timeout());
                await UniTask.Delay(TimeSpan.FromMilliseconds(400));

                await hostCollector.SendAsync(new C_Attack { SkillId = 0 }, Timeout());

                var apply = await guestCollector.WaitForPacketAsync<S_ApplyEffect>(
                    packet => packet.TargetId == room.GuestUserId && packet.SourceId == room.HostUserId,
                    Timeout());

                Assert.AreEqual(room.GuestUserId, apply.TargetId);
                Assert.AreEqual(room.HostUserId, apply.SourceId);
                Assert.AreEqual("basic_attack_dmg", apply.EffectId, "basic_swing의 OnHitEffect가 적용돼야 한다");
                Assert.Greater(apply.InstanceId, 0, "서버가 InstanceId를 권위 발급해야 한다");
            }
            finally
            {
                await hostCollector.DisposeAsync();
                await guestCollector.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_콤보A_공격하면_S_Attack_skillId2_와_combo_a_dmg를_수신한다() => UniTask.ToCoroutine(async () =>
        {
            // #7 콤보: 클라 ComboDriver 가 단계별 skillId(2/3/4)를 송신 → 서버 ResolveSkill(combo_a) →
            //   S_Attack{SkillId=2} 브로드캐스트 + 정면 적중 시 combo_a_dmg 부여. A 를 대표 검증(B/C 는 동일 경로·데이터만 상승).
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
            var guest = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                await host.SendAsync(new C_Move { PosX = 0, PosY = 0, PosZ = 0, RotY = 0 }, Timeout());
                await guest.SendAsync(new C_Move { PosX = 0, PosY = 0, PosZ = 1, RotY = 0 }, Timeout());
                await UniTask.Delay(TimeSpan.FromMilliseconds(400));

                await host.SendAsync(new C_Attack { SkillId = 2 }, Timeout()); // 2 = combo_a

                var atk = await host.WaitForPacketAsync<S_Attack>(p => p.AttackerId == room.HostUserId, Timeout());
                Assert.AreEqual(2, atk.SkillId, "콤보A 는 skillId 2 로 브로드캐스트돼야 한다");

                var apply = await guest.WaitForPacketAsync<S_ApplyEffect>(
                    p => p.TargetId == room.GuestUserId && p.SourceId == room.HostUserId, Timeout());
                Assert.AreEqual("combo_a_dmg", apply.EffectId, "콤보A 적중은 combo_a_dmg 를 부여해야 한다");
            }
            finally
            {
                await host.DisposeAsync();
                await guest.DisposeAsync();
            }
        });

        // ── M3 몬스터(서버 권위 스폰/이동/전투) ────────────────────────

        [UnityTest]
        public IEnumerator RawSocket_입장하면_몬스터_스폰_로스터_수신() => UniTask.ToCoroutine(async () =>
        {
            // 입장 시 RoomJoinLeaveHandler가 현재 몬스터 로스터(S_SpawnMonster×N)를 회신.
            // dungeon_01에는 슬라임 1마리(MaxHp 30) 시드됨.
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                var spawn = await host.WaitForPacketAsync<S_SpawnMonster>(p => p.MonsterId == "slime", Timeout());
                Assert.AreEqual("slime", spawn.MonsterId);
                Assert.Greater(spawn.InstanceId, 0, "서버가 InstanceId를 권위 발급해야 한다");
                Assert.AreEqual(30, spawn.MaxHp, "dungeon_01 슬라임 MaxHp");
            }
            finally
            {
                await host.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_몬스터를_반복_공격하면_S_MonsterDead_수신() => UniTask.ToCoroutine(async () =>
        {
            // 플레이어→몬스터: basic_swing 적중마다 서버 권위로 HP -10(basic_attack_dmg). 30→0 = 3타 이상.
            // 슬라임이 패트롤/추격으로 움직이므로 최신 S_MonsterState 위치로 재조준해 정면(−Z 1유닛)에서 타격.
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                var spawn = await host.WaitForPacketAsync<S_SpawnMonster>(p => p.MonsterId == "slime", Timeout());
                int slimeId = spawn.InstanceId;

                bool dead = false;
                var deadline = DateTime.UtcNow.AddSeconds(ServerConfig.TimeoutSeconds);
                while (!dead && DateTime.UtcNow < deadline)
                {
                    float sx = spawn.PosX, sz = spawn.PosZ;
                    if (host.TryGetLatest<S_MonsterState>(p => p.InstanceId == slimeId, out var st))
                    {
                        sx = st.PosX;
                        sz = st.PosZ;
                    }

                    // 슬라임 1유닛 앞(−Z)에서 +Z 정면 → basic_swing hitbox에 슬라임이 들어옴.
                    await host.SendAsync(new C_Move { PosX = sx, PosY = 0, PosZ = sz - 1f, RotY = 0 }, Timeout());
                    await UniTask.Delay(TimeSpan.FromMilliseconds(250));
                    await host.SendAsync(new C_Attack { SkillId = 0 }, Timeout());
                    await UniTask.Delay(TimeSpan.FromMilliseconds(200));

                    dead = host.TryGetLatest<S_MonsterDead>(p => p.InstanceId == slimeId, out _);
                }

                Assert.IsTrue(dead, "슬라임(HP30)을 반복 공격하면 S_MonsterDead를 받아야 한다");
            }
            finally
            {
                await host.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_공격하면_S_Attack_연출_브로드캐스트를_수신한다() => UniTask.ToCoroutine(async () =>
        {
            // 원격 공격 애니: 서버 게이트(마나·쿨다운)를 통과한 스윙만 S_Attack{AttackerId,SkillId} 를 방에 브로드캐스트.
            // room.Broadcast 는 방 전원 발신이라 시전자 자신도 수신 → RemoteDriver 가 타 플레이어 스윙 애니 재생.
            // 적중·데미지는 별도(S_ApplyEffect/S_MonsterState, 서버 권위) — 이 패킷은 연출 전용이라 여기서 판정하지 않는다.
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                // basic_swing(SkillId=0): 마나 0(무료) + 첫 발동은 쿨다운 통과 → 브로드캐스트돼야 한다.
                await host.SendAsync(new C_Attack { SkillId = 0 }, Timeout());

                var atk = await host.WaitForPacketAsync<S_Attack>(p => p.AttackerId == room.HostUserId, Timeout());

                Assert.AreEqual(room.HostUserId, atk.AttackerId, "S_Attack 의 AttackerId 가 시전자여야 한다");
                Assert.AreEqual(0, atk.SkillId, "SkillId(basic_swing=0)가 그대로 전달돼야 한다");
            }
            finally
            {
                await host.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_회피하면_S_Dodge_연출_브로드캐스트를_수신한다() => UniTask.ToCoroutine(async () =>
        {
            // 원격 회피 애니: 서버 게이트(쿨다운·마나)를 통과한 회피만 S_Dodge{UserId} 를 방에 브로드캐스트.
            // room.Broadcast 는 방 전원 발신이라 시전자 자신도 수신 → 다른 클라 RemoteDriver 가 회피(구르기) 애니 재생.
            // 무적 창/피해 무시는 별도(서버 권위) — 이 패킷은 연출 전용이라 여기서 판정하지 않는다.
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                // 첫 회피 = 쿨다운 통과 + Lv1 마나(≥DodgeConfig.ManaCost) → 브로드캐스트돼야 한다.
                await host.SendAsync(new C_Dodge(), Timeout());

                var dodge = await host.WaitForPacketAsync<S_Dodge>(p => p.UserId == room.HostUserId, Timeout());
                Assert.AreEqual(room.HostUserId, dodge.UserId, "S_Dodge 의 UserId 가 시전자여야 한다");
            }
            finally
            {
                await host.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_몬스터_사거리_안이면_S_ApplyEffect_monster_attack_dmg_수신() => UniTask.ToCoroutine(async () =>
        {
            // 몬스터→플레이어: 패트롤 사각형(6,6)~(10,10) 중심(8,8)으로 가면 슬라임이 항상 aggro 범위 →
            // 추격·사거리 진입 → 쿨다운마다 monster_attack_dmg(S_ApplyEffect)를 그 플레이어에게 발행.
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                await host.WaitForPacketAsync<S_SpawnMonster>(p => p.MonsterId == "slime", Timeout());

                await host.SendAsync(new C_Move { PosX = 8, PosY = 0, PosZ = 8, RotY = 0 }, Timeout());

                var apply = await host.WaitForPacketAsync<S_ApplyEffect>(
                    p => p.EffectId == "monster_attack_dmg" && p.TargetId == room.HostUserId,
                    Timeout());

                Assert.AreEqual("monster_attack_dmg", apply.EffectId);
                Assert.AreEqual(room.HostUserId, apply.TargetId, "공격 대상이 가장 가까운 플레이어(호스트)여야 한다");
            }
            finally
            {
                await host.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_몬스터_공격은_슬로우_CC도_함께_브로드캐스트한다() => UniTask.ToCoroutine(async () =>
        {
            // 2.6.2 던전 CC: slime(monsters.json onHitEffectId=slow_3s) 공격 시 서버 TickMonsters 가
            // 데미지(monster_attack_dmg)와 함께 CC(slow_3s, Amount=0) S_ApplyEffect 를 브로드캐스트 →
            // 클라 EffectReceiver 가 적용 → GrantedTags(State.Slow) 게이트. (서버 권위 CC 경로 검증)
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                await host.WaitForPacketAsync<S_SpawnMonster>(p => p.MonsterId == "slime", Timeout());

                await host.SendAsync(new C_Move { PosX = 8, PosY = 0, PosZ = 8, RotY = 0 }, Timeout());

                var cc = await host.WaitForPacketAsync<S_ApplyEffect>(
                    p => p.EffectId == "slow_3s" && p.TargetId == room.HostUserId,
                    Timeout());

                Assert.AreEqual("slow_3s", cc.EffectId);
                Assert.AreEqual(room.HostUserId, cc.TargetId);
                Assert.AreEqual(0, cc.Amount, "CC 는 HP 변경 없는 상태태그(Amount=0)여야 한다");
            }
            finally
            {
                await host.DisposeAsync();
            }
        });

        // ── 2.2 마나(서버 권위 검증·차감·동기화) ───────────────────────

        [UnityTest]
        public IEnumerator RawSocket_입장하면_초기_마나_S_PlayerMana_수신() => UniTask.ToCoroutine(async () =>
        {
            // 입장 시 RoomJoinLeaveHandler 가 owner 에게 초기 S_PlayerMana 송신 —
            // 클라 prefab 기준선(100)을 서버 권위 MaxMana(레벨테이블, Lv1=50)로 정렬. 만마(Mana==MaxMana)로 시작.
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                var mana = await host.WaitForPacketAsync<S_PlayerMana>(
                    p => p.UserId == room.HostUserId, Timeout());

                Assert.AreEqual(room.HostUserId, mana.UserId);
                Assert.Greater(mana.MaxMana, 0, "서버 권위 MaxMana(레벨테이블)가 실려야 한다");
                Assert.AreEqual(mana.MaxMana, mana.Mana, "입장 시 만마로 초기화돼야 한다");
            }
            finally
            {
                await host.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_회피하면_서버가_마나를_차감해_S_PlayerMana_정정한다() => UniTask.ToCoroutine(async () =>
        {
            // 서버 권위 마나: C_Dodge → DodgeHandler 가 쿨다운+마나(DodgeConfig.ManaCost=15) 게이트 통과 시
            // 차감하고 owner 에게 S_PlayerMana 정정 송신(클라 예측 차감과 정합 = 무한 회피 치팅 차단).
            // 리젠은 동기화 패킷을 안 보내므로(클라 동일예측 수렴) 입장 후 유일한 추가 S_PlayerMana = 이 차감 정정.
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                // 입장 초기 동기화로 권위 MaxMana 확보(만마 시작).
                var initial = await host.WaitForPacketAsync<S_PlayerMana>(
                    p => p.UserId == room.HostUserId, Timeout());
                int maxMana = initial.MaxMana;
                Assert.GreaterOrEqual(maxMana, DodgeConfig.ManaCost, "Lv1 마나가 회피 코스트 이상이어야 한다");

                await host.SendAsync(new C_Dodge(), Timeout());

                // 차감 정정 = 만마 − 회피 코스트.
                var afterDodge = await host.WaitForPacketAsync<S_PlayerMana>(
                    p => p.UserId == room.HostUserId && p.Mana < maxMana, Timeout());

                Assert.AreEqual(maxMana - DodgeConfig.ManaCost, afterDodge.Mana,
                    "회피 발동 시 서버가 DodgeConfig.ManaCost 만큼 차감해야 한다");
                Assert.AreEqual(maxMana, afterDodge.MaxMana, "MaxMana 는 변하지 않는다");
            }
            finally
            {
                await host.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_몬스터_전멸하면_양쪽_S_DungeonClear_수신() => UniTask.ToCoroutine(async () =>
        {
            // M4 A 트랙 ③: dungeon_01 시드 = 슬라임 1마리. 그 1마리를 처치하면 전멸 →
            // 서버 Room.TryMarkCleared(최초 1회) → S_DungeonClear 를 방에 브로드캐스트.
            // 시전자(호스트) + 같은 방 게스트 둘 다 수신해야 한다.
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
            var guest = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                var spawn = await host.WaitForPacketAsync<S_SpawnMonster>(p => p.MonsterId == "slime", Timeout());
                int slimeId = spawn.InstanceId;

                // 움직이는 슬라임을 최신 위치로 재조준하며 전멸(=클리어)까지 반복 타격.
                bool cleared = false;
                var deadline = DateTime.UtcNow.AddSeconds(ServerConfig.TimeoutSeconds);
                while (!cleared && DateTime.UtcNow < deadline)
                {
                    float sx = spawn.PosX, sz = spawn.PosZ;
                    if (host.TryGetLatest<S_MonsterState>(p => p.InstanceId == slimeId, out var st))
                    {
                        sx = st.PosX;
                        sz = st.PosZ;
                    }

                    // 슬라임 1유닛 앞(−Z)에서 +Z 정면 → basic_swing hitbox에 슬라임이 들어옴.
                    await host.SendAsync(new C_Move { PosX = sx, PosY = 0, PosZ = sz - 1f, RotY = 0 }, Timeout());
                    await UniTask.Delay(TimeSpan.FromMilliseconds(250));
                    await host.SendAsync(new C_Attack { SkillId = 0 }, Timeout());
                    await UniTask.Delay(TimeSpan.FromMilliseconds(200));

                    cleared = host.TryGetLatest<S_DungeonClear>(p => p.RoomId == room.RoomId, out _);
                }

                Assert.IsTrue(cleared, "몬스터 전멸 시 호스트(시전자)가 S_DungeonClear를 받아야 한다");
                host.TryGetLatest<S_DungeonClear>(p => p.RoomId == room.RoomId, out var hostClear);
                Assert.AreEqual(100, hostClear.RewardExp, "호스트 S_DungeonClear에 dungeon_01 보상 Exp(100)가 실려야 한다");

                // 같은 방의 게스트도 브로드캐스트를 받아야 한다(서버 권위 1회 발화).
                var guestClear = await guest.WaitForPacketAsync<S_DungeonClear>(
                    p => p.RoomId == room.RoomId, Timeout());
                Assert.AreEqual(room.RoomId, guestClear.RoomId, "게스트도 던전 클리어를 받아야 한다");
                Assert.AreEqual(100, guestClear.RewardExp, "게스트 S_DungeonClear에도 보상 Exp(100)가 실려야 한다");
            }
            finally
            {
                await host.DisposeAsync();
                await guest.DisposeAsync();
            }
        });

        // ── 3.3.7 루트 풀 E2E (사냥 → 드랍 → 줍기 → 인벤토리 지급) ──────────

        [UnityTest]
        public IEnumerator RawSocket_슬라임_처치_드랍_줍기하면_GameServer_인벤토리에_지급된다() => UniTask.ToCoroutine(async () =>
        {
            // 던전 루트 경로 풀 체인 검증(loot-drop.md §3):
            //   ① 슬라임 처치(서버 권위) → DropTable.Roll → S_SpawnGroundItem 브로드캐스트
            //   ② C_PickupItem → 서버 TryPickup(경쟁 중재·거리) → S_ItemPickedUp + Redis Stream 발행
            //   ③ GameServer LootGrantConsumer → GrantItemAsync → DB inventory_items(영속)
            //   ④ 클라 GetInventory(pull)로 지급 확인
            // dungeon_01 슬라임은 potion_hp_small 을 보장 드랍(DropTable Chance 1.0) → 결정적.
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                var spawn = await host.WaitForPacketAsync<S_SpawnMonster>(p => p.MonsterId == "slime", Timeout());
                int slimeId = spawn.InstanceId;

                // ① 움직이는 슬라임을 최신 위치로 재조준하며 처치 → 보장 드랍(S_SpawnGroundItem) 관측.
                S_SpawnGroundItem ground = null;
                var killDeadline = DateTime.UtcNow.AddSeconds(ServerConfig.TimeoutSeconds);
                while (ground == null && DateTime.UtcNow < killDeadline)
                {
                    float sx = spawn.PosX, sz = spawn.PosZ;
                    if (host.TryGetLatest<S_MonsterState>(p => p.InstanceId == slimeId, out var st))
                    {
                        sx = st.PosX;
                        sz = st.PosZ;
                    }

                    await host.SendAsync(new C_Move { PosX = sx, PosY = 0, PosZ = sz - 1f, RotY = 0 }, Timeout());
                    await UniTask.Delay(TimeSpan.FromMilliseconds(250));
                    await host.SendAsync(new C_Attack { SkillId = 0 }, Timeout());
                    await UniTask.Delay(TimeSpan.FromMilliseconds(200));

                    // 입장 시 바닥 로스터는 비어 있으므로(시드 0), 유일한 S_SpawnGroundItem = 이번 처치 드랍.
                    host.TryGetLatest<S_SpawnGroundItem>(p => p.ItemId == "potion_hp_small", out ground);
                }

                Assert.IsNotNull(ground, "슬라임 처치 시 보장 드랍(potion_hp_small)이 바닥에 스폰돼야 한다");
                Assert.AreEqual("potion_hp_small", ground.ItemId);
                Assert.GreaterOrEqual(ground.Qty, 1);

                // ② 드랍 위치로 이동(서버측 거리 검증 통과 = 거리 0) 후 줍기.
                await host.SendAsync(new C_Move { PosX = ground.PosX, PosY = 0, PosZ = ground.PosZ, RotY = 0 }, Timeout());
                await UniTask.Delay(TimeSpan.FromMilliseconds(200));
                await host.SendAsync(new C_PickupItem { GroundId = ground.GroundId }, Timeout());

                // 줍기 확정 토스트 + 바닥 제거 브로드캐스트.
                var picked = await host.WaitForPacketAsync<S_ItemPickedUp>(p => p.ItemId == "potion_hp_small", Timeout());
                Assert.AreEqual(ground.Qty, picked.Qty, "줍은 수량은 바닥 아이템 수량과 일치해야 한다");
                await host.WaitForPacketAsync<S_GroundItemRemoved>(p => p.GroundId == ground.GroundId, Timeout());

                // ③④ GameServer 인벤토리 지급은 Redis Stream 비동기 → GetInventory 폴링(호스트 토큰 인증).
                AccessToken = room.HostAccessToken;
                int grantedQty = 0;
                var grantDeadline = DateTime.UtcNow.AddSeconds(ServerConfig.TimeoutSeconds);
                while (grantedQty < ground.Qty && DateTime.UtcNow < grantDeadline)
                {
                    var inventory = await InventoryService.GetInventoryAsync(new GetInventoryRequest(), Timeout());
                    Assert.IsTrue(inventory.Result.Success, inventory.Result.Message);
                    var slot = inventory.Items.FirstOrDefault(i => i.ItemId == "potion_hp_small");
                    grantedQty = slot?.Quantity ?? 0;
                    if (grantedQty < ground.Qty)
                        await UniTask.Delay(TimeSpan.FromMilliseconds(300));
                }

                Assert.GreaterOrEqual(grantedQty, ground.Qty,
                    "줍기 후 GameServer 인벤토리에 potion_hp_small 이 줍은 수량만큼 지급돼야 한다");
            }
            finally
            {
                await host.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_던전에서_포션_소비하면_서버가_회복_S_ApplyEffect를_브로드캐스트한다() => UniTask.ToCoroutine(async () =>
        {
            // 플레이어 HP 서버 권위 증분2(authority-model §4): 던전 회복 = 크로스-서버.
            //   클라 ConsumeItem(GameServer 검증·차감) → PlayerConsumedMessage(Redis stream:game:player:consumed)
            //   → SocketServer PlayerConsumedConsumer → Room.ApplyPlayerEffect(+heal) + S_ApplyEffect(potion_hp_small) 브로드캐스트.
            // 호스트가 던전 방에서 그 회복 브로드캐스트를 받으면 전 경로 통과(차감은 GameServer 권위 = 무한힐 불가).
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                await host.WaitForPacketAsync<S_SpawnMonster>(p => p.MonsterId == "slime", Timeout()); // 던전 준비 동기화

                // 호스트 토큰으로 포션 시드(ClaimKill = Main 슬롯 처치, slime 보장 드랍 potion) 후 소비(gRPC).
                // 회복 수치는 클램프(만피)라도 브로드캐스트는 발생. ※ GrantItem 은 무한파밍 핵으로 제거됨(ClaimKill 대체).
                AccessToken = room.HostAccessToken;
                var grant = await InventoryService.ClaimKillAsync(
                    new ClaimKillRequest { MapId = "main_field_01", SlotId = 1 }, Timeout());
                Assert.IsTrue(grant.Result.Success, grant.Result.Message);
                Assert.IsTrue(grant.Granted.Any(g => g.ItemId == "potion_hp_small"), "slime 보장 드랍 potion 시드 실패");

                var consume = await InventoryService.ConsumeItemAsync(
                    new ConsumeItemRequest { ItemId = "potion_hp_small", Qty = 1 }, Timeout());
                Assert.IsTrue(consume.Result.Success, consume.Result.Message);

                // 크로스-서버 회복 통지 → 서버가 던전 방의 호스트에게 회복 효과를 브로드캐스트.
                var heal = await host.WaitForPacketAsync<S_ApplyEffect>(
                    p => p.EffectId == "potion_hp_small" && p.TargetId == room.HostUserId, Timeout());

                Assert.AreEqual("potion_hp_small", heal.EffectId);
                Assert.AreEqual(room.HostUserId, heal.TargetId, "회복 대상은 소비한 호스트여야 한다");
            }
            finally
            {
                await host.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_몬스터에게_죽으면_서버가_C_PlayerDead_없이_S_PlayerDead를_브로드캐스트한다() => UniTask.ToCoroutine(async () =>
        {
            // 플레이어 HP 서버 권위 증분1: 가만히 맞으면 서버가 자기 HP≤0 을 **직접 감지**(클라 C_PlayerDead 미송신)
            //   → S_PlayerDead{호스트} 브로드캐스트(죽은 본인도 수신). 불사 핵 차단의 직접 증명.
            // test_arena 맵 = 강한 fixture 몬스터 test_brute(사거리·aggro 무한, 쿨다운 50ms) → 호스트가 가만히 있어도
            //   ~2초에 사망. 게스트는 멀리 보내 호스트를 최근접 타깃으로 만든다. C_Attack/C_PlayerDead 는 절대 안 보냄.
            var room = await CreateStartedTwoPlayerRoomAsync("test_arena");
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
            var guest = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                await host.WaitForPacketAsync<S_SpawnMonster>(p => p.MonsterId == "test_brute", Timeout());

                bool dead = false;
                var deadline = DateTime.UtcNow.AddSeconds(20);
                while (!dead && DateTime.UtcNow < deadline)
                {
                    // 게스트를 멀리(keep-alive) → 브루트가 최근접 = 호스트를 타깃. 호스트는 스폰 자리에 가만히(keep-alive).
                    await guest.SendAsync(new C_Move { PosX = -18, PosY = 0, PosZ = -18, RotY = 0 }, Timeout());
                    await host.SendAsync(new C_Move { PosX = 0, PosY = 0, PosZ = 0, RotY = 0 }, Timeout());
                    await UniTask.Delay(TimeSpan.FromMilliseconds(300));
                    dead = host.TryGetLatest<S_PlayerDead>(p => p.UserId == room.HostUserId, out _);
                }

                Assert.IsTrue(dead, "서버가 HP0 을 직접 감지해 S_PlayerDead 를 발행해야 한다(C_PlayerDead 미송신).");
            }
            finally
            {
                await host.DisposeAsync();
                await guest.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_참가자_전원_다운하면_양쪽_S_DungeonFailed_수신() => UniTask.ToCoroutine(async () =>
        {
            // M4 B: 참가자 전원이 C_PlayerDead 를 보고하면 서버 Room.TryMarkFailed(최초 1회) →
            // S_DungeonFailed 를 방에 브로드캐스트. 일부만 다운이면 발화하지 않는다.
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
            var guest = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                // 호스트만 다운 → 아직 전원 다운 아님 → S_DungeonFailed 없어야 한다.
                await host.SendAsync(new C_PlayerDead(), Timeout());
                await UniTask.Delay(TimeSpan.FromMilliseconds(400));
                Assert.IsFalse(
                    host.TryGetLatest<S_DungeonFailed>(p => p.RoomId == room.RoomId, out _),
                    "일부만 다운이면 실패가 발화되면 안 된다");

                // 게스트까지 다운 → 전원 다운 → 양쪽 모두 S_DungeonFailed 수신.
                await guest.SendAsync(new C_PlayerDead(), Timeout());

                var hostFailed = await host.WaitForPacketAsync<S_DungeonFailed>(p => p.RoomId == room.RoomId, Timeout());
                var guestFailed = await guest.WaitForPacketAsync<S_DungeonFailed>(p => p.RoomId == room.RoomId, Timeout());
                Assert.AreEqual(room.RoomId, hostFailed.RoomId, "호스트가 던전 실패를 받아야 한다");
                Assert.AreEqual(room.RoomId, guestFailed.RoomId, "게스트가 던전 실패를 받아야 한다");
            }
            finally
            {
                await host.DisposeAsync();
                await guest.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_호스트가_퇴장하면_게스트가_S_PlayerLeft_수신() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();
            var hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
            var guestCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                // 호스트가 C_PlayerLeave 전송 → 서버 RoomManager.LeaveRoom →
                // 남은 게스트에게 S_PlayerLeft{UserId=hostUserId} 브로드캐스트.
                await hostCollector.SendAsync(new C_PlayerLeave(), Timeout());

                var left = await guestCollector.WaitForPacketAsync<S_PlayerLeft>(
                    packet => packet.UserId == room.HostUserId,
                    Timeout());

                Assert.AreEqual(room.HostUserId, left.UserId);
            }
            finally
            {
                await hostCollector.DisposeAsync();
                await guestCollector.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator RawSocket_다운된_아군을_부활하면_양쪽_S_PlayerRevived_수신() => UniTask.ToCoroutine(async () =>
        {
            // 2.5.2 Co-op 부활: 게스트 다운(C_PlayerDead → 서버 _downed 집계) → 호스트가 사거리(2.5m) 안에서
            // C_Revive → 서버 Room.TryRevive 검증(거리·다운상태·미실패) → S_PlayerRevived{게스트,Hp} 방 브로드캐스트.
            var room = await CreateStartedTwoPlayerRoomAsync();
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
            var guest = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                // 호스트·게스트를 부활 사거리 안 + 슬라임 aggro 밖(원점 근처)에 배치.
                await guest.SendAsync(new C_Move { PosX = 2, PosY = 0, PosZ = 2, RotY = 0 }, Timeout());
                await host.SendAsync(new C_Move { PosX = 2, PosY = 0, PosZ = 3, RotY = 0 }, Timeout());
                await UniTask.Delay(TimeSpan.FromMilliseconds(300)); // 서버 위치 갱신 대기

                // 게스트 다운(서버 _downed 집계). 호스트 생존 → 전원다운(실패) 아님.
                await guest.SendAsync(new C_PlayerDead(), Timeout());
                await guest.WaitForPacketAsync<S_PlayerDead>(p => p.UserId == room.GuestUserId, Timeout());

                // 호스트가 게스트 부활 요청(홀드는 클라 UX이므로 E2E는 완료 신호 C_Revive 만 송신).
                await host.SendAsync(new C_Revive { TargetUserId = room.GuestUserId }, Timeout());

                // 양쪽 모두 부활 브로드캐스트 수신(원격 가시성). HP 부분복구(>0).
                var guestRevived = await guest.WaitForPacketAsync<S_PlayerRevived>(
                    p => p.UserId == room.GuestUserId, Timeout());
                Assert.AreEqual(room.GuestUserId, guestRevived.UserId);
                Assert.Greater(guestRevived.Hp, 0, "부활 시 HP가 부분복구돼야 한다");

                var hostSaw = await host.WaitForPacketAsync<S_PlayerRevived>(
                    p => p.UserId == room.GuestUserId, Timeout());
                Assert.AreEqual(room.GuestUserId, hostSaw.UserId, "시전자도 부활 브로드캐스트를 본다");
            }
            finally
            {
                await host.DisposeAsync();
                await guest.DisposeAsync();
            }
        });

        // ── 재연결 / Disconnected 상태 시나리오 ────────────────────────

        /// <summary>
        /// C_PlayerLeave 없이 강제 연결 종료(게임 크래시 시뮬레이션) 후
        /// 같은 방에 다시 입장할 수 있어야 한다.
        ///
        /// 조건: 방에 다른 플레이어(게스트)가 남아 있어 SocketServer 방이 유지됨.
        /// gamesession:player 키는 2시간 TTL → 재접속에 사용 가능.
        /// </summary>
        [UnityTest]
        public IEnumerator 강제_연결_끊김_후_재접속_성공() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();

            // 게스트 먼저 입장 (방 유지 앵커)
            var guestCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());
            // 호스트 첫 번째 입장
            var hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                // 호스트 강제 종료 — C_PlayerLeave 없이 TCP 연결만 끊는다 (크래시 시뮬레이션)
                await hostCollector.DisposeAsync();
                hostCollector = null;

                // 서버가 TCP 연결 끊김을 감지할 시간을 준다
                await UniTask.Delay(TimeSpan.FromMilliseconds(600));

                // 호스트 재접속 시도 — gamesession:player 키가 유효하고
                // 게스트가 방을 유지 중이므로 재입장 가능해야 한다
                hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

                Assert.IsNotNull(hostCollector, "강제 끊김 후 재접속이 성공해야 한다");
            }
            finally
            {
                if (hostCollector != null)  await hostCollector.DisposeAsync();
                await guestCollector.DisposeAsync();
            }
        });

        /// <summary>
        /// SocketSession 이 Disconnected 상태가 되어도 재접속 루프가
        /// 무한 대기에 빠지지 않고 다음 시도를 진행해야 한다.
        ///
        /// 시나리오:
        ///   1회 시도: TCP 연결 직후 강제 종료 → Session.State = Disconnected
        ///   2회 시도: 정상 입장 → Joined
        /// </summary>
        [UnityTest]
        public IEnumerator Disconnected_상태에서_재시도로_입장_성공() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();

            // 게스트가 방을 유지
            var guestCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                // --- 1차 시도: 연결 후 즉시 Disconnect (응답 대기 없이 끊음) ---
                var state1    = new SocketPacketState();
                var connector1 = new SocketConnector();
                var dispatcher1 = new SocketPacketDispatcher(new IPacketHandler[]
                    { new PlayerJoinedPacketHandler(state1), new MovePacketHandler(state1) });
                var session1 = new SocketSession(connector1, dispatcher1);

                await session1.ConnectAsync(
                    new SocketConnectionInfo(ServerConfig.SocketServerHost, ServerConfig.SocketServerPort,
                        room.RoomId, room.HostUserId), Timeout());

                // JoinRoomAsync 없이 강제 Disconnect → State = Disconnected
                await session1.DisconnectAsync(CancellationToken.None);
                await connector1.DisposeAsync();

                Assert.AreEqual(SocketSessionState.Disconnected, session1.State,
                    "강제 종료 후 Disconnected 상태여야 한다");

                // --- 2차 시도: 정상 입장 ---
                // Disconnected 상태에서 새 세션을 만들어 재시도하면 성공해야 한다
                var hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
                Assert.IsNotNull(hostCollector, "Disconnected 이후 재시도로 입장 성공해야 한다");
                await hostCollector.DisposeAsync();
            }
            finally
            {
                await guestCollector.DisposeAsync();
            }
        });

        [UnityTest]
        public IEnumerator 게스트_부분퇴장후_재로그인시_방복원_안되고_호스트는_유지() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();
            var hostCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());
            var guestCollector = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());

            try
            {
                // 게스트만 명시적 퇴장 (방엔 호스트가 남음 = 부분 퇴장)
                await guestCollector.SendAsync(new C_PlayerLeave(), Timeout());

                // 호스트가 S_PlayerLeft(게스트) 수신 — 퇴장 전파 확인
                await hostCollector.WaitForPacketAsync<S_PlayerLeft>(
                    packet => packet.UserId == room.GuestUserId, Timeout());

                // GameServer가 PlayerLeft를 소비해 게스트 association을 제거할 때까지 폴링
                // (재로그인 응답의 CurrentRoomId==0 이면 복원 안 됨).
                long guestRoomId = -1;
                var deadline = DateTime.UtcNow.AddSeconds(ServerConfig.TimeoutSeconds);
                while (DateTime.UtcNow < deadline)
                {
                    guestRoomId = await ReloginCurrentRoomIdAsync(room.GuestEmail, "socket-e2e-guest-device", Timeout());
                    if (guestRoomId == 0) break;
                    await UniTask.Delay(TimeSpan.FromMilliseconds(200));
                }

                Assert.AreEqual(0, guestRoomId, "퇴장한 게스트는 재로그인 시 방으로 복원되면 안 된다");

                // 남은 호스트는 여전히 그 방으로 복원 가능해야 한다 (방 유지).
                var hostRoomId = await ReloginCurrentRoomIdAsync(room.HostEmail, "socket-e2e-host-device", Timeout());
                Assert.AreEqual(room.RoomId, hostRoomId, "남은 호스트는 방 복원 가능해야 한다");
            }
            finally
            {
                await hostCollector.DisposeAsync();
                await guestCollector.DisposeAsync();
            }
        });

        // ── 6.4 재접속 유예 창(grace) — 복귀 플로우 Green/Red ───────────────
        //
        // 두 퇴장 경로:
        //   크래시/끊김(graceful)  → PlayerState 를 ReconnectGraceMs(60s) 보존 → 유예 내 재접속 = 복귀(GREEN)
        //   명시 퇴장(C_PlayerLeave) → 상태 즉시 제거(영구)                    → 재접속 거부(RED)
        // 경계:
        //   유예 만료(>60s)        → 스윕이 상태 제거                          → 재접속 거부(RED, slow)
        //   전원 끊김(방 빔)        → 방 즉시 소멸                              → 재접속 거부(RED, Room not found)
        // 상세 = docs/wiki/codemap.md §2.17.

        /// <summary>[GREEN] 크래시(graceful) 후 유예 내 재접속 시, 스폰이 아니라 끊기기 직전 위치로 복귀한다.</summary>
        [UnityTest]
        public IEnumerator 크래시_후_유예내_재접속하면_이동한_위치가_보존되어_복귀한다() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();
            var guest = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout()); // 방 유지 앵커
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                const float px = 15f, pz = -8f;
                await host.SendAsync(new C_Move { PosX = px, PosY = 0, PosZ = pz, RotY = 0 }, Timeout());
                await UniTask.Delay(TimeSpan.FromMilliseconds(300)); // 서버 PlayerState 위치 갱신 대기

                // 크래시 — C_PlayerLeave 없이 TCP 강제 종료.
                await host.DisposeAsync();
                await UniTask.Delay(TimeSpan.FromMilliseconds(600)); // 서버 끊김 감지 → graceful 보존

                var reconnect = await ConnectAndSendJoinAsync(room.RoomId, room.HostUserId, Timeout());
                try
                {
                    var joined = await reconnect.WaitForPacketAsync<S_PlayerJoined>(
                        p => p.UserId == room.HostUserId || !p.Success, Timeout());

                    Assert.IsTrue(joined.Success, $"유예 내 재접속은 성공해야 한다: {joined.Message}");
                    Assert.AreEqual(px, joined.PosX, 0.01f, "보존된 X 위치로 복귀해야 한다(스폰 0 아님)");
                    Assert.AreEqual(pz, joined.PosZ, 0.01f, "보존된 Z 위치로 복귀해야 한다(스폰 0 아님)");
                }
                finally { await reconnect.DisposeAsync(); }
            }
            finally
            {
                await guest.DisposeAsync();
            }
        });

        /// <summary>[GREEN] 크래시 유예 중에는 퇴장 확정을 보류 — 남은 플레이어는 그 즉시 S_PlayerLeft 를 받지 않는다.</summary>
        [UnityTest]
        public IEnumerator 크래시_유예중에는_남은_플레이어에게_S_PlayerLeft가_즉시_오지_않는다() => UniTask.ToCoroutine(async () =>
        {
            // 대비: 명시 퇴장은 즉시 S_PlayerLeft(RawSocket_호스트가_퇴장하면_게스트가_S_PlayerLeft_수신).
            var room = await CreateStartedTwoPlayerRoomAsync();
            var guest = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                await host.DisposeAsync();                            // 크래시
                await UniTask.Delay(TimeSpan.FromSeconds(2));         // 서버 끊김 감지 + 여유

                Assert.IsFalse(
                    guest.TryGetLatest<S_PlayerLeft>(p => p.UserId == room.HostUserId, out _),
                    "유예 중에는 남은 플레이어에게 S_PlayerLeft 가 즉시 브로드캐스트되지 않아야 한다");
            }
            finally
            {
                await guest.DisposeAsync();
            }
        });

        /// <summary>[RED] 명시 퇴장(C_PlayerLeave) 후에는 상태가 즉시 제거되어 재접속이 거부된다(크래시와 대비).</summary>
        [UnityTest]
        public IEnumerator 명시퇴장_C_PlayerLeave_후_재접속하면_거부된다() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();
            var guest = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout()); // 방 유지
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                await host.SendAsync(new C_PlayerLeave(), Timeout());
                // 명시 퇴장은 즉시 확정 → 게스트가 S_PlayerLeft 받으면 상태 제거 완료가 보장된다.
                await guest.WaitForPacketAsync<S_PlayerLeft>(p => p.UserId == room.HostUserId, Timeout());
                await host.DisposeAsync();

                var reconnect = await ConnectAndSendJoinAsync(room.RoomId, room.HostUserId, Timeout());
                try
                {
                    var joined = await reconnect.WaitForPacketAsync<S_PlayerJoined>(
                        p => !p.Success || p.UserId == room.HostUserId, Timeout());
                    Assert.IsFalse(joined.Success, "명시 퇴장 후에는 재접속이 거부돼야 한다(상태 영구 제거)");
                }
                finally { await reconnect.DisposeAsync(); }
            }
            finally
            {
                await guest.DisposeAsync();
            }
        });

        /// <summary>[RED] 전원 끊겨 방이 비면 방은 즉시 제거 → 재접속은 거부된다(클라는 "방 종료" 팝업).</summary>
        [UnityTest]
        public IEnumerator 전원_끊기면_방이_사라지고_재접속은_거부된다() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();
            var guest = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            // 전원 크래시.
            await host.DisposeAsync();
            await guest.DisposeAsync();
            await UniTask.Delay(TimeSpan.FromSeconds(1)); // 서버가 양쪽 끊김 감지 → 빈 방 제거

            var reconnect = await ConnectAndSendJoinAsync(room.RoomId, room.HostUserId, Timeout());
            try
            {
                var joined = await reconnect.WaitForPacketAsync<S_PlayerJoined>(
                    p => !p.Success || p.UserId == room.HostUserId, Timeout());
                Assert.IsFalse(joined.Success, "전원 끊겨 방이 사라지면 재접속은 거부돼야 한다");
            }
            finally { await reconnect.DisposeAsync(); }
        });

        /// <summary>
        /// [RED · slow ~72s] 유예 창(60s) 만료 후엔 스윕이 상태를 제거 → 재접속 거부.
        /// 방 유지를 위해 게스트는 keepalive(C_Move)로 RoomPlayerTimeout(60s)을 회피(send-only라 수신 루프와 무관).
        /// 재접속 검증은 **새 단명 컬렉터**로 — 장수명 게스트는 70s간 몬스터 패킷 수백 개를 받아 취약.
        /// (유예 만료 시 S_PlayerLeft 발화·association 정리는 서버 단위테스트 `ReconnectGraceTests.유예_만료_스윕`이 검증.)
        /// 의도적으로 느린 테스트 — 유예 경계를 실제 시간으로 검증.
        /// </summary>
        [UnityTest]
        public IEnumerator 유예_만료후_재접속하면_거부된다() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();
            var guest = await ConnectAndJoinCollectorAsync(room.RoomId, room.GuestUserId, Timeout());
            var host = await ConnectAndJoinCollectorAsync(room.RoomId, room.HostUserId, Timeout());

            try
            {
                await host.DisposeAsync();                            // 크래시 → 유예 시작
                await UniTask.Delay(TimeSpan.FromMilliseconds(600));

                // ~72s 동안 게스트 keepalive(9s 간격) → 방 유지, 그동안 호스트 유예 만료(>60s) → 스윕이 호스트 상태 제거.
                for (int i = 0; i < 8; i++)
                {
                    await guest.SendAsync(new C_Move { PosX = 5, PosY = 0, PosZ = 5, RotY = 0 }, Timeout());
                    await UniTask.Delay(TimeSpan.FromSeconds(9));
                }

                // 유예 만료 후 fresh 재접속 → 호스트 상태 없음(스윕됨) → 거부.
                var reconnect = await ConnectAndSendJoinAsync(room.RoomId, room.HostUserId, Timeout());
                try
                {
                    var joined = await reconnect.WaitForPacketAsync<S_PlayerJoined>(
                        p => !p.Success || p.UserId == room.HostUserId, Timeout());
                    Assert.IsFalse(joined.Success, "유예 만료(60s 경과) 후 재접속은 거부돼야 한다(상태 스윕됨)");
                }
                finally { await reconnect.DisposeAsync(); }
            }
            finally
            {
                await guest.DisposeAsync();
            }
        });

        // ─────────────────────────────────────────────────────────────
        // 연결 생존성/거부 (liveness) — 하트비트·유휴 타임아웃·세션 검증
        // ─────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 세션배정_없는_UserId로_입장하면_거부된다() => UniTask.ToCoroutine(async () =>
        {
            // 게임 세션(gamesession:player:{id})에 배정된 적 없는 임의 UserId/RoomId → 서버가 입장 거부.
            // 소켓에는 별도 C_Auth 가 없고 C_PlayerJoin 의 Redis 검증이 인증을 대신한다 — 그 불변식을 고정.
            var collector = await ConnectAndSendJoinAsync(999_999_001L, 999_999_002L, Timeout());
            try
            {
                var resp = await collector.WaitForPacketAsync<S_PlayerJoined>(_ => true, Timeout());
                Assert.IsFalse(resp.Success, "세션 배정 없는 입장은 거부되어야 한다");
            }
            finally
            {
                await collector.DisposeAsync();
            }
        });

        /// <summary>
        /// 서버 유휴 타임아웃(HeartBeatService RoomPlayerTimeout=60s) 생존성 E2E.
        /// 핑 보내는 세션은 무이동 80s 후에도 Joined 유지(하트비트가 LastRecvAt 갱신),
        /// 핑 끈 세션은 서버가 끊고 → 클라가 OnDisconnected 로 감지. (~80s, 느린 liveness 테스트.)
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator 유휴시_핑있으면_연결유지_핑없으면_서버가_끊고_OnDisconnected가_발화한다() => UniTask.ToCoroutine(async () =>
        {
            var room = await CreateStartedTwoPlayerRoomAsync();

            // 호스트=기본 핑(15s)으로 생존, 게스트=핑 사실상 off → 60s 무패킷으로 서버가 퇴장.
            // ⚠️ 세션 수명 토큰은 CancellationToken.None — Timeout()(~짧은 토큰)을 넘기면 그 토큰이 세션을
            //    조기 종료시켜(연결 끊김) 80s 유휴를 관측할 수 없다. 연결 단계는 헬퍼 내부 deadline 으로 별도 보장.
            var host = await ConnectJoinedSessionAsync(room.RoomId, room.HostUserId, CancellationToken.None);
            var guest = await ConnectJoinedSessionAsync(room.RoomId, room.GuestUserId, CancellationToken.None, TimeSpan.FromHours(1));

            var guestDisconnected = false;
            guest.Session.OnDisconnected += () => guestDisconnected = true;

            try
            {
                // 서버 RoomPlayerTimeout(60s) + 체크주기(15s) 여유까지 무이동 유휴.
                await UniTask.Delay(TimeSpan.FromSeconds(80), ignoreTimeScale: true);

                Assert.IsTrue(guestDisconnected, "핑 없는 세션은 서버 유휴 타임아웃으로 끊겨 OnDisconnected 가 발화해야 한다");
                Assert.AreEqual(SocketSessionState.Disconnected, guest.Session.State, "끊긴 세션 상태=Disconnected");
                Assert.AreEqual(SocketSessionState.Joined, host.Session.State, "핑 보내는 세션은 Joined 유지(하트비트 생존)");
            }
            finally
            {
                await host.Session.DisconnectAsync(CancellationToken.None);
                await guest.Session.DisconnectAsync(CancellationToken.None);
            }
        });

        /// <summary>
        /// 원시 재접속 시도 — 연결 후 C_PlayerJoin 만 보낸 컬렉터를 반환한다(성공/실패 응답을 호출자가 검사).
        /// ConnectAndJoinCollectorAsync 와 달리 실패 응답에 재시도하지 않는다 — 거부(RED) 케이스 검증용.
        /// </summary>
        private static async UniTask<SocketPacketCollector> ConnectAndSendJoinAsync(long roomId, long userId, CancellationToken ct)
        {
            var collector = new SocketPacketCollector();
            await collector.ConnectAsync(ServerConfig.SocketServerHost, ServerConfig.SocketServerPort, ct);
            await collector.SendAsync(new C_PlayerJoin { RoomId = roomId, UserId = userId }, ct);
            return collector;
        }

        private static async UniTask<long> ReloginCurrentRoomIdAsync(string email, string deviceId, CancellationToken ct)
        {
            var provider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);
            try
            {
                var authService = new AuthGrpcService(provider);
                var login = await authService.LoginAsync(new LoginRequest
                {
                    Email = email,
                    Password = "Test1234!",
                    DeviceId = deviceId
                }, ct);
                Assert.IsTrue(login.Result.Success, login.Result.Message);
                return login.User.CurrentRoomId;
            }
            finally
            {
                provider.Dispose();
            }
        }

        private static async UniTask<StartedRoomContext> CreateStartedTwoPlayerRoomAsync(string mapId = null)
        {
            var provider = new GrpcChannelProvider(ServerConfig.GameServerGrpcAddress);

            try
            {
                var authService = new AuthGrpcService(provider);
                var userService = new UserGrpcService(provider);
                var lobbyService = new DungeonLobbyGrpcService(provider);

                var hostEmail = UniqueEmail();
                var hostNickname = UniqueNickname("Host");

                var hostRegister = await authService.RegisterAsync(new RegisterRequest
                {
                    Email = hostEmail,
                    Password = "Test1234!"
                }, Timeout());
                Assert.IsTrue(hostRegister.Result.Success, hostRegister.Result.Message);

                var hostLogin = await authService.LoginAsync(new LoginRequest
                {
                    Email = hostEmail,
                    Password = "Test1234!",
                    DeviceId = "socket-e2e-host-device"
                }, Timeout());
                Assert.IsTrue(hostLogin.Result.Success, hostLogin.Result.Message);

                provider.AccessTokenProvider = () => hostLogin.AccessToken;

                var setHostNickname = await userService.SetNickNameAsync(new SetNicknameRequest
                {
                    Nickname = hostNickname
                }, Timeout());
                Assert.IsTrue(setHostNickname.Result.Success, setHostNickname.Result.Message);

                var hostUserId = ExtractUserIdFromAccessToken(hostLogin.AccessToken);

                var created = await lobbyService.CreateRoomAsync(new CreateRoomRequest
                {
                    RoomName = $"Socket E2E {hostNickname}",
                    MaxPlayers = 2
                }, Timeout());
                Assert.IsTrue(created.Result.Success, created.Result.Message);

                var guestEmail = UniqueEmail();
                var guestNickname = UniqueNickname("Guest");

                var guestRegister = await authService.RegisterAsync(new RegisterRequest
                {
                    Email = guestEmail,
                    Password = "Test1234!"
                }, Timeout());
                Assert.IsTrue(guestRegister.Result.Success, guestRegister.Result.Message);

                var guestLogin = await authService.LoginAsync(new LoginRequest
                {
                    Email = guestEmail,
                    Password = "Test1234!",
                    DeviceId = "socket-e2e-guest-device"
                }, Timeout());
                Assert.IsTrue(guestLogin.Result.Success, guestLogin.Result.Message);

                provider.AccessTokenProvider = () => guestLogin.AccessToken;

                var setGuestNickname = await userService.SetNickNameAsync(new SetNicknameRequest
                {
                    Nickname = guestNickname
                }, Timeout());
                Assert.IsTrue(setGuestNickname.Result.Success, setGuestNickname.Result.Message);

                var guestUserId = ExtractUserIdFromAccessToken(guestLogin.AccessToken);

                var joined = await lobbyService.JoinRoomAsync(new JoinRoomRequest
                {
                    RoomId = created.RoomInfo.RoomId
                }, Timeout());
                Assert.IsTrue(joined.Result.Success, joined.Result.Message);

                provider.AccessTokenProvider = () => hostLogin.AccessToken;

                var started = await lobbyService.StartRoomAsync(new StartRoomRequest
                {
                    RoomId = created.RoomInfo.RoomId,
                    MapId = mapId ?? ""  // 비우면 서버 기본 맵(dungeon_01). test_arena 등 지정 시 그 맵으로 시작.
                }, Timeout());
                Assert.IsTrue(started.Result.Success, started.Result.Message);

                return new StartedRoomContext(
                    created.RoomInfo.RoomId,
                    hostUserId,
                    guestUserId,
                    hostNickname,
                    guestNickname,
                    hostEmail,
                    guestEmail,
                    hostLogin.AccessToken);
            }
            finally
            {
                provider.Dispose();
            }
        }

        private static async UniTask<SocketClientContext> ConnectJoinedSessionAsync(long roomId, long userId, CancellationToken ct, TimeSpan? heartbeatInterval = null)
        {
            Exception lastError = null;
            var deadline = DateTime.UtcNow.AddSeconds(ServerConfig.TimeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                var state = new SocketPacketState();
                var connector = new SocketConnector();
                var dispatcher = new SocketPacketDispatcher(new IPacketHandler[]
                {
                    new PlayerJoinedPacketHandler(state),
                    new MovePacketHandler(state)
                });
                var session = new SocketSession(connector, dispatcher);
                if (heartbeatInterval.HasValue) session.HeartbeatInterval = heartbeatInterval.Value;

                try
                {
                    await session.ConnectAsync(
                        new SocketConnectionInfo(ServerConfig.SocketServerHost, ServerConfig.SocketServerPort, roomId, userId),
                        ct);

                    await session.JoinRoomAsync(ct);
                    await UniTask.WaitUntil(
                        () => state.TryGetPlayer(userId, out _) || session.State == SocketSessionState.Failed,
                        cancellationToken: ct);

                    if (!state.TryGetPlayer(userId, out _))
                    {
                        throw new InvalidOperationException("Player join did not complete successfully.");
                    }

                    return new SocketClientContext(session, connector, state);
                }
                catch (Exception ex)
                {
                    lastError = ex;

                    try
                    {
                        await session.DisconnectAsync(CancellationToken.None);
                    }
                    catch
                    {
                    }

                    await connector.DisposeAsync();
                    await UniTask.Delay(TimeSpan.FromMilliseconds(200), cancellationToken: ct);
                }
            }

            throw new InvalidOperationException("Failed to connect and join socket session.", lastError);
        }

        private static async UniTask<SocketPacketCollector> ConnectAndJoinCollectorAsync(long roomId, long userId, CancellationToken ct)
        {
            Exception lastError = null;
            var deadline = DateTime.UtcNow.AddSeconds(ServerConfig.TimeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                var collector = new SocketPacketCollector();

                try
                {
                    await collector.ConnectAsync(ServerConfig.SocketServerHost, ServerConfig.SocketServerPort, ct);
                    await collector.SendAsync(new C_PlayerJoin { RoomId = roomId, UserId = userId }, ct);

                    // 실패 응답(Success=false)은 UserId=0으로 오므로 UserId 매칭만으로는
                    // 관측되지 않는다. 방 생성(스트림 소비)과 클라 접속 사이 레이스로
                    // "Room not found"가 올 수 있어, 실패 패킷도 관측해 재시도 경로를 태운다.
                    var joined = await collector.WaitForPacketAsync<S_PlayerJoined>(
                        packet => packet.UserId == userId || !packet.Success,
                        ct);

                    if (!joined.Success)
                    {
                        throw new InvalidOperationException($"Join failed: {joined.Message}");
                    }

                    return collector;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    await collector.DisposeAsync();
                    await UniTask.Delay(TimeSpan.FromMilliseconds(200), cancellationToken: ct);
                }
            }

            throw new InvalidOperationException("Failed to connect and join raw socket client.", lastError);
        }

        private static long ExtractUserIdFromAccessToken(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException("Access token is empty.", nameof(accessToken));
            }

            var parts = accessToken.Split('.');
            if (parts.Length < 2)
            {
                throw new InvalidOperationException("Access token payload is invalid.");
            }

            var payload = parts[1].Replace('-', '+').Replace('_', '/');

            switch (payload.Length % 4)
            {
                case 2:
                    payload += "==";
                    break;
                case 3:
                    payload += "=";
                    break;
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var match = Regex.Match(json, "\"sub\"\\s*:\\s*\"(?<id>\\d+)\"");

            if (!match.Success)
            {
                throw new InvalidOperationException("User id claim was not found in access token.");
            }

            return long.Parse(match.Groups["id"].Value);
        }

        private sealed class SocketPacketCollector
        {
            private readonly List<Packet> _receivedPackets = new List<Packet>();
            private readonly object _sync = new object();
            private readonly SocketConnector _connector = new SocketConnector();

            private CancellationTokenSource _receiveCts;
            private UniTask _receiveLoop;

            public async UniTask ConnectAsync(string host, int port, CancellationToken ct)
            {
                _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                await _connector.ConnectAsync(host, port, _receiveCts.Token);
                _receiveLoop = _connector.StartReceiveLoopAsync(OnPacketAsync, _receiveCts.Token);
                _receiveLoop.Forget(ex =>
                {
                    if (!IsExpectedDisconnectException(ex))
                    {
                        UnityEngine.Debug.LogException(ex);
                    }
                });
            }

            public UniTask SendAsync(Packet packet, CancellationToken ct)
            {
                return _connector.SendAsync(packet, ct);
            }

            public async UniTask<TPacket> WaitForPacketAsync<TPacket>(Func<TPacket, bool> predicate, CancellationToken ct)
                where TPacket : Packet
            {
                await UniTask.WaitUntil(() => TryFindPacket(predicate, out TPacket _), cancellationToken: ct);
                TryFindPacket(predicate, out TPacket packet);
                return packet;
            }

            public async UniTask DisposeAsync()
            {
                _receiveCts?.Cancel();
                _receiveCts?.Dispose();
                await _connector.DisposeAsync();
            }

            private UniTask OnPacketAsync(Packet packet)
            {
                lock (_sync)
                {
                    _receivedPackets.Add(packet);
                }

                return UniTask.CompletedTask;
            }

            private bool TryFindPacket<TPacket>(Func<TPacket, bool> predicate, out TPacket found)
                where TPacket : Packet
            {
                lock (_sync)
                {
                    foreach (var packet in _receivedPackets)
                    {
                        if (packet is TPacket typed && predicate(typed))
                        {
                            found = typed;
                            return true;
                        }
                    }
                }

                found = null;
                return false;
            }

            /// <summary>조건에 맞는 가장 최근 패킷(역순 탐색). 움직이는 몬스터의 최신 위치 조회용.</summary>
            public bool TryGetLatest<TPacket>(Func<TPacket, bool> predicate, out TPacket found)
                where TPacket : Packet
            {
                lock (_sync)
                {
                    for (int i = _receivedPackets.Count - 1; i >= 0; i--)
                    {
                        if (_receivedPackets[i] is TPacket typed && predicate(typed))
                        {
                            found = typed;
                            return true;
                        }
                    }
                }

                found = null;
                return false;
            }

            private static bool IsExpectedDisconnectException(Exception exception)
            {
                if (exception is OperationCanceledException)
                {
                    return true;
                }

                if (exception is ObjectDisposedException)
                {
                    return true;
                }

                if (exception is IOException ioException && ioException.InnerException is SocketException)
                {
                    return true;
                }

                return exception is SocketException;
            }
        }

        private sealed class StartedRoomContext
        {
            public long RoomId { get; }
            public long HostUserId { get; }
            public long GuestUserId { get; }
            public string HostNickname { get; }
            public string GuestNickname { get; }
            public string HostEmail { get; }
            public string GuestEmail { get; }
            public string HostAccessToken { get; }

            public StartedRoomContext(
                long roomId,
                long hostUserId,
                long guestUserId,
                string hostNickname,
                string guestNickname,
                string hostEmail,
                string guestEmail,
                string hostAccessToken)
            {
                RoomId = roomId;
                HostUserId = hostUserId;
                GuestUserId = guestUserId;
                HostNickname = hostNickname;
                GuestNickname = guestNickname;
                HostEmail = hostEmail;
                GuestEmail = guestEmail;
                HostAccessToken = hostAccessToken;
            }
        }

        private sealed class SocketClientContext
        {
            public ISocketSession Session { get; }
            public SocketConnector Connector { get; }
            public SocketPacketState State { get; }

            public SocketClientContext(ISocketSession session, SocketConnector connector, SocketPacketState state)
            {
                Session = session;
                Connector = connector;
                State = state;
            }
        }
    }
}
