using Shared.Gameplay.Equipment;

namespace Game.Presentation.Equipment
{
    /// <summary>장비창 View → Model 단일 진입 인텐트.</summary>
    public abstract class EquipmentIntent
    {
        /// <summary>서버에서 착용 세트를 다시 읽는다(창 열 때·장착 변경 시).</summary>
        public sealed class Refresh : EquipmentIntent
        {
            public static readonly Refresh Instance = new();
            private Refresh() { }
        }

        /// <summary>슬롯 해제(슬롯 클릭 등). 서버 권위 → 성공 시 OnChanged 로 자동 Refresh.</summary>
        public sealed class Unequip : EquipmentIntent
        {
            public readonly EquipmentType Slot;
            public Unequip(EquipmentType slot) { Slot = slot; }
        }
    }
}
