using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Presentation.Equipment;
using R3;
using Script.GUI.Inventory;
using Shared.Gameplay.Equipment;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.GUI.Equipment
{
    /// <summary>
    /// 장비 창 (MVI View). EquipmentModel만 주입받는다(proto·System 비참조).
    /// State.Equipped(착용분) → 슬롯 타입별 아이콘 렌더. 슬롯 타입은 공통 EquipmentType(서버 공유).
    ///   - 미착용 슬롯: Icon·Button GameObject 비활성(클릭 불가)
    ///   - 착용 슬롯:   Icon·Button 활성 → 버튼 클릭 시 ItemActionPanel(해제) 팝업(인벤토리와 동일 컨트롤러)
    /// X(Close) 버튼은 자기 SetActive(false) 로 독립 닫힘(인벤토리와 따로 닫힐 수 있음).
    /// </summary>
    public class Equipment : MonoBehaviour
    {
        [SerializeField] private Button btn_close;

        [Serializable]
        private class EquipmentSlotView
        {
            public EquipmentType type;
            public Image Slot;
            public Image Icon;
            public Button Button;
        }

        [SerializeField] private EquipmentSlotView[] _equipmentSlots;

        [Inject] private EquipmentModel _model;

        private IDisposable _stateSubscription;
        private EquipmentState _latestState;

        // 슬롯별 기본 배경 스프라이트(빈 슬롯 모양) — 해제 시 등급 배경에서 복원하기 위해 Start 에서 캐시.
        private readonly System.Collections.Generic.Dictionary<EquipmentType, Sprite> _defaultSlotSprites = new();

        private Canvas _canvas;
        private readonly ItemActionPanelController _actionPanel = new ItemActionPanelController();

        private void Start()
        {
            if (btn_close != null)
                btn_close.onClick.AddListener(Close);

            // 슬롯 버튼 클릭 → 해당 슬롯의 액션 패널(해제). 슬롯은 고정이므로 1회 배선.
            // 동시에 슬롯 기본 배경 스프라이트를 캐시(등급 배경 적용 후 해제 시 복원용).
            if (_equipmentSlots != null)
            {
                foreach (var slotView in _equipmentSlots)
                {
                    if (slotView == null) continue;
                    if (slotView.Slot != null)
                        _defaultSlotSprites[slotView.type] = slotView.Slot.sprite;
                    if (slotView.Button == null) continue;
                    var captured = slotView;
                    captured.Button.onClick.AddListener(() => OnSlotClicked(captured));
                }
            }

            if (_model != null)
            {
                // 창 활성 동안 게임플레이 입력 점유(인벤토리와 동일, refcount). OnDisable에서 자동 해제.
                gameObject.AddComponent<Game.GUI.UiInputCaptureBehaviour>()
                          .Bind(_model.BeginUiCapture, _model.EndUiCapture);

                _stateSubscription = _model.State.Subscribe(Render);
                _model.Accept(EquipmentIntent.Refresh.Instance);
            }
        }

        private void OnEnable()
        {
            // 재오픈 시 최신화(최초 활성화 때는 주입 전이라 null → Start가 담당).
            if (_model != null)
                _model.Accept(EquipmentIntent.Refresh.Instance);
        }

        private void OnDestroy()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = null;
            _actionPanel.Close();
        }

        private void Close()
        {
            _actionPanel.Close();
            gameObject.SetActive(false);
        }

        private void Render(EquipmentState state)
        {
            _latestState = state;
            if (_equipmentSlots == null) return;

            foreach (var slotView in _equipmentSlots)
            {
                if (slotView == null) continue;

                var equipped = FindEquipped(state, slotView.type);
                bool isEquipped = equipped != null;

                // 미착용: Icon·Button GameObject 비활성 / 착용: 활성 + 아이콘 세팅.
                if (slotView.Icon != null)
                {
                    if (isEquipped && equipped.Icon != null)
                        slotView.Icon.sprite = equipped.Icon;
                    slotView.Icon.gameObject.SetActive(isEquipped);
                }
                if (slotView.Button != null)
                    slotView.Button.gameObject.SetActive(isEquipped);

                // 등급 배경: 착용=등급 스프라이트 / 미착용=기본 배경 복원(Start 에서 캐시한 값).
                if (slotView.Slot != null)
                {
                    if (isEquipped && equipped.GradeBackground != null)
                        slotView.Slot.sprite = equipped.GradeBackground;
                    else if (_defaultSlotSprites.TryGetValue(slotView.type, out var def))
                        slotView.Slot.sprite = def;
                }
            }
        }

        // 착용 슬롯 클릭 → 슬롯 오른쪽에 해제 패널. 빈 슬롯(버튼 비활성이라 보통 안 옴)은 무시.
        private void OnSlotClicked(EquipmentSlotView slotView)
        {
            var equipped = FindEquipped(_latestState, slotView.type);
            if (equipped == null || slotView.Slot == null) return;

            var slotRect = (RectTransform)slotView.Slot.transform;
            OpenActionPanel(slotView.type, equipped.ItemId, slotRect).Forget();
        }

        private async UniTask OpenActionPanel(EquipmentType slot, string itemId, RectTransform slotRect)
        {
            await _actionPanel.OpenAsync(ResolveCanvas(), slotRect, panel => panel.Bind(
                itemId,
                onUse: null,
                onEquip: null,
                onUnequip: _ => _model.Accept(new EquipmentIntent.Unequip(slot)),
                onSell: null,
                canUse: false,
                canEquip: false,
                canUnequip: true,
                canSell: false));
        }

        private Canvas ResolveCanvas()
        {
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            return _canvas;
        }

        private static EquipmentSlotModel FindEquipped(EquipmentState state, EquipmentType type)
        {
            if (state?.Equipped == null) return null;
            foreach (var e in state.Equipped)
                if (e.Slot == type) return e;
            return null;
        }

        [InspectorButton("QuickSetting")]
        private void QuickSetting()
        {
            btn_close = this.FindChildComponentByName<Button>("btn_close");
            _equipmentSlots = new[]
            {
                GetSlot("Header_slot", EquipmentType.Header),
                GetSlot("Armor_slot", EquipmentType.Armor),
                GetSlot("Shoose_slot", EquipmentType.Shoose),
                GetSlot("Glove_slot", EquipmentType.Glove),
                GetSlot("Shield_slot", EquipmentType.Shield),
                GetSlot("Weapon_slot", EquipmentType.Weapon),
                GetSlot("Ring_slot", EquipmentType.Ring),
                GetSlot("Necklace_slot", EquipmentType.Necklace),
            };
        }

        private EquipmentSlotView GetSlot(string slotName, EquipmentType type)
        {
            var obg = this.FindChildComponentByName<Image>(slotName);
            return new EquipmentSlotView
            {
                type = type,
                Slot = obg,
                Icon = obg.FindChildComponentByName<Image>("Icon"),
                Button = obg.FindChildComponentByName<Button>("button_padding"),
            };
        }
    }
}
