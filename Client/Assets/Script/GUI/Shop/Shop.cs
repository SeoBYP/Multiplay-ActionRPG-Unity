using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.GUI.Common;
using Game.Presentation.Shop;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using VContainer;

namespace Game.GUI.Shop
{
    /// <summary>
    /// 상점 창 View(MVI). ShopModel 만 주입받아 State 구독·렌더 + Intent 발행(System/proto 비노출).
    /// 리스트/스탯 슬롯은 Addressable prefab(Shop_Item / Status_Slot)로 동적 생성(개수 무관).
    /// 열린 동안 UiInputCaptureBehaviour 가 캐릭터 이동을 차단(ShopModel.Begin/EndUiCapture).
    /// </summary>
    public class Shop : MonoBehaviour
    {
        public enum ShopItemType
        {
            Weapon,
            Armor,
            Accessory,
            Potion,
        }
        [Serializable]
        private class ShopTab
        {
            public ShopItemType ShopItemType;
            public Toggle tab_Toggle;
            public TextMeshProUGUI tab_Name;
        }

        [Header("Shop Tab")]
        [SerializeField] private ShopTab[] shopTabs;

        [Header("Shop Item List")]
        [SerializeField] private ScrollRect shopItemList;
        [SerializeField] private RectTransform shopItemListContent;

        [Header("Selected Shop Item")]
        [SerializeField] private GameObject EmptyShopItem;

        [SerializeField] private GameObject SelectedShopItem;
        [SerializeField] private TextMeshProUGUI SelectedShopItemName;
        [SerializeField] private TextMeshProUGUI SelectedShopItemDesc;
        [SerializeField] private Image SelectedShopItemIcon;
        [SerializeField] private Image SelectedShopItemGradeBackground; // 선택 아이템 슬롯 프레임 = 등급 배경

        [SerializeField] private GridLayoutGroup SelectedShopItemStatusSlotParent;

        [SerializeField] private InputField SelectedShopItemAmount;
        [SerializeField] private Button PlusButton;
        [SerializeField] private Button MinusButton;
        [SerializeField] private Button BuyButton;
        [SerializeField] private TextMeshProUGUI SelectedShopItemPrice;

        [Header("Window")]
        [SerializeField] private Button closeButton;

        [Header("Toast (구매 결과 — 성공/실패)")]
        [SerializeField] private TextMeshProUGUI toastText;   // 없으면 로그로 폴백
        [SerializeField] private float toastSeconds = 2f;
        private static readonly Color ToastSuccess = new Color(0.30f, 0.80f, 0.36f);
        private static readonly Color ToastFail = new Color(0.86f, 0.28f, 0.28f);
        private CancellationTokenSource _toastCts;

        [Inject] private ShopModel _model;

        // 동적 슬롯 prefab(Addressable). 인스펙터 할당 불요 — 인벤토리와 동일 패턴.
        // 리스트 슬롯 = 공통 ItemContentsSlot(인벤토리와 통합) — icon+name+등급배경.
        private ItemContentsSlot _itemPrefab;
        private ShopItemStatusSlot _statusPrefab;
        private AsyncOperationHandle<GameObject> _itemHandle;
        private AsyncOperationHandle<GameObject> _statusHandle;
        private bool _prefabsLoaded;

        private readonly List<ItemContentsSlot> _itemRows = new();
        private readonly List<ShopItemStatusSlot> _statusRows = new();

        private IDisposable _stateSub;
        private IDisposable _toastSub;
        private ShopState _last = ShopState.Initial;
        private bool _wired;

        private void Start()
        {
            if (_model == null)
            {
                Debug.LogError("[Shop] ShopModel 미주입 — 씬 스코프 등록/주입 경로 확인");
                return;
            }

            WireOnce();

            if (toastText != null)
                toastText.gameObject.SetActive(false); // 토스트는 구매 시에만 표시

            // 열린 동안 게임플레이(Player) 입력 점유 → 캐릭터 이동 차단(인벤/장비와 동일 패턴).
            gameObject.AddComponent<UiInputCaptureBehaviour>()
                      .Bind(_model.BeginUiCapture, _model.EndUiCapture);

            InitializeAsync().Forget();
        }

