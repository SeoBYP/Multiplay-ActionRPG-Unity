using System.Collections.Generic;

namespace Game.Network.Socket.Diagnostics
{
    /// <summary>스윙을 누가 시작했나. 관측 가능한 구간이 달라진다(로컬만 t_send 를 안다).</summary>
    public enum SwingOrigin : byte
    {
        /// <summary>내 캐릭터 — C_Attack 송신 시각을 알아 **송신→HP 반영** 전 구간이 보인다.</summary>
        LocalPlayer = 0,
        /// <summary>다른 플레이어 — 그가 언제 눌렀는지는 알 수 없다. 관측은 S_AbilityActivated 수신부터.</summary>
        RemotePlayer = 1,
        /// <summary>몬스터(ActorId &lt; 0) — 서버 틱이 발동. 관측은 S_AbilityActivated 수신부터.</summary>
        Monster = 2,
    }

    /// <summary>
    /// 한 번의 스윙을 클라 관점으로 이어붙인 결과. <b>던전의 모든 액터</b>(내 캐릭터·다른 플레이어·몬스터)가 대상이다.
    /// </summary>
    public struct CombatTraceRecord
    {
        public SwingOrigin Origin;
        public long ActorId;
        public int NetworkId;
        public long TargetId;

        /// <summary>C_Attack 송신(t_send). <b>로컬 스윙만</b> 알 수 있다 — 원격/몬스터는 -1.</summary>
        public long SentMs;
        public long ActivatedMs;   // S_AbilityActivated 수신 (없으면 -1)
        public long DamageMs;      // 데미지 관측 (없으면 -1)
        public long HpAppliedMs;   // HP 반영 (없으면 -1)

        /// <summary>서버 권위 최종 데미지(양수). 미관측이면 0.</summary>
        public int FinalDamage;
        public int HpAfter;
        public int Seq;

        /// <summary>타임라인 기준점. 로컬은 송신 시각, 원격·몬스터는 발동 통지 수신 시각.</summary>
        public long StartMs => SentMs >= 0 ? SentMs : ActivatedMs;

        /// <summary>발동 왕복(로컬 전용): 송신 → 서버 게이트 통과 통지. -1 = 해당 없음/미수신.</summary>
        public long ActivateRoundTripMs => (SentMs < 0 || ActivatedMs < 0) ? -1 : ActivatedMs - SentMs;

        /// <summary>체감 지연의 본체(로컬 전용): 공격 입력 송신 → 대상 HP 반영. -1 = 해당 없음/미완결.</summary>
        public long SendToHpMs => (SentMs < 0 || HpAppliedMs < 0) ? -1 : HpAppliedMs - SentMs;

        /// <summary>발동 통지 → HP 반영. <b>모든 액터에서 관측 가능</b>해 원격/몬스터 스윙의 유일한 지연 지표다.</summary>
        public long ActivateToHpMs => (ActivatedMs < 0 || HpAppliedMs < 0) ? -1 : HpAppliedMs - ActivatedMs;

        /// <summary>데미지 관측 → HP 반영. 크면 클라 디스패치/표시 구간이 범인.</summary>
        public long DamageToHpMs => (DamageMs < 0 || HpAppliedMs < 0) ? -1 : HpAppliedMs - DamageMs;

        /// <summary>
        /// 내가 보냈는데 서버가 발동을 알리지 않았다 = 게이트(쿨다운·마나·콤보)에 막혔을 가능성.
        /// <b>로컬 스윙에만 의미가 있다</b> — 원격·몬스터는 발동 통지가 곧 관측 시작점이라 정의상 거부가 보이지 않는다.
        /// </summary>
        public bool LikelyGated => Origin == SwingOrigin.LocalPlayer && ActivatedMs < 0;
    }

    /// <summary>한 몬스터의 동기화 상태 집계(동기화 검수용). 스윙과 무관하게 <b>모든 몬스터</b>가 나온다.</summary>
    public struct MonsterSyncStat
    {
        public long ActorId;        // -InstanceId
        public int InstanceId;
        public int LastHp;
        public int LastSeq;
        public int Updates;         // 반영된 S_MonsterState 수
        public int StaleDrops;      // Seq 로 버린 수(AC-C3 가 막아낸 순서 역전)
        public int TotalDamage;     // 누적 HP 감소량 — 서버 데미지 합과 대조(데미지 검수)
    }

