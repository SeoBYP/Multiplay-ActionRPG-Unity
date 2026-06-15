using System.Collections.Generic;

namespace Game.Presentation.Equipment
{
    /// <summary>장비창 State. 착용 중인 슬롯들(빈 슬롯 제외)과 로딩/에러.</summary>
    public sealed class EquipmentState
    {
        public IReadOnlyList<EquipmentSlotModel> Equipped { get; }
        public bool IsLoading { get; }
        public string Error { get; }

        public static readonly EquipmentState Initial =
            new EquipmentState(new List<EquipmentSlotModel>(), false, null);

        private EquipmentState(IReadOnlyList<EquipmentSlotModel> equipped, bool isLoading, string error)
        {
            Equipped = equipped;
            IsLoading = isLoading;
            Error = error;
        }

        public EquipmentState WithLoading() => new EquipmentState(Equipped, true, null);
        public EquipmentState WithEquipped(IReadOnlyList<EquipmentSlotModel> equipped) => new EquipmentState(equipped, false, null);
        public EquipmentState WithError(string error) => new EquipmentState(Equipped, false, error);
    }
}