        private async UniTaskVoid InitializeAsync()
        {
            await LoadPrefabsAsync();
            if (!_prefabsLoaded)
                return;

            _stateSub = _model.State.Subscribe(Render);
            _toastSub = _model.OnToast.Subscribe(ShowToast);

            // 초기 선택 동기화: 켜져 있는 탭의 카테고리로 시작(없으면 전체).
            var active = shopTabs?.FirstOrDefault(t => t?.tab_Toggle != null && t.tab_Toggle.isOn);
            if (active != null)
                _model.Accept(new ShopIntent.SelectTab(ToCategory(active.ShopItemType)));

            _model.Accept(ShopIntent.Refresh.Instance);
        }

        private async UniTask LoadPrefabsAsync()
        {
            _itemHandle = Addressables.LoadAssetAsync<GameObject>(AddressKeys.UI.ShopItem);
            _statusHandle = Addressables.LoadAssetAsync<GameObject>(AddressKeys.UI.ShopStatusSlot);
            try
            {
                await _itemHandle.Task.AsUniTask();
                await _statusHandle.Task.AsUniTask();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Shop] 슬롯 prefab Addressable 로드 실패: {e.Message}");
                return;
            }

            _itemPrefab = _itemHandle.Result != null ? _itemHandle.Result.GetComponent<ItemContentsSlot>() : null;
            _statusPrefab = _statusHandle.Result != null ? _statusHandle.Result.GetComponent<ShopItemStatusSlot>() : null;

            if (_itemPrefab == null)
                Debug.LogError("[Shop] Shop_Item prefab 로드 실패/ItemContentsSlot 컴포넌트 없음 (Addressable 주소·컴포넌트 확인)");
            if (_statusPrefab == null)
                Debug.LogError("[Shop] Status_Slot prefab 로드 실패/컴포넌트 없음 (Addressable 주소 확인)");

            // 프리팹에 미리 박힌 슬롯은 정리(동적 생성과 중복 방지) — 인벤토리 BuildSlots 와 동일.
            if (shopItemListContent != null)
                foreach (var existing in shopItemListContent.GetComponentsInChildren<ItemContentsSlot>(true))
                    Destroy(existing.gameObject);
            if (SelectedShopItemStatusSlotParent != null)
                foreach (var existing in SelectedShopItemStatusSlotParent.GetComponentsInChildren<ShopItemStatusSlot>(true))
                    Destroy(existing.gameObject);

            _prefabsLoaded = _itemPrefab != null && _statusPrefab != null;
        }

        private void OnEnable()
        {
            // 재오픈 시 진열·골드 새로고침(서버 권위). 최초엔 InitializeAsync 가 로드 후 호출.
            if (_model != null && _prefabsLoaded)
                _model.Accept(ShopIntent.Refresh.Instance);
        }

        private void OnDestroy()
        {
            _stateSub?.Dispose();
            _toastSub?.Dispose();
            _toastCts?.Cancel();
            _toastCts?.Dispose();
            if (_itemHandle.IsValid()) Addressables.Release(_itemHandle);
            if (_statusHandle.IsValid()) Addressables.Release(_statusHandle);
        }

        /// <summary>버튼/탭 리스너는 1회만 연결(Render 마다 재연결 금지). prefab 불요라 Start 즉시 가능.</summary>
        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (PlusButton != null)
                PlusButton.onClick.AddListener(() => _model.Accept(new ShopIntent.SetQuantity(_last.Quantity + 1)));
            if (MinusButton != null)
                MinusButton.onClick.AddListener(() => _model.Accept(new ShopIntent.SetQuantity(_last.Quantity - 1)));
            if (BuyButton != null)
                BuyButton.onClick.AddListener(() => _model.Accept(ShopIntent.Buy.Instance));
            if (SelectedShopItemAmount != null)
                SelectedShopItemAmount.onEndEdit.AddListener(s =>
                {
                    if (int.TryParse(s, out var q)) _model.Accept(new ShopIntent.SetQuantity(q));
                });