    /// <summary>
    /// 링버퍼의 원시 엔트리를 "스윙 단위"로 병합한다(AC-C1b). <b>순수 함수</b> — EditMode 로 검증한다.
    ///
    /// <para><b>모든 액터를 낸다</b>: 레코드의 시작점은 로컬이면 <c>AttackSent</c>, 원격 플레이어·몬스터면
    /// <c>AbilityActivated</c> 다(그들이 언제 눌렀는지는 클라가 알 방법이 없다 — 서버 통지가 첫 관측이다).
    /// 초기 구현은 <c>AttackSent</c> 만 시작점으로 봐서 **내 스윙만 보였다**.</para>
    ///
    /// <para><b>왜 클라가 산식 입력을 못 채우나(설계 정정)</b>: §2.4 상세 패널 초안은 <c>AP=시전자 AttackPower</c> 를
    /// 그렸지만, 그건 **서버 권위 스탯이라 클라에 오지 않는다**. §2.5 가 "서버 로그를 창으로 끌어오지 않는다"고
    /// 못박았으므로 둘은 동시에 성립할 수 없다. → 클라는 아는 것만 쓴다:
    /// <c>base</c>(AbilityDefinition SO) + <c>final</c>(S_ApplyEffect.Amount) 로 <b><c>AP-DEF = final - base</c> 를 역산</b>한다.</para>
    /// </summary>
    public static class CombatTraceJoin
    {
        /// <summary>스윙 하나에 속한 후속 이벤트로 볼 최대 지연. 이보다 늦게 온 건 다음 스윙의 것으로 본다.</summary>
        public const long CorrelationWindowMs = 2_000;

        /// <summary>ActorId 규약(진실원 = Shared.Gameplay <c>ActorIds</c>): 양수=플레이어 / 음수=몬스터 / 0=환경.
        /// Game.Network 는 <c>overrideReferences</c> 라 Shared.Gameplay.dll 이 안 걸려 있어 부호로 직접 판별한다.</summary>
        public static bool IsMonster(long actorId) => actorId < 0;

        /// <summary>
        /// 던전의 <b>모든</b> 스윙(내 캐릭터·다른 플레이어·몬스터)을 시간순 레코드로 만든다.
        /// </summary>
        /// <param name="localActorId">내 캐릭터의 ActorId(+UserId). 0 이면 로컬 판별 없이 전부 원격으로 본다.</param>
        public static List<CombatTraceRecord> Build(IReadOnlyList<CombatTraceEntry> entries, long localActorId = 0)
        {
            var records = new List<CombatTraceRecord>();
            if (entries == null) return records;

            // 로컬 송신에 이미 짝지어진 발동 통지 — 같은 스윙이 두 번 나오지 않도록 소비 표시.
            var consumedActivation = new HashSet<int>();

            // 1) 로컬 스윙: AttackSent 가 시작점 → 송신→발동 왕복까지 보인다.
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Kind != CombatTraceKind.AttackSent) continue;

                var sent = entries[i];
                var rec = NewRecord(SwingOrigin.LocalPlayer, sent.ActorId, sent.NetworkId);
                rec.SentMs = sent.TimeMs;

                Attach(entries, i + 1, sent.TimeMs, sent.ActorId, sent.NetworkId, ref rec, consumedActivation);
                records.Add(rec);
            }

