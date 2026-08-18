using System;
using System.Collections.Generic;
using Game.System.Player;
using R3;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using VContainer.Unity;

namespace Game.Presentation.Inventory
{
    /// <summary>
    /// Side Effect 핸들러 — InventoryModel.OnConsumableUsed(차감 성공 신호) 구독 →
    /// ConsumableCatalog 데이터로 GameplayEffectDefinition 빌드 → 로컬 ASC.ApplyEffect.
    ///
    /// 회복 적용 = 클라 권위(플레이어 HP). 적용은 GAS funnel 로 — ASC.OnAttributeChanged → HUD 자동 갱신.
    /// (LobbyModel.NavigateToRoom 을 View 가 구독해 화면전환하는 것과 동일 구도 — Model 은 신호만, 구독자가 효과)
    /// </summary>
    public sealed class ConsumableEffectHandler : IInitializable, IDisposable
    {
        private readonly InventoryModel    _model;
        private readonly ConsumableCatalog _catalog;
        private readonly LocalPlayerContext _localPlayer;

        private IDisposable _subscription;

        public ConsumableEffectHandler(InventoryModel model, ConsumableCatalog catalog, LocalPlayerContext localPlayer)
        {
            _model       = model;
            _catalog     = catalog;
            _localPlayer = localPlayer;
        }

        public void Initialize()
            => _subscription = _model.OnConsumableUsed.Subscribe(ApplyEffects);

        private void ApplyEffects(int itemId)
        {
            var asc = _localPlayer?.AbilitySystem;
            if (asc == null)
            {
                Debug.LogWarning($"[ConsumableEffectHandler] 로컬 ASC 없음 — '{itemId}' 효과 적용 스킵");
                return;
            }

            var def = _catalog != null ? _catalog.Get(itemId) : null;
            if (def == null || def.effects == null || def.effects.Count == 0)
            {
                Debug.LogWarning($"[ConsumableEffectHandler] '{itemId}' 소모품 효과 정의 없음(ConsumableCatalog)");
                return;
            }

            // 각 효과를 자기 정책(Instant/Duration)대로 개별 GameplayEffect 로 적용 — GAS 가 Health 등 Resource 를 즉시/지속 반영.
            foreach (var e in def.effects)
            {
                var gameplayDef = new GameplayEffectDefinition(
                    id: $"consume_{itemId}_{e.stat}",
                    category: EEffectCategory.AttackPower, // Instant 회복은 버프 아이콘 없음 → category cosmetic
                    policy: e.policy,
                    durationMs: e.durationMs,
                    modifiers: new List<GameplayAttributeModifier>
                    {
                        GameplayAttributeModifier.Create(e.stat, e.amount, EModifierType.Additive),
                    });
                asc.ApplyEffect(gameplayDef);
            }

            Debug.Log($"[ConsumableEffectHandler] '{itemId}' 효과 {def.effects.Count}개 적용(로컬 ASC)");
        }

        public void Dispose() => _subscription?.Dispose();
    }
}
