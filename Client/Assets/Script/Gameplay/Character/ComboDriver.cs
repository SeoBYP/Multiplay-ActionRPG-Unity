using System;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 근접 공격 콤보(A→B→C) 진행기 = Action 축(FSM 아님, CA-1). 순수 로직 — 시각만 보고 "언제 어느 단계를 낼지" 정한다.
    ///
    /// <b>타이밍의 진실원 = 스킬 데이터</b>(`SkillTimeline.ComboChainMs/ComboWindowMs`, SO 저작→bake→skills.json).
    /// 같은 값을 <b>서버도</b> cadence 권위 게이트에 쓰므로 서버 권위와 애니가 어긋나지 않는다.
    /// (드라이버는 skillId → (chain, window) 조회 함수만 주입받는다 — 데이터 접근은 소유자가 담당.)
    ///
    /// <b>선입력(버퍼링) 모델</b> — 콤보는 "이전 공격에서 이어서" 나가야 자연스럽다:
    ///   - 첫 타(A)는 입력 즉시 발동.
    ///   - 다음 공격은 <b>직전 스윙의 ComboChainMs</b> 가 지나야 발동(= 애니 체인 지점).
    ///     그 전에 들어온 입력은 <b>버려지지 않고 버퍼</b>돼 있다가 그 시점에 자동으로 나간다.
    ///   - 직전 스윙 후 <b>ComboWindowMs</b> 까지 입력이 없으면 콤보가 끊겨 A 부터 다시.
    ///
    /// <b>왜 타이밍을 애니메이터가 아니라 데이터/코드가 갖는가</b>: 컨트롤러 체인 전이에 `hasExitTime` 을 걸면
    /// Unity 가 Attack <b>트리거를 소실</b>해 전이를 놓친다(실측). 그래서 체인 전이는 `hasExitTime=false` 로 두고
    /// (= 코드가 트리거를 쏘는 순간 전이) <b>언제 쏠지</b>를 스킬 데이터가 정한다. 덕분에 데미지·네트워크 송신도
    /// 애니 체인과 같은 시점에 나가 어긋나지 않는다.
    ///
    /// 소유: <see cref="Game.Gameplay.Character.PlayerCharacterAgent"/> — 매 프레임 <see cref="TryFire"/> 폴링,
    /// 공격 입력 시 <see cref="OnAttackPressed"/>.
    /// </summary>
    public sealed class ComboDriver
    {
        /// <summary>skillId → (체인 지점 초, 콤보 창 초). 스킬 데이터(SkillTimeline)에서 온다.</summary>
        public delegate (float chainSec, float windowSec) TimingResolver(int skillId);

        private readonly int[] _skillIds; // 단계별 skillId (예: [2=combo_a, 3=combo_b, 4=combo_c])
        private readonly TimingResolver _timings;

        private int _index;               // 다음에 칠 단계(0..N-1)
        private float _lastSwingTime;
        private bool _hasSwung;           // 첫 발동 전에는 게이트 없음
        private bool _buffered;           // 선입력 대기 중

        // 직전 스윙의 타이밍(다음 입력 판정에 쓰인다 — "이 스윙 후 언제/언제까지").
        private float _lastChainSec;
        private float _lastWindowSec;

        public ComboDriver(int[] comboSkillIds, TimingResolver timings)
        {
            _skillIds = comboSkillIds;
            _timings = timings;
        }

        /// <summary>공격 입력 접수 — 즉시 발동이든 선입력이든 일단 버퍼에 담는다. 실제 발동은 <see cref="TryFire"/>.</summary>
        public void OnAttackPressed(float now)
        {
            // 이미 창이 끊겼으면 새 콤보(A)로 시작한다.
            if (_hasSwung && now - _lastSwingTime > _lastWindowSec)
                _index = 0;

            _buffered = true;
        }

        /// <summary>
        /// 매 프레임 폴링 — 지금 발동할 단계가 있으면 true + (skillId, 단계index).
        /// A(첫 발동)는 즉시. 이후는 직전 스윙의 체인 지점이 지나야 (버퍼돼 있던 입력이) 나간다.
        /// </summary>
        public bool TryFire(float now, out int skillId, out int step)
        {
            skillId = 0;
            step = 0;

            if (!_buffered)
                return false;

            if (_hasSwung)
            {
                float since = now - _lastSwingTime;

                // 아직 체인 지점 전 — 버퍼를 유지한 채 대기(선입력 보존).
                if (since < _lastChainSec)
                    return false;

                // 창이 끊겼으면 A 부터.
                if (since > _lastWindowSec)
                    _index = 0;
            }

            step = _index;
            skillId = _skillIds[step];

            // 이번에 낸 스킬의 타이밍이 "다음 입력"의 기준이 된다.
            var t = _timings(skillId);
            _lastChainSec = t.chainSec;
            _lastWindowSec = t.windowSec;

            _lastSwingTime = now;
            _hasSwung = true;
            _index = (step + 1) % _skillIds.Length; // A→B→C→A 순환
            _buffered = false;
            return true;
        }

        /// <summary>사망/부활/씬전환 등에서 콤보 상태 초기화(선입력 포함).</summary>
        public void Reset()
        {
            _index = 0;
            _hasSwung = false;
            _buffered = false;
            _lastChainSec = 0f;
            _lastWindowSec = 0f;
        }
    }
}
