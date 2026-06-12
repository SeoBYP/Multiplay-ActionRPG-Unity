using System;
using Game.Network.Socket;
using Game.System.Auth;
using Game.System.Player;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using VContainer.Unity;

namespace Game.Presentation.InGame
{
    /// <summary>
    /// EF-2d: 서버가 보낸 Effect(버프/디버프)를 받아 대상 ASC에 적용한다.
    ///
    /// 흐름: 네트워크 핸들러 → ISocketPacketState 이벤트 → (여기) 카탈로그 조회 + 타겟 라우팅 →
    ///       AbilitySystemComponent.ApplyEffectAuthoritative(서버 InstanceId 키).
    ///
    /// 라우팅: 현재는 로컬 플레이어(TargetId == 내 UserId)만. 원격 캐릭터 ASC 레지스트리는 후속.
    /// </summary>
    public sealed class EffectReceiver : IInitializable, IDisposable
    {
        private readonly ISocketPacketState _state;
        private readonly GameplayEffectCatalog _catalog;
        private readonly LocalPlayerContext _localPlayer;
        private readonly AuthSession _authSession;

        public EffectReceiver(
            ISocketPacketState state,
            GameplayEffectCatalog catalog,
            LocalPlayerContext localPlayer,
            AuthSession authSession)
        {
            _state = state;
            _catalog = catalog;
            _localPlayer = localPlayer;
            _authSession = authSession;
        }

        public void Initialize()
        {
            _state.OnEffectApplied += OnEffectApplied;
            _state.OnEffectRemoved += OnEffectRemoved;
        }

        private void OnEffectApplied(SocketEffectApply data)
        {
            var target = ResolveTarget(data.TargetId);
            if (target == null)
                return; // 원격 대상 또는 로컬 캐릭터 미스폰 — 원격 ASC 레지스트리는 후속

            var def = _catalog != null ? _catalog.Get(data.EffectId) : null;
            if (def == null)
            {
                Debug.LogWarning($"[EffectReceiver] 알 수 없는 EffectId='{data.EffectId}' — 무시");
                return;
            }

            target.ApplyEffectAuthoritative(def, data.InstanceId, data.Stacks);

            // 로컬 플레이어가 받은 효과(데미지/회복)와 적용 후 HP 로그.
            var hp = target.GetAttribute(EGameplayAttribute.Health);
            Debug.Log($"[EffectReceiver] 효과 적용 — EffectId={data.EffectId} → HP={hp?.CurrentValue}/{hp?.MaxValue}");
        }

        private void OnEffectRemoved(int instanceId)
        {
            // 현재 로컬만 라우팅 — 원격 레지스트리 도입 시 대상별 제거로 확장.
            _localPlayer.AbilitySystem?.RemoveEffect(instanceId);
        }

        private AbilitySystemComponent ResolveTarget(long targetId)
        {
            if (_authSession != null && targetId == _authSession.UserId)
                return _localPlayer.AbilitySystem;
            return null;
        }

        public void Dispose()
        {
            _state.OnEffectApplied -= OnEffectApplied;
            _state.OnEffectRemoved -= OnEffectRemoved;
        }
    }
}
