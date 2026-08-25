using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.GUI.Common;
using Game.Gameplay.Input;
using Game.Presentation.InGame;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.GUI.OutGame
{
    /// <summary>
    /// 인게임 HUD View.
    ///
    /// MVI 규칙:
    ///   - State를 받아 UI를 렌더링한다.
    ///   - 사용자 입력을 Intent로 변환해 Model에 전달한다.
    ///   - Service / Repository 직접 호출 없음.
    /// </summary>
    public class GameHud : MonoBehaviour, IInputHandler
    {
        private enum SideButtonType
        {
            Inventory,
            Ability,
            Smithy,
            Quest,
            Shop,
            Setting
        }
        
        [Serializable]
        private class SideButton
        {
            public SideButtonType type;
            public Button button;
        }
        
        [Inject] private InGameModel _model;
        [Inject] private IInputRouter _inputRouter;

        [Header("Dungeon Result")]
        [Tooltip("던전 클리어(몬스터 전멸) 결과 패널. 미할당이어도 동작은 무해(토글만 생략).")]
        [SerializeField] private DungeonClear dungeonClearView;
        [Tooltip("던전 실패(참가자 전원 다운) 패널. 미할당이어도 동작은 무해(토글만 생략).")]
        [SerializeField] private DungeonFailed dungeonFailedView;
        [Tooltip("던전 클리어 후 결과 패널을 띄우기까지 지연(초). 그 사이 막타 드랍을 주울 수 있다. 패널은 입력을 막지 않는다.")]
        [SerializeField] private float dungeonClearPanelDelaySeconds = 4f;

        [Header("Side Buttons")]
        [SerializeField] private SideButton[] sideButtons;
        
        [Header("Buff Slot Container")]
        [SerializeField] private LayoutGroup buffSlotContainer;
        [SerializeField] private BattleEffectSlot buffSlotPrefab;

        private readonly List<BattleEffectSlot> _buffSlots = new List<BattleEffectSlot>();
        private bool _buffPoolInitialized;
        private bool _clearShowScheduled;

        [Header("Quick Buttons")]
        [SerializeField] private QuickButtonSlot[] quickButtons;
        
        [Header("Player Status")]
        [SerializeField] private SliderBall hpSlider;
        [SerializeField] private SliderBall mpSlider;
        [SerializeField] private Slider expSlider;
        [SerializeField] private TextMeshProUGUI expValue; // {현재 경험치}/{다음 레벨업에 필요한 경험치}

        [Header("Item Pickup Toast (ShopToastMessage 패턴)")]
        [Tooltip("획득 토스트 TMP. 프리팹에 배치하면 그걸 쓰고, 미할당이면 코드로 생성(하단 중앙).")]
        [SerializeField] private TextMeshProUGUI itemToastText;
        [Tooltip("아이템 획득 토스트 표시 시간(초). 줍기 애니 대신 이 토스트로 획득 피드백.")]
        [SerializeField] private float itemToastSeconds = 2f;

        [Header("상호작용 안내")]
        [Tooltip("가까운 대상이 있을 때 뜨는 안내(예: \"[E] 오르기\"). 미할당이면 코드로 생성한다(프리팹 무변경).")]
        [SerializeField] private TextMeshProUGUI interactionPromptText;
        [Tooltip("안내에 표시할 상호작용 키 라벨. 키 바인딩을 바꾸면 여기도 바꾼다.")]
        [SerializeField] private string interactKeyLabel = "E";
        private static readonly Color ItemToastColor = new Color(1f, 0.92f, 0.55f); // 옅은 금색(획득감)
        private TextMeshProUGUI _pickupToast;
        private TextMeshProUGUI _interactionPrompt;          // 실제 사용 TMP(serialized 또는 코드 생성) 캐시
        private CancellationTokenSource _pickupToastCts;
        
        [InspectorButton("Quick Setting")]
        private void QuickSetting()
        {
            sideButtons = new SideButton[]
            {
                new SideButton { type = SideButtonType.Inventory, button = this.FindChildComponentByName<Button>("btn_Inventory") },
                new SideButton { type = SideButtonType.Ability, button = this.FindChildComponentByName<Button>("btn_Ability") },
                new SideButton { type = SideButtonType.Smithy, button = this.FindChildComponentByName<Button>("btn_Smithy") },
                new SideButton { type = SideButtonType.Quest, button = this.FindChildComponentByName<Button>("btn_Quest") },
                new SideButton { type = SideButtonType.Shop, button = this.FindChildComponentByName<Button>("btn_Shop") },
                new SideButton { type = SideButtonType.Setting, button = this.FindChildComponentByName<Button>("btn_Setting") },
            };
            
            buffSlotContainer = this.FindChildComponentByPath<LayoutGroup>("window_actionbar/BuffSlots");
            
            quickButtons = this.GetComponentsInChildren<QuickButtonSlot>(true);
            
            hpSlider = this.FindChildComponentByName<SliderBall>("HP_Ball");
            mpSlider = this.FindChildComponentByName<SliderBall>("MP_Ball");
            
            expSlider = this.FindChildComponentByName<Slider>("expSlider");
            expValue = this.FindChildComponentByName<TextMeshProUGUI>("expValue");

            // 획득 토스트 TMP(있으면 사용, 없으면 런타임에 코드로 생성). Shop 의 ToastText 배선과 동형.
            itemToastText = this.FindChildComponentByName<TextMeshProUGUI>("ItemToastText");
        }
        
        private void Start()
        {
            // 라우터 등록은 Start 에서 한다 — [Inject] 필드 주입이 OnEnable 보다 늦을 수 있다(unity-client.md).
            _inputRouter?.Register(this);

            // 결과 패널의 자체 return 버튼도 같은 복귀 흐름으로 연결.
            if (dungeonClearView != null) dungeonClearView.Bind(OnClickReturnToLobby);
            if (dungeonFailedView != null) dungeonFailedView.Bind(OnClickReturnToLobby);

            // 사이드버튼 배선 — 현재는 Inventory만 연동(나머지 Ability/Shop 등은 후속).
            // 버튼 클릭 → InGameModel 토글 신호 → InventoryViewController가 창 로드/토글(I키와 동일 funnel).
            BindSideButton(SideButtonType.Inventory, () => _model.Accept(InGameIntent.ToggleInventory.Instance));
            BindSideButton(SideButtonType.Shop, () => _model.Accept(InGameIntent.ToggleShop.Instance));
            BindSideButton(SideButtonType.Quest, () => _model.Accept(InGameIntent.ToggleQuest.Instance));
            BindSideButton(SideButtonType.Ability, () => _model.Accept(InGameIntent.ToggleAbility.Instance));

            InitBuffPool();

            _model.State
                .Subscribe(Render)
                .AddTo(destroyCancellationToken);

            // 비정상 연결 끊김 → 끊김 팝업(확인 시 로비 복귀).
            _model.OnConnectionLost
                .Subscribe(_ => ShowDisconnectPopupAsync().Forget())
                .AddTo(destroyCancellationToken);

            // 아이템 획득(서버 S_ItemPickedUp) → 하단 중앙 토스트(줍기 애니 대체).
            _model.OnItemPickup
                .Subscribe(ShowPickupToast)
                .AddTo(destroyCancellationToken);

            // 상호작용 안내 — 가까운 대상이 생기면 "[E] 오르기", 없으면 숨김.
            _model.OnInteractionPrompt
                .Subscribe(RenderInteractionPrompt)
                .AddTo(destroyCancellationToken);
        }

        /// <summary>
        /// 서버 획득 확정 시 뜨는 획득 토스트(ShopToastMessage 패턴). Shop 은 미할당 시 로그 폴백이지만,
        /// 획득은 놓치면 안 되는 피드백이라 미할당 시 코드로 TMP 를 생성해 항상 보이게 한다(프리팹 무변경).
        /// </summary>
        private void ShowPickupToast(ItemToastMessage toast)
        {
            if (_pickupToast == null)
                _pickupToast = itemToastText != null ? itemToastText : CreatePickupToast();

            _pickupToast.text = toast.Message;
            _pickupToast.color = ItemToastColor;
            _pickupToast.gameObject.SetActive(true);

            _pickupToastCts?.Cancel();
            _pickupToastCts?.Dispose();
            _pickupToastCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            HidePickupToastAfterDelay(_pickupToastCts.Token).Forget();
        }

        /// <summary>
        /// 상호작용 안내 표시. 대상이 없으면(null) 숨긴다.
        /// 문구 조립(키 라벨 + 행동 이름)은 <b>여기서</b> 한다 — Gameplay 는 키 배치를 몰라야 한다(레이어 규칙).
        /// </summary>
        private void RenderInteractionPrompt(string prompt)
        {
            if (_interactionPrompt == null)
                _interactionPrompt = interactionPromptText != null ? interactionPromptText : CreateInteractionPrompt();

            if (string.IsNullOrEmpty(prompt))
            {
                _interactionPrompt.gameObject.SetActive(false);
                return;
            }

            _interactionPrompt.text = $"[{interactKeyLabel}] {prompt}";
            _interactionPrompt.gameObject.SetActive(true);
        }

        private TextMeshProUGUI CreateInteractionPrompt()
        {
            var go = new GameObject("InteractionPrompt", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 240f); // 획득 토스트(180)보다 위 — 겹치지 않게
            rt.sizeDelta = new Vector2(700f, 44f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontSize = 28f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            go.SetActive(false);
            return tmp;
        }

        private TextMeshProUGUI CreatePickupToast()
        {
            var go = new GameObject("ItemPickupToast", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 180f); // 액션바 위
            rt.sizeDelta = new Vector2(700f, 44f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontSize = 26f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false; // 색은 ShowPickupToast 가 ItemToastColor 로 설정
            return tmp;
        }

        private async UniTaskVoid HidePickupToastAfterDelay(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(itemToastSeconds), cancellationToken: ct);
                if (_pickupToast != null)
                    _pickupToast.gameObject.SetActive(false);
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>컨테이너에 미리 배치된 슬롯이 있으면 풀로 흡수하고 모두 숨긴다.</summary>
        private void InitBuffPool()
        {
            if (_buffPoolInitialized || buffSlotContainer == null)
                return;

            buffSlotContainer.GetComponentsInChildren(true, _buffSlots);
            foreach (var slot in _buffSlots)
                slot.Hide();
            _buffPoolInitialized = true;
        }

        private void OnDestroy()
        {
            // 라우터는 씬 스코프, HUD 는 Addressable 인스턴스 — 수명이 달라 반드시 스스로 해제한다.
            _inputRouter?.Unregister(this);
        }

        // ── IInputHandler ─────────────────────────
        // UI 우선순위 100(LobbyViewController 와 동일) — 월드 인터랙션보다 먼저 소비한다.
        public int Priority => 100;

        /// <summary>
        /// 창 토글 키. I = 인벤토리+장비 쌍 / K = 장비 / Q = 퀘스트 / G = 어빌리티.
        /// 상점은 키 없이 HUD 상점버튼으로만 연다(S는 WASD 후진과 충돌해 제외).
        /// 창 열림 시 이동 차단은 UiInputCaptureBehaviour 가 처리한다.
        /// </summary>
        public bool TryHandle(GameInputAction action)
        {
            if (_model == null) return false;

            switch (action)
            {
                case GameInputAction.ToggleInventory: _model.Accept(InGameIntent.ToggleInventory.Instance); return true;
                case GameInputAction.ToggleEquipment: _model.Accept(InGameIntent.ToggleEquipment.Instance); return true;
                case GameInputAction.ToggleQuest:     _model.Accept(InGameIntent.ToggleQuest.Instance);     return true;
                case GameInputAction.ToggleAbility:   _model.Accept(InGameIntent.ToggleAbility.Instance);   return true;
                default: return false;
            }
        }

        private void OnClickReturnToLobby()
        {
            _model.Accept(InGameIntent.ReturnToLobby.Instance);
        }

        private bool _disconnectPopupShown;

        /// <summary>비정상 연결 끊김 알림 팝업. 확인 시 로비(Main)로 복귀. 팝업 로드 실패 시 즉시 복귀.</summary>
        private async UniTaskVoid ShowDisconnectPopupAsync()
        {
            if (_disconnectPopupShown) return;
            _disconnectPopupShown = true;

            if (GUIRoot.Instance == null) { OnClickReturnToLobby(); return; }

            var inst = await AddressableLoader.LoadAndInstantiateAsync(
                AddressKeys.UI.AlertPopup, GUIRoot.Instance.transform, destroyCancellationToken);
            if (inst == null) { OnClickReturnToLobby(); return; }

            var popup = inst.GameObject.GetComponent<AlertPopup>();
            if (popup == null) { inst.Dispose(); OnClickReturnToLobby(); return; }

            popup.SetAddressableOwner(inst);
            popup.Setup("연결 끊김", "서버와의 연결이 끊겼습니다.\n로비로 돌아갑니다.",
                OnClickReturnToLobby, PopupGlowType.Danger);
        }

        /// <summary>sideButtons 배열에서 해당 타입의 버튼을 찾아 클릭 핸들러를 연결한다.</summary>
        private void BindSideButton(SideButtonType type, global::System.Action onClick)
        {
            if (sideButtons == null) return;
            foreach (var sb in sideButtons)
            {
                if (sb != null && sb.type == type && sb.button != null)
                {
                    sb.button.onClick.AddListener(() => onClick());
                    return;
                }
            }
        }

        private void Render(InGameState state)
        {
            // 던전 클리어(몬스터 전멸) → 결과 패널을 '지연' 표시(그 사이 막타 드랍 줍기 가능, 입력은 안 막음).
            // 상태(IsDungeonCleared)는 즉시 true지만 패널 SetActive만 dungeonClearPanelDelaySeconds 만큼 늦춘다.
            if (dungeonClearView != null)
            {
                if (state.IsDungeonCleared)
                {
                    if (!_clearShowScheduled)
                    {
                        _clearShowScheduled = true;
                        ShowDungeonClearAfterDelay(state.RewardExp).Forget();
                    }
                }
                else
                {
                    _clearShowScheduled = false;
                    if (dungeonClearView.gameObject.activeSelf)
                        dungeonClearView.gameObject.SetActive(false);
                }
            }

            // 던전 실패(참가자 전원 다운) → 실패 패널 표시.
            if (dungeonFailedView != null && dungeonFailedView.gameObject.activeSelf != state.IsDungeonFailed)
                dungeonFailedView.gameObject.SetActive(state.IsDungeonFailed);

            // 로컬 플레이어 스탯 → 게이지 (GAS Attribute에서 중계된 값)
            if (hpSlider != null)
                hpSlider.SetValue(state.Hp, state.MaxHp);
            if (mpSlider != null)
                mpSlider.SetValue(state.Mp, state.MaxMp);

            RenderExp(state);
            RenderBuffs(state.Buffs);
        }

        /// <summary>
        /// 클리어 결과 패널을 dungeonClearPanelDelaySeconds 뒤에 표시한다.
        /// 지연 동안 플레이어는 막타로 떨어진 드랍을 줍고 자유롭게 이동할 수 있다(패널은 모달이 아님).
        /// 지연 중 상태가 클리어 해제(복귀 등)되면 _clearShowScheduled가 false로 풀려 표시를 건너뛴다.
        /// </summary>
        private async UniTaskVoid ShowDungeonClearAfterDelay(long rewardExp)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(dungeonClearPanelDelaySeconds),
                    cancellationToken: destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (dungeonClearView == null || !_clearShowScheduled)
                return;

            dungeonClearView.SetReward(rewardExp);
            dungeonClearView.gameObject.SetActive(true);
        }

        /// <summary>진행(레벨/Exp) → exp 게이지 + 텍스트. 서버 권위 GetProgression 중계값(InGameModel). 만렙이면 MAX.</summary>
        private void RenderExp(InGameState state)
        {
            bool isMax = state.ExpToNext <= 0;
            if (expSlider != null)
            {
                // 표시 전용 게이지 — SetValueWithoutNotify 로 onValueChanged 발동을 막는다.
                // (값을 받아 텍스트를 쓰는 용도라 콜백 불필요. 프리팹에 잘못 연결된 리스너가 있어도 안전.)
                float fill = isMax ? 1f : Mathf.Clamp01((float)((double)state.Exp / state.ExpToNext));
                expSlider.SetValueWithoutNotify(fill);
            }
            if (expValue != null)
                expValue.text = isMax ? "MAX" : $"{state.Exp}/{state.ExpToNext}";
        }

        /// <summary>활성 버프 목록을 슬롯 풀에 바인딩. 부족하면 prefab으로 확장, 남으면 숨김.</summary>
        private void RenderBuffs(IReadOnlyList<BuffView> buffs)
        {
            if (buffSlotContainer == null)
                return;

            while (_buffSlots.Count < buffs.Count && buffSlotPrefab != null)
                _buffSlots.Add(Instantiate(buffSlotPrefab, buffSlotContainer.transform));

            for (int i = 0; i < _buffSlots.Count; i++)
            {
                if (i < buffs.Count)
                    _buffSlots[i].Bind(buffs[i]);
                else
                    _buffSlots[i].Hide();
            }
        }
    }
}