using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using Game.Presentation.Inventory;
using Game.System.Input;
using Game.System.Player;
using Game.System.Progression;
using R3;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Game.Presentation.InGame
{
    /// <summary>
    /// 인게임 화면의 MVI Model.
    ///
    /// Intent → Effect → Result → Reducer → State
    ///
    /// 규칙:
    ///   View는 Accept(Intent)만 호출한다.
    ///   Model은 State만 발행한다. View를 직접 조작하지 않는다.
    ///   Reducer는 순수 함수다. 비동기 처리는 Effect 메서드에서만 한다.
    /// </summary>
    public sealed class InGameModel : IInitializable, IDisposable
    {
        private readonly ISocketSession _socketSession;
        private readonly LocalPlayerContext _localPlayer;
        private readonly GameplayEffectCatalog _effectCatalog;
        private readonly EffectIconCatalog _iconCatalog;
        private readonly ISocketPacketState _packetState;
        private readonly PlayerProgressionHolder _progression; // 레벨/Exp 중계(없으면 exp 게이지 미갱신)
        private readonly IInputContext _inputContext;          // 끊김 시 입력/이동 정지(없으면 정지만 생략)
        private readonly ItemDisplayCatalog _itemDisplay;      // 아이템 이름 표시(없으면 itemId 폴백)
        private readonly ItemPickupNotifier _pickupNotifier;   // Main 로컬 줍기 통지(던전은 소켓 경로)
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly ReactiveProperty<InGameState> _state
            = new ReactiveProperty<InGameState>(InGameState.Initial);

        // HUD 버튼/I키가 인벤토리 토글을 요청하는 신호. InventoryViewController가 구독.
        private readonly Subject<Unit> _toggleInventory = new Subject<Unit>();
        public Observable<Unit> OnToggleInventory => _toggleInventory;

        // K키가 장비창 단독 토글을 요청하는 신호. EquipmentViewController가 구독.
        private readonly Subject<Unit> _toggleEquipment = new Subject<Unit>();
        public Observable<Unit> OnToggleEquipment => _toggleEquipment;

        // S키/상점버튼이 상점창 단독 토글을 요청하는 신호. ShopViewController가 구독.
        private readonly Subject<Unit> _toggleShop = new Subject<Unit>();
        public Observable<Unit> OnToggleShop => _toggleShop;

        // HUD 퀘스트버튼이 퀘스트창 단독 토글을 요청하는 신호. QuestViewController가 구독.
        private readonly Subject<Unit> _toggleQuest = new Subject<Unit>();
        public Observable<Unit> OnToggleQuest => _toggleQuest;

        // HUD Ability버튼/G키가 스탯창 단독 토글을 요청하는 신호. StatViewController가 구독.
        private readonly Subject<Unit> _toggleAbility = new Subject<Unit>();
        public Observable<Unit> OnToggleAbility => _toggleAbility;

        // 아이템 획득 토스트 신호(ShopToastMessage 패턴 — 타입 메시지). GameHud가 구독해 하단 토스트 표시.
        // 줍기 애니를 없애고(전용 클립 없음) 이 토스트로 피드백 대체(사용자 결정). 진실원 = 서버 S_ItemPickedUp.
        private readonly Subject<ItemToastMessage> _itemPickup = new Subject<ItemToastMessage>();
        public Observable<ItemToastMessage> OnItemPickup => _itemPickup;

        // 비정상 연결 끊김 1회 신호(OnToast 동형 side-channel). GameHud가 구독해 끊김 팝업 표시.
        private readonly Subject<Unit> _connectionLost = new Subject<Unit>();
        public Observable<Unit> OnConnectionLost => _connectionLost;
        private bool _connectionLostHandled;
        private bool _uiCaptured;

        private AbilitySystemComponent _asc;
        private bool _isProcessing;

        public ReadOnlyReactiveProperty<InGameState> State =>
            _state.ToReadOnlyReactiveProperty();

        public InGameModel(
            ISocketSession socketSession,
            LocalPlayerContext localPlayer,
            GameplayEffectCatalog effectCatalog = null,
            EffectIconCatalog iconCatalog = null,
            ISocketPacketState packetState = null,
            PlayerProgressionHolder progression = null,
            IInputContext inputContext = null,
            ItemDisplayCatalog itemDisplay = null,
            ItemPickupNotifier pickupNotifier = null)
        {
            _socketSession  = socketSession;
            _localPlayer    = localPlayer;
            _effectCatalog  = effectCatalog;
            _iconCatalog    = iconCatalog;
            _packetState    = packetState;
            _progression    = progression;
            _inputContext   = inputContext;
            _itemDisplay    = itemDisplay;
            _pickupNotifier = pickupNotifier;
        }

        public void Initialize()
        {
            // 스폰 순서와 무관하게 동작: 이미 set돼 있으면 즉시 바인딩, 아니면 OnSet 대기.
            _localPlayer.OnSet += BindLocalPlayer;
            if (_localPlayer.AbilitySystem != null)
                BindLocalPlayer(_localPlayer.AbilitySystem);

            // 전원 입장(S_GameStatus InProgress) → 던전 준비 완료 상태로 전환.
            if (_packetState != null)
            {
                _packetState.OnDungeonReady += OnDungeonReady;
                _packetState.OnDungeonCleared += OnDungeonCleared;
                _packetState.OnDungeonFailed += OnDungeonFailed;
                _packetState.OnItemPickedUp += OnItemPickedUp; // 던전(소켓) 줍기
            }

            // Main 로컬 줍기(LocalGroundItem→ClaimKill) → 던전과 동일 획득 토스트로 병합.
            if (_pickupNotifier != null)
                _pickupNotifier.OnPickup += OnItemPickedUp;

            // 비정상 소켓 끊김 → 입력 정지 + 끊김 알림(메인 스레드).
            _socketSession.OnDisconnected += OnSocketDisconnected;

            // 진행(레벨/Exp) → exp 게이지 중계. holder.StartAsync(IAsyncStartable)는 Initialize 이후 실행되므로
            // 여기서 먼저 구독하면 로그인 직후 첫 pull 의 OnChanged 를 놓치지 않는다. 현재값도 즉시 1회 반영.
            if (_progression != null)
            {
                _progression.OnChanged += PushProgression;
                PushProgression();
            }
        }

        private void PushProgression()
        {
            var p = _progression.Current;
            Dispatch(new InGameResult.ExpChanged(p.Level, p.Exp, p.ExpToNext));
        }

        // 이벤트는 메인 스레드에서 오지만(SocketSession이 SwitchToMainThread 후 발화), 방어적으로 한 번 더 보장.
        private void OnSocketDisconnected() => HandleConnectionLostAsync().Forget();

        private async UniTaskVoid HandleConnectionLostAsync()
        {
            await UniTask.SwitchToMainThread();
            if (_connectionLostHandled) return;   // 1회만
            _connectionLostHandled = true;

            _inputContext?.EnterUi();             // 입력/이동 정지(Player 맵 비활성) — ReturnToLobby 시 Dispose 에서 ExitUi 로 균형
            _uiCaptured = true;
            Debug.LogWarning("[InGameModel] 소켓 연결 끊김 감지 — 입력 정지 + 끊김 알림 발행");
            _connectionLost.OnNext(Unit.Default);
        }

        private void OnDungeonReady()
        {
            Debug.Log("[InGameModel] 전원 입장 — IsDungeonReady=true 로 전환");
            Dispatch(InGameResult.DungeonReady.Instance);
        }

        private void OnDungeonCleared(long rewardExp)
        {
            Debug.Log($"[InGameModel] 던전 클리어 — IsDungeonCleared=true (보상 Exp={rewardExp})");
            Dispatch(new InGameResult.DungeonCleared(rewardExp));
        }

        private void OnDungeonFailed()
        {
            Debug.Log("[InGameModel] 던전 실패 — IsDungeonFailed=true 로 전환");
            Dispatch(InGameResult.DungeonFailed.Instance);
        }

        /// <summary>서버 권위 획득 확정(S_ItemPickedUp) → 획득 토스트 메시지 발행. 이름은 표시 카탈로그, 없으면 itemId.</summary>
        private void OnItemPickedUp(string itemId, int qty)
        {
            string name = _itemDisplay?.Get(itemId)?.displayName;
            if (string.IsNullOrEmpty(name)) name = itemId;
            string text = qty > 1 ? $"{name} x{qty} 획득" : $"{name} 획득";
            _itemPickup.OnNext(new ItemToastMessage(text));
        }

        // ── 로컬 플레이어 ASC ↔ State 중계 ────────────

        private void BindLocalPlayer(AbilitySystemComponent asc)
        {
            if (asc == null || ReferenceEquals(asc, _asc))
                return;

            if (_asc != null)
            {
                _asc.OnAttributeChanged -= OnAttributeChanged;
                _asc.OnActiveEffectsChanged -= RefreshBuffs;
            }

            _asc = asc;
            _asc.OnAttributeChanged += OnAttributeChanged;
            _asc.OnActiveEffectsChanged += RefreshBuffs;

            // 구독 시점의 현재값을 즉시 1회 반영 (이벤트는 변화 시점에만 오므로).
            PushInitial(EGameplayAttribute.Health, (c, m) => Dispatch(new InGameResult.HpChanged(c, m)));
            PushInitial(EGameplayAttribute.Mana,   (c, m) => Dispatch(new InGameResult.MpChanged(c, m)));
            RefreshBuffs();
        }

        private void PushInitial(EGameplayAttribute type, Action<int, int> dispatch)
        {
            if (_asc.TryGetAttribute(type, out var attr))
                dispatch(attr.CurrentValue, attr.MaxValue);
        }

        private void OnAttributeChanged(EGameplayAttribute type, int current, int max)
        {
            switch (type)
            {
                case EGameplayAttribute.Health:
                    Dispatch(new InGameResult.HpChanged(current, max));
                    break;
                case EGameplayAttribute.Mana:
                    Dispatch(new InGameResult.MpChanged(current, max));
                    break;
            }
        }
        // 사망 보고(C_PlayerDead 송신)는 제거됨 — 플레이어 HP 서버 권위 승격(authority-model §4).
        // 서버 `Room.TickMonsters`가 자기 HP≤0 을 직접 감지해 S_PlayerDead 를 발행한다(클라 보고 불필요).
        // 로컬 다운 연출/입력 게이트는 PlayerCharacterAgent 가 HP≤0 예측으로 즉발 처리(즉발 손맛).

        // ── 활성 버프/디버프 → BuffView 중계 ───────────

        private void RefreshBuffs()
        {
            if (_asc == null)
                return;

            var snapshots = _asc.GetActiveEffectSnapshots();
            var views = new List<BuffView>(snapshots.Count);

            foreach (var s in snapshots)
            {
                var def = _effectCatalog?.Get(s.EffectId);
                var category = def?.Category ?? default;
                bool isBuff = ResolvePolarity(def);

                Sprite icon = _iconCatalog != null ? _iconCatalog.GetIcon(category) : null;
                Color tint  = _iconCatalog != null ? _iconCatalog.GetColor(isBuff) : Color.white;

                views.Add(new BuffView(
                    icon, tint,
                    s.RemainingMs / 1000f,
                    s.DurationMs / 1000f,
                    s.Stacks,
                    s.IsInfinite));
            }

            Dispatch(new InGameResult.BuffsChanged(views));
        }

        /// <summary>버프/디버프 색 판정: 정의의 명시값 우선, 없으면 modifier 부호 합으로.</summary>
        private static bool ResolvePolarity(GameplayEffectDefinition def)
        {
            if (def == null)
                return true;
            if (def.PolarityOverride.HasValue)
                return def.PolarityOverride.Value;

            int net = 0;
            foreach (var m in def.Modifiers)
            {
                switch (m.ModifierType)
                {
                    case EModifierType.Additive:       net += m.Amount; break;
                    case EModifierType.Multiplicative: net += m.Amount - 100; break; // 100 = ±0%
                }
            }
            return net >= 0;
        }

        // ── View의 단일 진입점 ────────────────────────

        public void Accept(InGameIntent intent)
        {
            // 인벤토리 토글은 비동기 Effect가 아닌 즉발 신호 — 처리 중 가드와 무관하게 항상 통과.
            if (intent is InGameIntent.ToggleInventory)
            {
                _toggleInventory.OnNext(Unit.Default);
                return;
            }

            if (intent is InGameIntent.ToggleEquipment)
            {
                _toggleEquipment.OnNext(Unit.Default);
                return;
            }

            if (intent is InGameIntent.ToggleShop)
            {
                _toggleShop.OnNext(Unit.Default);
                return;
            }

            if (intent is InGameIntent.ToggleQuest)
            {
                _toggleQuest.OnNext(Unit.Default);
                return;
            }

            if (intent is InGameIntent.ToggleAbility)
            {
                _toggleAbility.OnNext(Unit.Default);
                return;
            }

            if (_isProcessing)
            {
                Debug.LogWarning($"[InGameModel] {intent.GetType().Name} 무시됨 — 처리 중");
                return;
            }

            if (intent is InGameIntent.ReturnToLobby)
                ReturnToLobbyAsync().Forget();
        }

        // ── Effect ───────────────────────────────────

        private async UniTaskVoid ReturnToLobbyAsync()
        {
            _isProcessing = true;
            Dispatch(InGameResult.Returning.Instance);
            try
            {
                // 1. TCP 방 퇴장 패킷 전송 (다른 플레이어에게 S_PlayerLeft 브로드캐스트)
                Debug.Log("[InGameModel] C_PlayerLeave 전송 중...");
                await _socketSession.LeaveRoomAsync(_cts.Token);

                // 2. TCP 소켓 연결 해제
                Debug.Log("[InGameModel] 소켓 연결 해제 중...");
                await _socketSession.DisconnectAsync(_cts.Token);
                Debug.Log("[InGameModel] 소켓 연결 해제 완료");

                // 3. Main 씬으로 복귀
                Debug.Log("[InGameModel] Main 씬으로 복귀");
                await SceneManager.LoadSceneAsync("Main");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[InGameModel] ReturnToLobby 취소됨");
                _isProcessing = false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[InGameModel] ReturnToLobby 실패: {e}");
                Dispatch(new InGameResult.Failed(e.Message));
                _isProcessing = false;
            }
        }

        // ── Result → Reducer → State ──────────────────

        private void Dispatch(InGameResult result)
        {
            _state.Value = InGameReducer.Reduce(_state.Value, result);
        }

        public void Dispose()
        {
            _localPlayer.OnSet -= BindLocalPlayer;
            if (_packetState != null)
            {
                _packetState.OnDungeonReady -= OnDungeonReady;
                _packetState.OnDungeonCleared -= OnDungeonCleared;
                _packetState.OnDungeonFailed -= OnDungeonFailed;
                _packetState.OnItemPickedUp -= OnItemPickedUp;
            }
            if (_pickupNotifier != null)
                _pickupNotifier.OnPickup -= OnItemPickedUp;

            _socketSession.OnDisconnected -= OnSocketDisconnected;
            if (_uiCaptured) _inputContext?.ExitUi(); // 끊김 때 잡은 입력 점유 해제(전역 Singleton 누수 방지)
            if (_progression != null)
                _progression.OnChanged -= PushProgression;
            if (_asc != null)
            {
                _asc.OnAttributeChanged -= OnAttributeChanged;
                _asc.OnActiveEffectsChanged -= RefreshBuffs;
            }

            _cts.Cancel();
            _cts.Dispose();
            _state.Dispose();
            _toggleInventory.Dispose();
            _toggleEquipment.Dispose();
            _toggleShop.Dispose();
            _itemPickup.Dispose();
            _connectionLost.Dispose();
        }
    }
}
