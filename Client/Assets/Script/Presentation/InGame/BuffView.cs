using UnityEngine;

namespace Game.Presentation.InGame
{
    /// <summary>
    /// HUD 버프 슬롯 표시용 DTO. System(GAS) 타입(EffectId/Category 등)을 View에 노출하지 않기 위해
    /// Presentation에서 Sprite·Color·시간으로 변환해 담는다.
    /// </summary>
    public readonly struct BuffView
    {
        public readonly Sprite Icon;
        public readonly Color Tint;
        public readonly float RemainingSeconds;
        public readonly float TotalSeconds;
        public readonly int Stacks;
        public readonly bool IsInfinite;

        public BuffView(Sprite icon, Color tint, float remainingSeconds, float totalSeconds, int stacks, bool isInfinite)
        {
            Icon = icon;
            Tint = tint;
            RemainingSeconds = remainingSeconds;
            TotalSeconds = totalSeconds;
            Stacks = stacks;
            IsInfinite = isInfinite;
        }
    }
}
