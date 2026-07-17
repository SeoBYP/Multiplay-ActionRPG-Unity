using System;
using System.Collections.Generic;

namespace Game.Network.Socket.Diagnostics
{
    /// <summary>
    /// 한 번의 스윙을 클라 관점으로 이어붙인 결과(송신 → 발동 → 데미지 → HP 반영).
    /// 구간 delta 가 §2.1 표와 1:1 이고, 판정부는 §2.2 를 **클라가 아는 범위**로 채운다.
    /// </summary>
    public struct CombatTraceRecord
    {
        public long ActorId;
        public int NetworkId;
        public long TargetId;

        public long SentMs;        // t_send (C_Attack)
        public long ActivatedMs;   // S_AbilityActivated 수신 (없으면 -1)
        public long DamageMs;      // S_ApplyEffect 수신 (없으면 -1)
        public long HpAppliedMs;   // S_MonsterState 반영 (없으면 -1)

        /// <summary>서버 권위 최종 데미지(양수). 미수신이면 0.</summary>
        public int FinalDamage;
        public int HpAfter;
        public int Seq;

        /// <summary>발동 왕복: 送信 → 서버 게이트 통과 통지. -1 = 미수신(= 서버가 발동을 거부했을 수 있다).</summary>
        public long ActivateRoundTripMs => ActivatedMs < 0 ? -1 : ActivatedMs - SentMs;

        /// <summary>체감 지연의 본체: 공격 입력 송신 → 대상 HP 가 화면에 반영되기까지. -1 = 미완결.</summary>
        public long SendToHpMs => HpAppliedMs < 0 ? -1 : HpAppliedMs - SentMs;

        /// <summary>데미지 도착 → HP 반영. 크면 클라 디스패치/표시 구간이 범인.</summary>
        public long DamageToHpMs => (DamageMs < 0 || HpAppliedMs < 0) ? -1 : HpAppliedMs - DamageMs;

        /// <summary>서버가 발동을 알리지 않았다 = 게이트에 막혔을 가능성(쿨다운·마나·콤보 cadence). 서버 [CombatTrace] gate 와 대조한다.</summary>
        public bool LikelyGated => ActivatedMs < 0;
    }

    /// <summary>
    /// 링버퍼의 원시 엔트리를 "스윙 단위"로 병합한다(AC-C1b). <b>순수 함수</b> — EditMode 로 검증한다.
    ///
    /// <para><b>왜 클라가 산식 입력을 못 채우나(설계 정정)</b>: §2.4 상세 패널 초안은 <c>AP=시전자 AttackPower</c> 를
    /// 그렸지만, 그건 **서버 권위 스탯이라 클라에 오지 않는다**. §2.5 가 "서버 로그를 창으로 끌어오지 않는다"고
    /// 못박았으므로 둘은 동시에 성립할 수 없다. → 클라는 아는 것만 쓴다:
    /// <c>base</c>(AbilityDefinition SO) + <c>final</c>(S_ApplyEffect.Amount) 로 <b><c>AP-DEF = final - base</c> 를 역산</b>한다.
    /// 이것만으로 "왜 이 숫자인가"는 닫히고, AP/DEF 분해가 필요하면 <c>seq</c>·ActorId 로 서버 로그(Graylog)와 조인한다.</para>
    /// </summary>
    public static class CombatTraceJoin
    {
        /// <summary>스윙 하나에 속한 후속 이벤트로 볼 최대 지연. 이보다 늦게 온 건 다음 스윙의 것으로 본다.</summary>
        public const long CorrelationWindowMs = 2_000;

        /// <summary>
        /// 송신(AttackSent)을 기준으로 같은 <c>(ActorId, NetworkId)</c> 의 후속 이벤트를 시간창 안에서 이어붙인다.
        /// 데미지·HP 는 대상별로 갈리므로 **첫 대상**만 잇는다(다중 히트는 개별 엔트리로 링에 남아 있다).
        /// </summary>
        public static List<CombatTraceRecord> Build(IReadOnlyList<CombatTraceEntry> entries)
        {
            var records = new List<CombatTraceRecord>();
            if (entries == null) return records;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Kind != CombatTraceKind.AttackSent) continue;

                var sent = entries[i];
                var rec = new CombatTraceRecord
                {
                    ActorId = sent.ActorId,
                    NetworkId = sent.NetworkId,
                    SentMs = sent.TimeMs,
                    ActivatedMs = -1,
                    DamageMs = -1,
                    HpAppliedMs = -1,
                    TargetId = 0,
                };

                for (int j = i + 1; j < entries.Count; j++)
                {
                    var e = entries[j];
                    if (e.TimeMs - sent.TimeMs > CorrelationWindowMs) break;
                    // 다음 스윙이 시작되면 이 스윙의 구간은 닫힌다.
                    if (e.Kind == CombatTraceKind.AttackSent && e.ActorId == sent.ActorId) break;

                    switch (e.Kind)
                    {
                        case CombatTraceKind.AbilityActivated:
                            if (rec.ActivatedMs < 0 && e.ActorId == sent.ActorId && e.NetworkId == sent.NetworkId)
                                rec.ActivatedMs = e.TimeMs;
                            break;

                        case CombatTraceKind.DamageReceived:
                            if (rec.DamageMs < 0 && e.ActorId == sent.ActorId)
                            {
                                rec.DamageMs = e.TimeMs;
                                rec.TargetId = e.TargetId;
                                rec.FinalDamage = -e.Amount; // Amount 는 Health 델타(음수) → 표시용 양수
                            }
                            break;

                        case CombatTraceKind.MonsterHpApplied:
                            // 데미지가 지목한 대상의 HP 반영만 이 스윙에 귀속한다(다른 몬스터의 틱 갱신 제외).
                            if (rec.HpAppliedMs < 0 && rec.TargetId != 0 && e.TargetId == rec.TargetId)
                            {
                                rec.HpAppliedMs = e.TimeMs;
                                rec.HpAfter = e.Hp;
                                rec.Seq = e.Seq;
                            }
                            break;
                    }
                }

                records.Add(rec);
            }

            return records;
        }

        /// <summary>
        /// 서버가 쓴 산식의 스탯 기여분을 역산한다: <c>final - base = AP - DEF</c>.
        /// 클라는 base(SO 저작)와 final(서버 권위)만 아므로 여기까지가 한계 — 분해는 서버 로그와 조인.
        /// </summary>
        public static int InferStatContribution(int finalDamage, int baseDamage) => finalDamage - baseDamage;
    }
}
