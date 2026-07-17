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
            r.RecordMonsterHpApplied(1019, Monster, hp: 3, seq: 41);

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
            r.RecordMonsterHpApplied(1012, targetId: -99, hp: 50, seq: 7); // 무관한 몬스터
            r.RecordDamageReceived(1015, Actor, Monster, amount: -27);
            r.RecordMonsterHpApplied(1019, Monster, hp: 3, seq: 41);

            var rec = CombatTraceJoin.Build(r.Snapshot()).Single();

            Assert.AreEqual(19, rec.SendToHpMs, "데미지가 지목한 대상의 HP 반영만 귀속돼야 한다");
            Assert.AreEqual(3, rec.HpAfter);
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
        public void 스탯_기여분은_final과_base로_역산된다()
        {
            // 클라는 base(SO)와 final(서버 권위)만 안다 → AP-DEF 합만 역산 가능. 분해는 서버 로그와 조인.
            Assert.AreEqual(17, CombatTraceJoin.InferStatContribution(finalDamage: 27, baseDamage: 10));
        }
    }
}