            // 2) 원격 플레이어·몬스터: 발동 통지가 시작점(그들의 입력 시각은 클라가 알 수 없다).
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Kind != CombatTraceKind.AbilityActivated) continue;
                if (consumedActivation.Contains(i)) continue; // 로컬 스윙이 이미 가져감

                var act = entries[i];
                var origin = IsMonster(act.ActorId) ? SwingOrigin.Monster : SwingOrigin.RemotePlayer;
                var rec = NewRecord(origin, act.ActorId, act.NetworkId);
                rec.ActivatedMs = act.TimeMs;

                Attach(entries, i + 1, act.TimeMs, act.ActorId, act.NetworkId, ref rec, consumed: null);
                records.Add(rec);
            }

            records.Sort((a, b) => a.StartMs.CompareTo(b.StartMs));
            return records;
        }

        private static CombatTraceRecord NewRecord(SwingOrigin origin, long actorId, int networkId) => new CombatTraceRecord
        {
            Origin = origin,
            ActorId = actorId,
            NetworkId = networkId,
            SentMs = -1,
            ActivatedMs = -1,
            DamageMs = -1,
            HpAppliedMs = -1,
            TargetId = 0,
        };

        /// <summary>시작 시각 이후의 발동/데미지/HP 반영을 이 스윙에 이어붙인다.</summary>
        private static void Attach(
            IReadOnlyList<CombatTraceEntry> entries, int from, long startMs,
            long actorId, int networkId, ref CombatTraceRecord rec, HashSet<int> consumed)
        {
            for (int j = from; j < entries.Count; j++)
            {
                var e = entries[j];
                if (e.TimeMs - startMs > CorrelationWindowMs) break;

                // 같은 액터의 다음 스윙이 시작되면 이 스윙의 구간은 닫힌다.
                if ((e.Kind == CombatTraceKind.AttackSent || e.Kind == CombatTraceKind.AbilityActivated)
                    && e.ActorId == actorId && j != from - 1)
                {
                    bool isOwnActivation = e.Kind == CombatTraceKind.AbilityActivated
                                           && rec.ActivatedMs < 0 && e.NetworkId == networkId;
                    if (!isOwnActivation) break;
                }

                switch (e.Kind)
                {
                    case CombatTraceKind.AbilityActivated:
                        if (rec.ActivatedMs < 0 && e.ActorId == actorId && e.NetworkId == networkId)
                        {
                            rec.ActivatedMs = e.TimeMs;
                            consumed?.Add(j);
                        }
                        break;

                    case CombatTraceKind.DamageReceived:
                        // 플레이어가 대상인 피해(S_ApplyEffect) — SourceId 로 시전자가 특정된다.
                        if (rec.DamageMs < 0 && e.ActorId == actorId)
                        {
                            rec.DamageMs = e.TimeMs;
                            rec.TargetId = e.TargetId;
                            rec.FinalDamage = -e.Amount; // Amount 는 Health 델타(음수) → 표시용 양수
                        }
                        break;

                    case CombatTraceKind.MonsterHpApplied:
                        if (rec.HpAppliedMs >= 0) break;

                        if (rec.TargetId != 0)
                        {
                            if (e.TargetId != rec.TargetId) break;
                            rec.HpAppliedMs = e.TimeMs;
                            rec.HpAfter = e.Hp;
                            rec.Seq = e.Seq;
                        }
                        else if (e.Amount < 0 && !IsMonster(actorId))
                        {
                            // 플레이어→몬스터: 데미지가 S_ApplyEffect 로 오지 않고 **HP 델타가 유일한 신호**다.
                            // ⚠ 여러 플레이어가 동시에 때리면 이 델타의 주인을 클라는 구분할 수 없다(서버 로그로 확정).
                            rec.TargetId = e.TargetId;
                            rec.FinalDamage = -e.Amount;
                            rec.DamageMs = e.TimeMs;   // 이 경로는 데미지 통지와 HP 상태가 같은 패킷이라 두 시각이 같다.
                            rec.HpAppliedMs = e.TimeMs;
                            rec.HpAfter = e.Hp;
                            rec.Seq = e.Seq;
                        }
                        break;
                }
            }
        }

        // ※ BuildMonsterSync 는 제거됐다(C1c 측정 근거). 링에서 유도하면 링이 도는 순간 집계가 **조용히 유실**된다
        //   (실측: m3 가 seq 234 인데 updates 185 = 49건 증발). 집계는 몬스터당 1행이라 링에 둘 이유가 없다
        //   → CombatTraceRecorder.MonsterSync() 가 링과 독립인 맵으로 들고 있다.

        /// <summary>
        /// 서버가 쓴 산식의 스탯 기여분을 역산한다: <c>final - base = AP - DEF</c>.
        /// 클라는 base(SO 저작)와 final(서버 권위)만 아므로 여기까지가 한계 — 분해는 서버 로그와 조인.
        /// </summary>
        public static int InferStatContribution(int finalDamage, int baseDamage) => finalDamage - baseDamage;
    }
}
