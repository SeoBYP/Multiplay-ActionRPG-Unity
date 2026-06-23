using UnityEngine;

namespace Game.Presentation.Inventory
{
    /// <summary>
    /// 등급(레어도) → 표시 색. 인벤토리·상점·도감 슬롯이 공유하는 단일 색 매핑(중복 정의 방지).
    /// 색은 관용(게임 공통): Common 회색 · Rare 파랑 · Epic 보라 · Legendary 주황.
    /// </summary>
    public static class GradeColors
    {
        public static readonly Color Common    = new(0.78f, 0.78f, 0.78f); // 회색
        public static readonly Color Rare      = new(0.26f, 0.53f, 0.96f); // 파랑
        public static readonly Color Epic      = new(0.64f, 0.35f, 0.93f); // 보라
        public static readonly Color Legendary = new(0.95f, 0.60f, 0.18f); // 주황

        public static Color Of(ItemGrade grade) => grade switch
        {
            ItemGrade.Rare => Rare,
            ItemGrade.Epic => Epic,
            ItemGrade.Legendary => Legendary,
            _ => Common,
        };
    }
}
