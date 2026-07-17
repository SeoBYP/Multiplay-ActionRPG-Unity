using System;

namespace Game.Network.Socket.Diagnostics
{
    /// <summary>클라가 관측할 수 있는 전투 사건의 종류. 서버 판정과 달리 **클라 관점**이다(combat-diagnostics.md §2.5).</summary>
    public enum CombatTraceKind : byte
    {
        /// <summary>C_Attack 송신(t_send).</summary>
        AttackSent = 0,
        /// <summary>S_AbilityActivated 수신 — 서버가 발동 게이트를 통과시켰다는 신호.</summary>
        AbilityActivated = 1,
        /// <summary>S_ApplyEffect 수신 — 서버 권위 데미지(Amount)가 도착.</summary>
        DamageReceived = 2,
        /// <summary>S_MonsterState 수신 → HP 반영(t_apply).</summary>
        MonsterHpApplied = 3,
        /// <summary>Seq 로 스테일을 버림(AC-C3). 순서 역전이 실제로 일어났다는 증거.</summary>
        StaleDropped = 4,
    }

    /// <summary>
    /// 전투 트레이스 1건. <b>구조체 + 사전할당 링버퍼</b>라 기록 시 할당이 없다(§2.3 오버헤드 요건).
    /// 문자열을 담지 않는 이유도 같다 — 어빌리티 이름은 표시 시점에 <c>networkId</c> 로 카탈로그에서 찾는다.
    /// </summary>
    public struct CombatTraceEntry
    {
        public CombatTraceKind Kind;
        public long TimeMs;

        /// <summary>발동자 ActorId(플레이어 +UserId / 몬스터 -InstanceId). 상관키.</summary>
        public long ActorId;
        /// <summary>대상 ActorId. 미상이면 0.</summary>
        public long TargetId;

        /// <summary>어빌리티 networkId. 상관키(송신↔발동↔데미지 join).</summary>
        public int NetworkId;

        /// <summary>서버 권위 Health 델타(S_ApplyEffect.Amount, 보통 음수). 해당 없으면 0.</summary>
        public int Amount;

        /// <summary>반영된 몬스터 HP. 해당 없으면 0.</summary>
        public int Hp;

        /// <summary>S_MonsterState.Seq — 서버 로그와 조인하는 키이자 스테일 판정 근거(AC-C3).</summary>
        public int Seq;
    }

    /// <summary>
    /// 클라 전투 트레이스 링버퍼(AC-C1b). **이게 단일 소스**이고 에디터 창(C1b')은 이 위의 뷰일 뿐이다.
    /// 설계 = <c>docs/wiki/combat-diagnostics.md</c> §2.3.
    ///
    /// <para><b>기본 Off.</b> <see cref="Enabled"/> 가 false 면 Record* 가 즉시 반환한다 —
    /// 링버퍼는 사전할당이고 엔트리는 구조체라 On 이어도 기록 자체에 할당이 없다.</para>
    ///
    /// <para><b>왜 순수 C# 인가</b>: UnityEngine 에 의존하지 않아야 EditMode 에서 시간·순서를 직접 넣어
    /// 링 회전·구간 계산을 검증할 수 있다. 시각은 호출부가 넘긴다(서버 <c>CombatTrace</c> 와 같은 규약).</para>
    ///
    /// <para><b>스레드</b>: 소켓 수신 스레드와 메인 스레드가 모두 기록할 수 있어 쓰기를 lock 으로 감싼다.
    /// Off 면 lock 조차 잡지 않는다.</para>
    /// </summary>
    public sealed class CombatTraceRecorder
    {
        /// <summary>보관 건수. 전투 수 초 분량이면 충분하고(재현 즉시 확인용), 512×32B ≈ 16KB 로 상주 비용도 무시할 만하다.</summary>
        public const int Capacity = 512;

        /// <summary>
        /// 런타임 기록·에디터 창이 공유하는 인스턴스.
        /// <para><b>왜 static 인가</b>: 기록자는 DI 로 닿지만 <b>에디터 창은 VContainer 스코프 밖</b>이라 같은 객체를 볼 방법이 없다.
        /// (스코프를 찾아 Resolve 하는 건 씬·스코프 이름에 결합돼 더 깨지기 쉽다.) 서버 <c>CombatTrace</c> 도 같은 이유로 static 이다.
        /// 진단 전용·기본 Off 라 부작용이 없고, 테스트는 <c>new CombatTraceRecorder()</c> 로 격리한다.</para>
        /// </summary>
        public static readonly CombatTraceRecorder Shared = new CombatTraceRecorder();

        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();

        /// <summary>단조 증가 클럭(ms). 구간 delta 전용이라 절대시각일 필요가 없고, <c>Time.time</c> 과 달리 스레드 어디서든 안전하다
        /// (소켓 수신 스레드가 기록한다 — Unity API 는 메인 스레드 전용이라 쓸 수 없다).</summary>
        public static long NowMs => Clock.ElapsedMilliseconds;

