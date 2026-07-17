using System.Linq;
using Game.Network.Socket.Diagnostics;
using NUnit.Framework;

namespace Game.Tests.EditMode.Socket
{
    /// <summary>
    /// AC-C1b: 클라 전투 트레이스 링버퍼. 설계 = combat-diagnostics.md §2.3/§5.
    /// 검증 4축: ① 기본 Off ② 링 회전(최신 N 유지) ③ 구간 계산 ④ 판정(서버 결과) 병합.
    /// </summary>
    [TestFixture]
    public class CombatTraceRecorderTests
    {
        private const int Actor = 100;   // 플레이어 ActorId = +UserId
        private const int Monster = -7;  // 몬스터 ActorId = -InstanceId
        private const int NetId = 2;     // combo_a

        [Test]
        public void 기본은_Off라_아무것도_기록하지_않는다()
        {
            var r = new CombatTraceRecorder();

            r.RecordAttackSent(1000, Actor, NetId);
            r.RecordDamageReceived(1010, Actor, Monster, -27);

            Assert.IsFalse(r.Enabled, "상시 기록 금지 — 기본값은 Off 여야 한다");
            Assert.AreEqual(0, r.Count);
            Assert.AreEqual(0, r.Total);
            Assert.IsEmpty(r.Snapshot());
        }

        [Test]
        public void Off로_되돌리면_그_뒤_기록이_멈춘다()
        {
            var r = new CombatTraceRecorder { Enabled = true };
            r.RecordAttackSent(1000, Actor, NetId);

            r.Enabled = false;
            r.RecordAttackSent(1100, Actor, NetId);

            Assert.AreEqual(1, r.Count, "Off 이후의 기록은 무시돼야 한다");
        }

        [Test]
        public void 용량을_넘으면_링이_돌아_최신_N건만_남는다()
        {
            var r = new CombatTraceRecorder { Enabled = true };

            int over = CombatTraceRecorder.Capacity + 10;
            for (int i = 0; i < over; i++)
                r.RecordAttackSent(timeMs: i, Actor, networkId: i);

            Assert.AreEqual(CombatTraceRecorder.Capacity, r.Count, "보관은 Capacity 에서 포화");
            Assert.AreEqual(over, r.Total, "Total 은 덮어쓴 것까지 세어 유실 여부를 알린다");

            var snap = r.Snapshot();
            // 스냅샷은 오래된 것부터 = 가장 오래된 10건이 밀려나간 상태.
            Assert.AreEqual(10, snap[0].TimeMs, "가장 오래된 10건이 덮여야 한다");
            Assert.AreEqual(over - 1, snap[snap.Length - 1].TimeMs, "마지막은 가장 최근 기록");
        }

        [Test]
        public void Clear는_보관과_누적을_모두_비운다()
        {
            var r = new CombatTraceRecorder { Enabled = true };
            r.RecordAttackSent(1000, Actor, NetId);

            r.Clear();

            Assert.AreEqual(0, r.Count);
            Assert.AreEqual(0, r.Total);
        }

        [Test]
        public void 한_스윙의_구간이_계산되고_서버_판정이_병합된다()
        {
            // 실제 순서: C_Attack 송신 → S_AbilityActivated → S_ApplyEffect(서버 권위 데미지) → S_MonsterState(HP 반영)
            var r = new CombatTraceRecorder { Enabled = true };
            r.RecordAttackSent(1000, Actor, NetId);
            r.RecordAbilityActivated(1014, Actor, NetId);
            r.RecordDamageReceived(1015, Actor, Monster, amount: -27);
            r.RecordMonsterHpApplied(1019, Monster, hp: 3, seq: 41, amount: -27); // HP 가 실제로 변해야 t_apply 다(델타 0=이동은 링에 없다)

            var rec = CombatTraceJoin.Build(r.Snapshot()).Single();

            Assert.AreEqual(Actor, rec.ActorId);
            Assert.AreEqual(Monster, rec.TargetId);
            Assert.AreEqual(NetId, rec.NetworkId);

            // ③ 구간
            Assert.AreEqual(14, rec.ActivateRoundTripMs, "송신→발동 통지 왕복");
            Assert.AreEqual(19, rec.SendToHpMs, "체감 지연의 본체(송신→HP 반영)");
            Assert.AreEqual(4, rec.DamageToHpMs, "데미지 도착→반영 = 클라 구간");

            // ④ 서버 판정 병합
            Assert.AreEqual(27, rec.FinalDamage, "Amount(음수 델타) → 표시용 양수");
            Assert.AreEqual(3, rec.HpAfter);
            Assert.AreEqual(41, rec.Seq, "서버 로그와 조인하는 상관키");
            Assert.IsFalse(rec.LikelyGated);
        }

