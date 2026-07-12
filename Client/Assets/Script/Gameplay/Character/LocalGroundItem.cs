using System;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Interfaces;
using Game.System.Player;
using GameServer.Grpc.Inventory;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// Main(싱글) 바닥 아이템 + 줍기 — B-lite 서버 검증. 오브 자체는 "이 킬에 전리품 있음"만 표시하고,
    /// **보상 내용·정원·쿨다운은 서버가 결정**한다(클라가 itemId/qty 지정하던 무한파밍 핵 차단).
    /// 줍기 = `ClaimKill(mapId, slotId)` gRPC → 서버가 슬롯 검증 + 쿨다운 + 권위 roll → 지급분(granted) 반환.
    /// 정본 = docs/wiki/main-spawn-claim.md.
    ///
    /// IInteractable 이라 InteractionDetector 가 근처에서 최근접 선택 → 로컬 드라이버가 E키로 Interact 호출.
    /// 진실원 = 서버 DB. 프리팹에는 InteractionDetector 감지 레이어의 Collider 가 있어야 한다.
    /// </summary>
    public sealed class LocalGroundItem : MonoBehaviour, IInteractable
    {
        /// <summary>드랍 위치를 가슴 높이로 띄우는 오프셋(던전 교훈 — 바닥이면 감지 구체에 안 닿음).</summary>
        private const float SpawnYOffset = 0.7f;

        [Inject] private readonly IInventoryGrpcService _inventory = null;
        [Inject] private readonly ItemPickupNotifier _pickupNotifier = null; // 획득 토스트(GameHud) 통지

        private string _mapId = string.Empty;
        private int _slotId;
        private bool _picked;

        public void Initialize(string mapId, int slotId, Vector3 pos)
        {
            _mapId  = mapId;
            _slotId = slotId;
            transform.position = new Vector3(pos.x, pos.y + SpawnYOffset, pos.z);
        }

        public void Interact(GameObject interactor)
        {
            if (_picked || _inventory == null || _slotId <= 0 || string.IsNullOrEmpty(_mapId))
                return;

            _picked = true; // 이중 줍기 방지(성공 전까지 1회만 시도). 실패 시 아래서 해제.
            ClaimAsync().Forget();
        }

        private async UniTaskVoid ClaimAsync()
        {
            try
            {
                var res = await _inventory.ClaimKillAsync(
                    new ClaimKillRequest { MapId = _mapId, SlotId = _slotId }, destroyCancellationToken);

                if (res.Result.Success)
                {
                    // 서버가 roll 한 실제 보상(쿨다운이면 빈 목록 = 이미 클레임됨).
                    // 경험치는 줍기가 아니라 킬 즉시(MainMonsterSpawner→ClaimMonsterExp)로 별도 지급.
                    foreach (var g in res.Granted)
                    {
                        Debug.Log($"[LocalGroundItem] 획득 — {g.ItemId} x{g.Qty} (보유 {g.NewQuantity})");
                        _pickupNotifier?.Notify(g.ItemId, g.Qty); // 던전 소켓 경로와 동일 획득 토스트(GameHud)
                    }
                    if (res.Granted.Count == 0)
                        Debug.Log($"[LocalGroundItem] 보상 없음(쿨다운/드랍없음) — map {_mapId} slot {_slotId}");
                    Destroy(gameObject); // 클레임 처리됨 → 오브 소멸
                }
                else
                {
                    Debug.LogWarning($"[LocalGroundItem] 클레임 거부 — slot {_slotId}: {res.Result.Message}");
                    _picked = false; // 재시도 허용
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LocalGroundItem] 클레임 실패 — {e.Message}");
                _picked = false;
            }
        }
    }
}
