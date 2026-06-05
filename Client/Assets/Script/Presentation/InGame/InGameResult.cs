using System.Collections.Generic;

namespace Game.Presentation.InGame
{
    /// <summary>
    /// Effect 실행 결과의 닫힌 집합.
    /// Reducer만 받는다 — View에 직접 노출되지 않는다.
    /// </summary>
    public abstract class InGameResult
    {
        private InGameResult() { }

        /// <summary>복귀 처리 시작 — 버튼 비활성화 등 로딩 표시에 사용.</summary>
        public sealed class Returning : InGameResult
        {
            public static readonly Returning Instance = new Returning();
            private Returning() { }
        }

        /// <summary>복귀 도중 예외 발생.</summary>
        public sealed class Failed : InGameResult
        {
            public readonly string Message;
            public Failed(string message) { Message = message; }
        }

        /// <summary>로컬 플레이어 HP 변경 (GAS Attribute 중계).</summary>
        public sealed class HpChanged : InGameResult
        {
            public readonly int Current;
            public readonly int Max;
            public HpChanged(int current, int max) { Current = current; Max = max; }
        }

        /// <summary>로컬 플레이어 MP 변경 (GAS Attribute 중계).</summary>
        public sealed class MpChanged : InGameResult
        {
            public readonly int Current;
            public readonly int Max;
            public MpChanged(int current, int max) { Current = current; Max = max; }
        }

        /// <summary>활성 버프/디버프 목록 변경 (GAS ActiveEffects 중계).</summary>
        public sealed class BuffsChanged : InGameResult
        {
            public readonly IReadOnlyList<BuffView> Buffs;
            public BuffsChanged(IReadOnlyList<BuffView> buffs) { Buffs = buffs; }
        }

        /// <summary>전원 입장 완료(서버 S_GameStatus InProgress) — 던전 시작 가능.</summary>
        public sealed class DungeonReady : InGameResult
        {
            public static readonly DungeonReady Instance = new DungeonReady();
            private DungeonReady() { }
        }

        /// <summary>던전 클리어(서버 S_DungeonClear, 몬스터 전멸) — 결과 화면 표시.</summary>
        public sealed class DungeonCleared : InGameResult
        {
            public static readonly DungeonCleared Instance = new DungeonCleared();
            private DungeonCleared() { }
        }
    }
}
