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
        private readonly PartyAscRegistry _partyRegistry;

        public EffectReceiver(
            ISocketPacketState state,
            GameplayEffectCatalog catalog,
            LocalPlayerContext localPlayer,
            AuthSession authSession,
            PartyAscRegistry partyRegistry)
        {
            _state = state;
            _catalog = catalog;
            _localPlayer = localPlayer;
            _authSession = authSession;
            _partyRegistry = partyRegistry;
        }

        public void Initialize()
        {
            _state.OnEffectApplied += OnEffectApplied;
            _state.OnEffectRemoved += OnEffectRemoved;
            _state.OnManaUpdated += OnManaUpdated;
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

            // 서버가 Amount(스탯 반영 Health 델타)를 보냈으면 카탈로그 고정값 대신 그 값을 적용(서버 권위).
            target.ApplyEffectAuthoritative(def, data.InstanceId, data.Stacks, data.Amount);

            // 로컬 플레이어가 받은 효과(데미지/회복)와 적용 후 HP 로그.
            var hp = target.GetAttribute(EGameplayAttribute.Health);
            Debug.Log($"[EffectReceiver] 효과 적용 — EffectId={data.EffectId} amount={data.Amount} → HP={hp?.CurrentValue}/{hp?.MaxValue}");
        }

        private void OnEffectRemoved(int instanceId)
        {
            // 현재 로컬만 라우팅 — 원격 레지스트리 도입 시 대상별 제거로 확장.
            _localPlayer.AbilitySystem?.RemoveEffect(instanceId);
        }

        /// <summary>
        /// 서버 권위 마나 정정(차감/거부/입장 초기화). 로컬 플레이어 것이면 ASC.Mana 를 서버 값으로 덮어쓴다.
        /// MaxMana(레벨테이블 권위)로 상한을 먼저 맞추고(클라 prefab 기준선 정렬) Current 를 정정한다.
        /// 리젠은 PlayerCharacterAgent 가 동일 rate 로 예측 — 이 정정은 발동 순간에만 도착한다.
        /// </summary>
        private void OnManaUpdated(long userId, int mana, int maxMana)
        {
            if (_authSession == null || userId != _authSession.UserId)
                return; // owner-only 패킷이지만 방어적으로 대상 확인.

            var attr = _localPlayer.AbilitySystem?.GetAttribute(EGameplayAttribute.Mana);
            if (attr == null)
                return;

            if (maxMana > 0 && maxMana != attr.MaxValue)
                attr.SetMax(maxMana);
            attr.SetCurrent(mana);
        }

        private AbilitySystemComponent ResolveTarget(long targetId)
        {
            // 파티 레지스트리로 로컬·원격 모두 라우팅(CharacterSpawner 가 스폰 시 등록). 파티 HP HUD 가 이 경로로 GAS HP 추적.
            if (_partyRegistry != null && _partyRegistry.TryGet(targetId, out var asc))
                return asc;
            // 폴백(레지스트리 미등록 타이밍): 로컬은 LocalPlayerContext 로.
            if (_authSession != null && targetId == _authSession.UserId)
                return _localPlayer.AbilitySystem;
            return null;
        }

        public void Dispose()
        {
            _state.OnEffectApplied -= OnEffectApplied;
            _state.OnEffectRemoved -= OnEffectRemoved;
            _state.OnManaUpdated -= OnManaUpdated;
        }
    }
}