            if (shopTabs != null)
            {
                foreach (var tab in shopTabs)
                {
                    if (tab?.tab_Toggle == null) continue;
                    var category = ToCategory(tab.ShopItemType);
                    tab.tab_Toggle.onValueChanged.AddListener(isOn =>
                    {
                        if (isOn) _model.Accept(new ShopIntent.SelectTab(category));
                    });
                }
            }
        }

        private void Close() => gameObject.SetActive(false);

        /// <summary>구매 결과 토스트 — 성공=초록/실패=빨강으로 띄우고 toastSeconds 후 숨김. 필드 미할당 시 로그 폴백.</summary>
        private void ShowToast(ShopToastMessage toast)
        {
            if (toastText == null)
            {
                Debug.Log($"[Shop] {(toast.Success ? "성공" : "실패")}: {toast.Message}");
                return;
            }

            toastText.text = toast.Message;
            toastText.color = toast.Success ? ToastSuccess : ToastFail;
            toastText.gameObject.SetActive(true);

            _toastCts?.Cancel();
            _toastCts?.Dispose();
            _toastCts = new CancellationTokenSource();
            HideToastAfterDelay(_toastCts.Token).Forget();
        }

        private async UniTaskVoid HideToastAfterDelay(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(toastSeconds), cancellationToken: ct);
                if (toastText != null)
                    toastText.gameObject.SetActive(false);
            }
            catch (OperationCanceledException)
            {
                // 새 토스트가 교체 — 정상 취소
            }
        }

        private void Render(ShopState state)
        {
            _last = state;
            if (!_prefabsLoaded)
                return;

            // 리스트 — 선택 탭 카테고리로 필터(없으면 전체). 부족하면 동적 생성, 남으면 비활성.
            var filtered = state.Items
                .Where(i => state.SelectedCategory == null || i.Category == state.SelectedCategory)
                .ToList();

            EnsureItemRows(filtered.Count);
            for (int i = 0; i < _itemRows.Count; i++)
            {
                var slot = _itemRows[i];
                if (i < filtered.Count)
                {
                    var item = filtered[i];
                    slot.gameObject.SetActive(true);
                    // 공통 슬롯: 상점 리스트는 수량 미표시(count=0) + 이름·등급배경 표시.
                    slot.Bind(item.ItemId, item.Icon, 0,
                        _ => _model.Accept(new ShopIntent.SelectItem(item.ItemId)),
                        item.GradeBackground, item.DisplayName);
                }
                else
                {
                    slot.gameObject.SetActive(false);
                }
            }

            RenderSelected(state);
        }

        private void RenderSelected(ShopState state)
        {
            var sel = state.Selected;
            bool hasSel = sel != null;

            if (EmptyShopItem != null) EmptyShopItem.SetActive(!hasSel);
            if (SelectedShopItem != null) SelectedShopItem.SetActive(hasSel);
            if (!hasSel) return;

            if (SelectedShopItemName != null) SelectedShopItemName.text = sel.DisplayName;
            if (SelectedShopItemDesc != null) SelectedShopItemDesc.text = sel.Description; // ItemDisplayCatalog.description
            if (SelectedShopItemIcon != null)
            {
                SelectedShopItemIcon.sprite = sel.Icon;
                SelectedShopItemIcon.enabled = sel.Icon != null;
            }
            // 선택 아이템 등급 배경(슬롯 프레임) — 리스트 슬롯과 동일한 등급 스프라이트.
            if (SelectedShopItemGradeBackground != null)
            {
                SelectedShopItemGradeBackground.sprite = sel.GradeBackground;
                SelectedShopItemGradeBackground.enabled = sel.GradeBackground != null;
            }
            if (SelectedShopItemAmount != null) SelectedShopItemAmount.text = state.Quantity.ToString();
            if (SelectedShopItemPrice != null) SelectedShopItemPrice.text = (sel.BuyPrice * state.Quantity).ToString("N0");

            RenderStats(sel);
        }

        private void RenderStats(ShopItemModel sel)
        {
            EnsureStatusRows(sel.Stats.Count);
            for (int i = 0; i < _statusRows.Count; i++)
            {
                var slot = _statusRows[i];
                if (i < sel.Stats.Count)
                {
                    var s = sel.Stats[i];
                    slot.gameObject.SetActive(true);
                    slot.Bind($"{StatLabel(s.Stat)} +{s.Amount}");
                }
                else
                {
                    slot.gameObject.SetActive(false);
                }
            }
        }

        // ── 동적 슬롯 풀(부족하면 생성, 재사용) ──
        private void EnsureItemRows(int needed)
        {
            while (_itemRows.Count < needed && _itemPrefab != null && shopItemListContent != null)
                _itemRows.Add(Instantiate(_itemPrefab, shopItemListContent));
        }

        private void EnsureStatusRows(int needed)
        {
            var parent = SelectedShopItemStatusSlotParent != null ? SelectedShopItemStatusSlotParent.transform : null;
            while (_statusRows.Count < needed && _statusPrefab != null && parent != null)
                _statusRows.Add(Instantiate(_statusPrefab, parent));
        }

        private static ShopCategory ToCategory(ShopItemType type) => type switch
        {
            ShopItemType.Weapon => ShopCategory.Weapon,
            ShopItemType.Armor => ShopCategory.Armor,
            ShopItemType.Accessory => ShopCategory.Accessory,
            ShopItemType.Potion => ShopCategory.Potion,
            _ => ShopCategory.Unspecified,
        };

        private static string StatLabel(string stat) => stat switch
        {
            "AttackPower" => "공격력",
            "Defense" => "방어력",
            "MaxHealth" => "체력",
            "MaxMana" => "마나",
            "Strength" => "힘",
            "Dexterity" => "민첩",
            "Intelligence" => "지능",
            _ => stat,
        };

        [InspectorButton("Quick Setting")]
        private void QuickSetting()
        {
            shopTabs = new ShopTab[]
            {
                new ShopTab{ ShopItemType = ShopItemType.Weapon, tab_Toggle = this.FindChildComponentByName<Toggle>("tab_Weapon"), tab_Name = this.FindChildComponentByName<TextMeshProUGUI>("tab_Weapon_Name") },
                new ShopTab{ ShopItemType = ShopItemType.Armor, tab_Toggle = this.FindChildComponentByName<Toggle>("tab_Armor"), tab_Name = this.FindChildComponentByName<TextMeshProUGUI>("tab_Armor_Name") },
                new ShopTab{ ShopItemType = ShopItemType.Accessory, tab_Toggle = this.FindChildComponentByName<Toggle>("tab_Accessory"), tab_Name = this.FindChildComponentByName<TextMeshProUGUI>("tab_Accessory_Name") },
                new ShopTab{ ShopItemType = ShopItemType.Potion, tab_Toggle = this.FindChildComponentByName<Toggle>("tab_Potion"), tab_Name = this.FindChildComponentByName<TextMeshProUGUI>("tab_Potion_Name") },
            };

            shopItemList = this.FindChildComponentByName<ScrollRect>("shopItemList");
            shopItemListContent = shopItemList.content;

            EmptyShopItem = this.FindChildComponentByName("EmptyShopItem");
            SelectedShopItem = this.FindChildComponentByName("SelectedShopItem");

            SelectedShopItemName = SelectedShopItem.FindChildComponentByName<TextMeshProUGUI>("SelectedShopItemName");
            SelectedShopItemDesc = SelectedShopItem.FindChildComponentByName<TextMeshProUGUI>("SelectedShopItemDesc");
            SelectedShopItemIcon = SelectedShopItem.FindChildComponentByName<Image>("SelectedShopItemIcon");
            SelectedShopItemGradeBackground = SelectedShopItem.FindChildComponentByName<Image>("item_slot");

            SelectedShopItemStatusSlotParent = SelectedShopItem.FindChildComponentByName<GridLayoutGroup>("SelectedShopItemStatusSlotParent");

            SelectedShopItemAmount = SelectedShopItem.FindChildComponentByName<InputField>("SelectedShopItemAmount");
            PlusButton = SelectedShopItem.FindChildComponentByName<Button>("PlusButton");
            MinusButton = SelectedShopItem.FindChildComponentByName<Button>("MinusButton");
            BuyButton = SelectedShopItem.FindChildComponentByName<Button>("BuyButton");
            SelectedShopItemPrice = SelectedShopItem.FindChildComponentByName<TextMeshProUGUI>("SelectedShopItemPrice");

            closeButton = this.FindChildComponentByName<Button>("CloseButton");
            toastText = this.FindChildComponentByName<TextMeshProUGUI>("ToastText");
        }
    }
}