        [Test]
        public void 발동_통지가_없으면_게이트_의심으로_표시된다()
        {
            // "왜 공격이 안 나갔나" — 서버가 쿨다운/마나/콤보로 거부하면 S_AbilityActivated 가 오지 않는다.
            var r = new CombatTraceRecorder { Enabled = true };
            r.RecordAttackSent(1000, Actor, NetId);

            var rec = CombatTraceJoin.Build(r.Snapshot()).Single();

            Assert.IsTrue(rec.LikelyGated, "발동 통지 부재 = 서버 gate 에 막혔을 가능성");
            Assert.AreEqual(-1, rec.ActivateRoundTripMs);
            Assert.AreEqual(-1, rec.SendToHpMs, "미완결 구간은 -1(0 이면 '즉시'로 오독된다)");
        }

        [Test]
        public void 다른_몬스터의_틱_갱신은_이_스윙에_귀속되지_않는다()
        {
            // 틱은 매 순간 무관한 몬스터의 S_MonsterState 를 흘린다 → 그걸 t_apply 로 오인하면 구간이 거짓이 된다.
            var r = new CombatTraceRecorder { Enabled = true };
            r.RecordAttackSent(1000, Actor, NetId);
            r.RecordAbilityActivated(1010, Actor, NetId);
            r.RecordMonsterHpApplied(1012, targetId: -99, hp: 50, seq: 7, amount: 0); // 무관한 몬스터의 이동 틱
            r.RecordDamageReceived(1015, Actor, Monster, amount: -27);
            r.RecordMonsterHpApplied(1019, Monster, hp: 3, seq: 41, amount: -27);

            var rec = CombatTraceJoin.Build(r.Snapshot()).Single();

            Assert.AreEqual(19, rec.SendToHpMs, "데미지가 지목한 대상의 HP 반영만 귀속돼야 한다");
            Assert.AreEqual(3, rec.HpAfter);
        }

        [Test]
        public void 몬스터_피격은_S_ApplyEffect_없이_HP델타로_귀속된다()
        {
            // ⚠ 실제 서버 동작: 몬스터 피해는 S_ApplyEffect 로 오지 않는다(그건 플레이어가 대상일 때만).
            //   서버가 몬스터 HP 를 권위로 깎아 S_MonsterState 로만 보낸다 → HP 델타가 유일한 데미지 신호.
            //   이게 던전의 주 시나리오(= "몬스터 HP 동기화가 느리다"는 원 관측)이므로 반드시 병합돼야 한다.
            var r = new CombatTraceRecorder { Enabled = true };
            r.RecordAttackSent(1000, Actor, NetId);
            r.RecordAbilityActivated(1014, Actor, NetId);
            r.RecordMonsterHpApplied(1031, Monster, hp: 3, seq: 41, amount: -27); // 데미지 신호 = 델타

            var rec = CombatTraceJoin.Build(r.Snapshot()).Single();

            Assert.AreEqual(Monster, rec.TargetId, "델타가 있는 몬스터가 이 스윙의 대상");
            Assert.AreEqual(27, rec.FinalDamage);
            Assert.AreEqual(3, rec.HpAfter);
            Assert.AreEqual(41, rec.Seq);
            Assert.AreEqual(31, rec.SendToHpMs, "체감 지연의 본체가 계산돼야 한다");
            Assert.IsFalse(rec.LikelyGated);
        }

        [Test]
        public void 델타없는_이동틱은_대상으로_오인되지_않는다()
        {
            // 추격 중인 몬스터는 매 틱 위치만 바뀐 S_MonsterState(델타 0)를 흘린다 → 대상으로 잡히면 구간이 거짓이 된다.
            var r = new CombatTraceRecorder { Enabled = true };
            r.RecordAttackSent(1000, Actor, NetId);
            r.RecordMonsterHpApplied(1005, targetId: -99, hp: 50, seq: 7, amount: 0); // 이동만
            r.RecordMonsterHpApplied(1031, Monster, hp: 3, seq: 41, amount: -27);     // 진짜 피격

            var rec = CombatTraceJoin.Build(r.Snapshot()).Single();

            Assert.AreEqual(Monster, rec.TargetId, "델타 0 인 이동 갱신은 배제돼야 한다");
            Assert.AreEqual(31, rec.SendToHpMs);
        }

