using System.Collections.Generic;

namespace Game.Presentation.InGame
{
    /// <summary>
    /// 인게임 화면의 불변 상태 스냅샷.
    /// WithXxx 메서드로만 새 State를 생성한다 — 직접 필드 수정 불가.
    /// </summary>
    public sealed class InGameState
    {
        private static readonly IReadOnlyList<BuffView> EmptyBuffs = new BuffView[0];

        /// <summary>복귀 처리 중 (소켓 해제 + LeaveRoom 진행 중).</summary>
        public readonly bool IsReturning;

        /// <summary>복귀 실패 메시지. null = 에러 없음.</summary>
        public readonly string ErrorMessage;

        /// <summary>로컬 플레이어 스탯 (GAS Attribute에서 중계된 값).</summary>
        public readonly int Hp;
        public readonly int MaxHp;
        public readonly int Mp;
        public readonly int MaxMp;

        /// <summary>활성 버프/디버프 표시 목록.</summary>
        public readonly IReadOnlyList<BuffView> Buffs;

        /// <summary>전원 입장 완료(서버 S_GameStatus InProgress). 던전 시작 가능 = "서로 보임" 상태.</summary>
        public readonly bool IsDungeonReady;

        /// <summary>던전 클리어(서버 S_DungeonClear, 몬스터 전멸). 결과 화면 표시 → 로비 복귀 유도.</summary>
        public readonly bool IsDungeonCleared;

        /// <summary>던전 클리어 시 받은 Exp 보상(표시용). 미클리어면 0.</summary>
        public readonly long RewardExp;

        /// <summary>던전 실패(서버 S_DungeonFailed, 참가자 전원 다운). 실패 화면 표시 → 로비 복귀 유도.</summary>
        public readonly bool IsDungeonFailed;

        public static readonly InGameState Initial = new InGameState(false, null, 0, 0, 0, 0, EmptyBuffs, false, false, 0, false);

        private InGameState(bool isReturning, string errorMessage,
            int hp, int maxHp, int mp, int maxMp, IReadOnlyList<BuffView> buffs,
            bool isDungeonReady, bool isDungeonCleared, long rewardExp, bool isDungeonFailed)
        {
            IsReturning  = isReturning;
            ErrorMessage = errorMessage;
            Hp    = hp;
            MaxHp = maxHp;
            Mp    = mp;
            MaxMp = maxMp;
            Buffs = buffs ?? EmptyBuffs;
            IsDungeonReady = isDungeonReady;
            IsDungeonCleared = isDungeonCleared;
            RewardExp = rewardExp;
            IsDungeonFailed = isDungeonFailed;
        }

        public InGameState WithReturning() =>
            new InGameState(true, null, Hp, MaxHp, Mp, MaxMp, Buffs, IsDungeonReady, IsDungeonCleared, RewardExp, IsDungeonFailed);

        public InGameState WithError(string message) =>
            new InGameState(false, message, Hp, MaxHp, Mp, MaxMp, Buffs, IsDungeonReady, IsDungeonCleared, RewardExp, IsDungeonFailed);

        public InGameState WithHp(int hp, int maxHp) =>
            new InGameState(IsReturning, ErrorMessage, hp, maxHp, Mp, MaxMp, Buffs, IsDungeonReady, IsDungeonCleared, RewardExp, IsDungeonFailed);

        public InGameState WithMp(int mp, int maxMp) =>
            new InGameState(IsReturning, ErrorMessage, Hp, MaxHp, mp, maxMp, Buffs, IsDungeonReady, IsDungeonCleared, RewardExp, IsDungeonFailed);

        public InGameState WithBuffs(IReadOnlyList<BuffView> buffs) =>
            new InGameState(IsReturning, ErrorMessage, Hp, MaxHp, Mp, MaxMp, buffs, IsDungeonReady, IsDungeonCleared, RewardExp, IsDungeonFailed);

        public InGameState WithDungeonReady() =>
            new InGameState(IsReturning, ErrorMessage, Hp, MaxHp, Mp, MaxMp, Buffs, true, IsDungeonCleared, RewardExp, IsDungeonFailed);

        public InGameState WithDungeonCleared(long rewardExp) =>
            new InGameState(IsReturning, ErrorMessage, Hp, MaxHp, Mp, MaxMp, Buffs, IsDungeonReady, true, rewardExp, IsDungeonFailed);

        public InGameState WithDungeonFailed() =>
            new InGameState(IsReturning, ErrorMessage, Hp, MaxHp, Mp, MaxMp, Buffs, IsDungeonReady, IsDungeonCleared, RewardExp, true);
    }
}
