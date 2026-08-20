using System;

namespace Game.System.Player
{
    /// <summary>
    /// "지금 무엇을 상호작용할 수 있는가" 통지 채널(POCO, DI 싱글턴).
    /// <see cref="Game.Gameplay.Character.InteractionDetector"/>(탐지) → 이 통지 → InGameModel → HUD 안내 문구.
    ///
    /// <b>왜 채널을 두나</b>: HUD(Game.GUI)는 Gameplay 를 참조하면 안 된다(레이어 규칙).
    /// 줍기 토스트(<see cref="ItemPickupNotifier"/>)와 같은 패턴 — Gameplay 는 밀어 넣기만, 표시 방식은 모른다.
    /// </summary>
    public sealed class InteractionPromptNotifier
    {
        /// <summary>표시할 행동 이름. <c>null</c> = 대상 없음(숨김).</summary>
        public string Current { get; private set; }

        /// <summary>대상이 <b>바뀔 때만</b> 발행(매 프레임 폴링이라 중복 발행을 막는다).</summary>
        public event Action<string> OnChanged;

        public void Set(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) prompt = null;
            if (Current == prompt) return;

            Current = prompt;
            OnChanged?.Invoke(prompt);
        }

        /// <summary>대상 없음(사망·씬 전환 등에서 강제 숨김).</summary>
        public void Clear() => Set(null);
    }
}