        private readonly CombatTraceEntry[] _ring = new CombatTraceEntry[Capacity];
        private readonly object _sync = new object();
        private int _next;    // 다음 쓸 위치
        private int _count;   // 채워진 건수(Capacity 에서 포화)
        private long _total;  // 누적 기록 수(덮어쓴 것 포함) — 유실 여부 판단용

        /// <summary>기록 스위치. 기본 <b>Off</b>(상시 기록 금지) — 에디터 창의 Record 토글이 켠다.</summary>
        public bool Enabled { get; set; }

        /// <summary>현재 보관 중인 건수(최대 <see cref="Capacity"/>).</summary>
        public int Count { get { lock (_sync) { return _count; } } }

        /// <summary>누적 기록 수. <c>Total &gt; Count</c> 면 링이 돌아 오래된 건이 덮였다는 뜻.</summary>
        public long Total { get { lock (_sync) { return _total; } } }

        public void Clear()
        {
            lock (_sync)
            {
                _next = 0;
                _count = 0;
                _total = 0;
            }
        }

        /// <summary>C_Attack 송신(t_send).</summary>
        public void RecordAttackSent(long timeMs, long actorId, int networkId)
            => Write(CombatTraceKind.AttackSent, timeMs, actorId, targetId: 0, networkId, amount: 0, hp: 0, seq: 0);

        /// <summary>S_AbilityActivated 수신 — 서버 발동 게이트 통과.</summary>
        public void RecordAbilityActivated(long timeMs, long actorId, int networkId)
            => Write(CombatTraceKind.AbilityActivated, timeMs, actorId, targetId: 0, networkId, amount: 0, hp: 0, seq: 0);

        /// <summary>S_ApplyEffect 수신 — 서버 권위 데미지 도착(판정 결과 병합의 핵심).</summary>
        public void RecordDamageReceived(long timeMs, long actorId, long targetId, int amount)
            => Write(CombatTraceKind.DamageReceived, timeMs, actorId, targetId, networkId: 0, amount, hp: 0, seq: 0);

        /// <summary>
        /// S_MonsterState 반영(t_apply).
        /// <para><paramref name="amount"/> = 이번 반영의 HP 델타(피해면 음수, 변화 없으면 0).
        /// <b>몬스터 피해는 S_ApplyEffect 로 오지 않는다</b> — 서버는 몬스터 HP 를 권위로 계산해 S_MonsterState 로만 보낸다
        /// (S_ApplyEffect 는 플레이어가 대상일 때만). 그래서 이 델타가 <b>플레이어→몬스터 스윙의 유일한 데미지 신호</b>이고,
        /// 이게 없으면 틱마다 흐르는 무관한 몬스터 갱신과 구별할 수 없다.</para>
        /// </summary>
        public void RecordMonsterHpApplied(long timeMs, long targetId, int hp, int seq, int amount = 0)
            => Write(CombatTraceKind.MonsterHpApplied, timeMs, actorId: 0, targetId, networkId: 0, amount, hp, seq);

        /// <summary>스테일 드롭(AC-C3) — 순서 역전이 실제로 일어난 증거.</summary>
        public void RecordStaleDropped(long timeMs, long targetId, int droppedSeq, int currentSeq)
            => Write(CombatTraceKind.StaleDropped, timeMs, actorId: currentSeq, targetId, networkId: 0, amount: 0, hp: 0, seq: droppedSeq);

        private void Write(CombatTraceKind kind, long timeMs, long actorId, long targetId, int networkId, int amount, int hp, int seq)
        {
            if (!Enabled) return; // Off = 즉시 반환(lock 도 잡지 않는다)

            lock (_sync)
            {
                _ring[_next] = new CombatTraceEntry
                {
                    Kind = kind,
                    TimeMs = timeMs,
                    ActorId = actorId,
                    TargetId = targetId,
                    NetworkId = networkId,
                    Amount = amount,
                    Hp = hp,
                    Seq = seq,
                };
                _next = (_next + 1) % Capacity;
                if (_count < Capacity) _count++;
                _total++;
            }
        }

        /// <summary>
        /// 보관 중인 엔트리를 <b>오래된 것부터</b> 복사해 반환한다(뷰·테스트용). 여기서만 할당한다 —
        /// 기록 경로를 무할당으로 유지하려고 스냅샷 시점으로 비용을 몰았다.
        /// </summary>
        public CombatTraceEntry[] Snapshot()
        {
            lock (_sync)
            {
                var result = new CombatTraceEntry[_count];
                // 링이 한 바퀴 돌았으면 _next 가 가장 오래된 위치, 아니면 0 부터.
                int start = _count == Capacity ? _next : 0;
                for (int i = 0; i < _count; i++)
                    result[i] = _ring[(start + i) % Capacity];
                return result;
            }
        }
    }
}