        [Test]
        public void 연속_스윙은_각각_별개의_구간으로_분리된다()
        {
            var r = new CombatTraceRecorder { Enabled = true };
            r.RecordAttackSent(1000, Actor, 2);
            r.RecordAbilityActivated(1010, Actor, 2);
            r.RecordAttackSent(1300, Actor, 3);          // 다음 스윙 시작 → 앞 스윙 구간은 닫힌다
            r.RecordAbilityActivated(1312, Actor, 3);

            var recs = CombatTraceJoin.Build(r.Snapshot());

            Assert.AreEqual(2, recs.Count);
            Assert.AreEqual(10, recs[0].ActivateRoundTripMs);
            Assert.AreEqual(12, recs[1].ActivateRoundTripMs);
        }

        [Test]
        public void 스테일_드롭이_기록된다_AC_C3_관측()
        {
            // Seq 로 버린 사건 자체가 "순서 역전이 실재했다"는 증거 — C1c 측정에서 빈도를 본다.
            var r = new CombatTraceRecorder { Enabled = true };
            r.RecordStaleDropped(1020, Monster, droppedSeq: 40, currentSeq: 41);

            var e = r.Snapshot().Single();

            Assert.AreEqual(CombatTraceKind.StaleDropped, e.Kind);
            Assert.AreEqual(Monster, e.TargetId);
            Assert.AreEqual(40, e.Seq);
        }

        [Test]
        public void 다른_플레이어의_스윙도_기록된다()
        {
            // 회귀: 초기 Join 은 AttackSent(=내 캐릭터만 남긴다)에서만 레코드를 만들어 **내 스윙만 보였다**.
            // 다른 플레이어의 입력 시각은 알 수 없지만, 서버 발동 통지부터는 관측 가능하다.
            const long Other = 200;
            var r = new CombatTraceRecorder { Enabled = true };
            r.RecordAttackSent(1000, Actor, NetId);                                   // 내 스윙
            r.RecordAbilityActivated(1012, Actor, NetId);
            r.RecordAbilityActivated(1100, Other, NetId);                             // 다른 플레이어 스윙
            r.RecordMonsterHpApplied(1130, Monster, hp: 20, seq: 9, amount: -15);

            var recs = CombatTraceJoin.Build(r.Snapshot(), localActorId: Actor);

            Assert.AreEqual(2, recs.Count, "내 스윙 + 다른 플레이어 스윙이 모두 나와야 한다");
            var mine = recs.Single(x => x.ActorId == Actor);
            var theirs = recs.Single(x => x.ActorId == Other);

            Assert.AreEqual(SwingOrigin.LocalPlayer, mine.Origin);
            Assert.AreEqual(SwingOrigin.RemotePlayer, theirs.Origin);
            Assert.AreEqual(-1, theirs.SentMs, "다른 플레이어의 입력 시각은 알 수 없다");
            Assert.AreEqual(30, theirs.ActivateToHpMs, "발동→HP 반영은 관측 가능하다");
            Assert.AreEqual(15, theirs.FinalDamage);
            Assert.IsFalse(theirs.LikelyGated, "원격은 발동 통지가 곧 시작점이라 거부가 정의상 보이지 않는다");
        }

        [Test]
        public void 몬스터의_공격도_기록된다()
        {
            // 몬스터→플레이어: 서버 틱이 S_AbilityActivated(ActorId<0) + S_ApplyEffect(SourceId=몬스터) 를 보낸다.
            var r = new CombatTraceRecorder { Enabled = true };
            r.RecordAbilityActivated(2000, Monster, networkId: 101);
            r.RecordDamageReceived(2018, Monster, targetId: Actor, amount: -9);

            var rec = CombatTraceJoin.Build(r.Snapshot(), localActorId: Actor).Single();

            Assert.AreEqual(SwingOrigin.Monster, rec.Origin);
            Assert.AreEqual(Monster, rec.ActorId);
            Assert.AreEqual(Actor, rec.TargetId);
            Assert.AreEqual(9, rec.FinalDamage);
            Assert.AreEqual(-1, rec.SendToHpMs, "몬스터 스윙엔 내 송신 시각이 없다");
        }

