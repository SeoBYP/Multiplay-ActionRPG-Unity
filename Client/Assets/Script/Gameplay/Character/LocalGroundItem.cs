using System;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Interfaces;
using GameServer.Grpc.Inventory;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// Main(싱글) 바닥 아이템 + 줍기. 던전 GroundItemEntity(C_PickupItem→서버 권위 중재)와 달리,
    /// 싱글이라 경쟁이 없어 클라가 직접 지급한다 — 줍기 = `GrantItem` gRPC(증분8, 서버 가드) 호출.
    ///
    /// IInteractable 이라 InteractionDetector 가 근처에서 최근접 선택 → 로컬 드라이버가 E키로 Interact 호출.
    /// 진실원은 서버 DB(지급은 서버 가드 통과해야 확정). 줍기 성공 시 디스폰 + 토스트(현재 로그).
    /// 프리팹에는 InteractionDetector 감지 레이어의 Collider 가 있어야 한다(GroundItem.prefab 과 동일 구성).
    /// </summary>
    public sealed class LocalGroundItem : MonoBehaviour, IInteractable
    {
        /// <summary>드랍 위치를 가슴 높이로 띄우는 오프셋(던전 교훈 — 바닥이면 감지 구체에 안 닿음).</summary>
        private const float SpawnYOffset = 0.7f;

        [Inject] private readonly IInventoryGrpcService _inventory = null;

        private string _itemId = string.Empty;
        private int _qty;
        private bool _picked;

        public void Initialize(string itemId, int qty, Vector3 pos)
        {
            _itemId = itemId;
            _qty    = qty;
            transform.position = new Vector3(pos.x, pos.y + SpawnYOffset, pos.z);
        }

        public void Interact(GameObject interactor)
        {
            if (_picked || _inventory == null || string.IsNullOrEmpty(_itemId))
                return;

            _picked = true; // 이중 줍기 방지(성공 전까지 1회만 시도). 실패 시 아래서 해제.
            GrantAsync().Forget();
        }

        private async UniTaskVoid GrantAsync()
        {
            try
            {
                var res = await _inventory.GrantItemAsync(
                    new GrantItemRequest { ItemId = _itemId, Qty = _qty }, destroyCancellationToken);

                if (res.Result.Success)
                {
                    // TODO(7.x): 정식 획득 토스트 위젯. 현재는 로그.
                    Debug.Log($"[LocalGroundItem] 획득 — {_itemId} x{_qty} (보유 {res.NewQuantity})");
                    Destroy(gameObject);
                }
                else
                {
                    Debug.LogWarning($"[LocalGroundItem] 지급 거부 — {_itemId}: {res.Result.Message}");
                    _picked = false; // 재시도 허용
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LocalGroundItem] 지급 실패 — {e.Message}");
                _picked = false;
            }
        }
    }
}
