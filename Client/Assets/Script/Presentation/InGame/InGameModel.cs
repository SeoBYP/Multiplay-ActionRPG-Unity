using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using Game.System.Player;
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
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly ReactiveProperty<InGameState> _state
            = new ReactiveProperty<InGameState>(InGameState.Initial);

        private AbilitySystemComponent _asc;
        private bool _isProcessing;
        private bool _localDeadReported;

        public ReadOnlyReactiveProperty<InGameState> State =>
            _state.ToReadOnlyReactiveProperty();

        public InGameModel(
            ISocketSession socketSession,
            LocalPlayerContext localPlayer,
            GameplayEffectCatalog effectCatalog = null,
            EffectIconCatalog iconCatalog = null,
            ISocketPacketState packetState = null)
        {
            _socketSession = socketSession;
            _localPlayer   = localPlayer;
            _effectCatalog = effectCatalog;
            _iconCatalog   = iconCatalog;
            _packetState   = packetState;
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
            }
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
                    if (current <= 0)
                        ReportLocalDeath();
                    break;
                case EGameplayAttribute.Mana:
                    Dispatch(new InGameResult.MpChanged(current, max));
                    break;
            }
        }

        /// <summary>
        /// 로컬 플레이어 HP 0 → 서버에 C_PlayerDead 1회 보고(플레이어 HP=클라 권위).
        /// 서버는 참가자 전원 다운 시 S_DungeonFailed 를 발화한다.
        /// </summary>
        private void ReportLocalDeath()
        {
            if (_localDeadReported)
                return;
            _localDeadReported = true;
            Debug.Log("[InGameModel] 로컬 플레이어 사망 — C_PlayerDead 송신");
            _socketSession.SendAsync(new C_PlayerDead(), _cts.Token).Forget();
        }

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
            }
            if (_asc != null)
            {
                _asc.OnAttributeChanged -= OnAttributeChanged;
                _asc.OnActiveEffectsChanged -= RefreshBuffs;
            }

            _cts.Cancel();
            _cts.Dispose();
            _state.Dispose();
        }
    }
}