        [Test]
        public void 모든_몬스터의_동기화가_집계된다_한대도_안맞아도()
        {
            // 동기화 검수: 스윙과 무관하게 상태를 받은 모든 몬스터가 나와야 한다.
            var r = new CombatTraceRecorder { Enabled = true };
            r.RecordMonsterHpApplied(1000, -7, hp: 30, seq: 1, amount: 0);   // 이동만(피해 0)
            r.RecordMonsterHpApplied(1100, -7, hp: 18, seq: 2, amount: -12); // 피격
            r.RecordStaleDropped(1105, -7, droppedSeq: 1, currentSeq: 2);    // 순서 역전 방어
            r.RecordMonsterHpApplied(1200, -8, hp: 50, seq: 1, amount: 0);   // 한 대도 안 맞은 몬스터

            var stats = r.MonsterSync();

            Assert.AreEqual(2, stats.Count, "상태를 받은 몬스터는 전부 나와야 한다");

            var m7 = stats.Single(s => s.InstanceId == 7);
            Assert.AreEqual(18, m7.LastHp);
            Assert.AreEqual(2, m7.LastSeq);
            Assert.AreEqual(2, m7.Updates, "이동 틱도 갱신 수에는 포함된다(집계는 모든 갱신을 본다)");
            Assert.AreEqual(1, m7.StaleDrops, "AC-C3 가 막아낸 순서 역전");
            Assert.AreEqual(12, m7.TotalDamage, "누적 피해 = 서버 final 합과 대조할 값");

            var m8 = stats.Single(s => s.InstanceId == 8);
            Assert.AreEqual(0, m8.TotalDamage);
            Assert.AreEqual(0, m8.StaleDrops);
            Assert.AreEqual(1, m8.Updates, "이동만 한 몬스터도 사라지면 안 된다(모든 몬스터 검수)");
        }

        [Test]
        public void 이동만_하는_틱은_링을_채우지_않는다_C1c()
        {
            // C1c 측정에서 링 508건 중 451건(89%)이 이동 틱이라 정작 볼 스윙이 덮였다.
            // 이동 틱은 진단 가치가 없으므로 **링에는 넣지 않는다**(집계 맵에는 그대로 반영).
            var r = new CombatTraceRecorder { Enabled = true };

            for (int i = 0; i < 100; i++)
                r.RecordMonsterHpApplied(1000 + i, -7, hp: 30, seq: i + 1, amount: 0); // 이동만

            Assert.AreEqual(0, r.Count, "이동 틱은 링에 쌓이지 않아야 한다");
            Assert.AreEqual(100, r.MonsterSync().Single().Updates, "그래도 집계에는 전부 반영된다");
        }

        [Test]
        public void HP가_변한_틱은_링에_남는다_C1c()
        {
            // 필터가 과해서 정작 데미지까지 버리면 스윙 병합이 깨진다 — 그 반대 방향도 고정한다.
            var r = new CombatTraceRecorder { Enabled = true };

            r.RecordMonsterHpApplied(1000, -7, hp: 30, seq: 1, amount: 0);   // 이동 → 링 X
            r.RecordMonsterHpApplied(1100, -7, hp: 18, seq: 2, amount: -12); // 피격 → 링 O

            var e = r.Snapshot().Single();
            Assert.AreEqual(CombatTraceKind.MonsterHpApplied, e.Kind);
            Assert.AreEqual(-12, e.Amount);
            Assert.AreEqual(18, e.Hp);
        }

        [Test]
        public void 집계는_링이_돌아도_유실되지_않는다_C1c()
        {
            // 예전엔 집계를 링에서 유도해, 링이 도는 순간 앞부분이 조용히 사라졌다
            // (실측: m3 가 seq 234 인데 updates 185 = 49건 증발). 이제 집계는 링과 독립이다.
            var r = new CombatTraceRecorder { Enabled = true };

            int over = CombatTraceRecorder.Capacity + 50;
            for (int i = 0; i < over; i++)
                r.RecordMonsterHpApplied(1000 + i, -7, hp: 100 - i, seq: i + 1, amount: -1); // 전부 피격 = 전부 링에 들어감

            Assert.AreEqual(CombatTraceRecorder.Capacity, r.Count, "링은 포화된다");

            var s = r.MonsterSync().Single();
            Assert.AreEqual(over, s.Updates, "집계는 링 포화와 무관하게 전부 센다");
            Assert.AreEqual(over, s.TotalDamage, "누적 피해도 유실되면 데미지 검수가 거짓이 된다");
            Assert.AreEqual(over, s.LastSeq);
        }

        [Test]
        public void 스탯_기여분은_final과_base로_역산된다()
        {
            // 클라는 base(SO)와 final(서버 권위)만 안다 → AP-DEF 합만 역산 가능. 분해는 서버 로그와 조인.
            Assert.AreEqual(17, CombatTraceJoin.InferStatContribution(finalDamage: 27, baseDamage: 10));
        }
    }
}
