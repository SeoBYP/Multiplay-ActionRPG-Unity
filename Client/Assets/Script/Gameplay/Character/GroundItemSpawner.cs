using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using UnityEngine;
using VContainer.Unity;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 서버 권위 바닥 아이템 스포너(Dungeon 전용). MonsterSpawner 의 자매 — 드랍된 루트 담당.
    ///
    /// - OnGroundItemSpawned → GroundItemPrefab 인스턴스화 + GroundItemEntity 초기화
    /// - OnGroundItemRemoved → 디스폰(누군가 주웠거나 만료)
    /// - OnItemPickedUp      → 획득 토스트(현재 로그 — 정식 토스트 위젯은 7.x UI 후속)
    /// 구독 전 이미 도착한 바닥 아이템은 GetAllGroundItems() 로 초기 스폰(늦은 입장 로스터 유실 방지).
    /// </summary>
    public class GroundItemSpawner : IAsyncStartable, IDisposable
    {
        private readonly ISocketSession          _socketSession;
        private readonly ISocketPacketState      _packetState;
        private readonly CharacterPrefabSettings _prefabs;

        private readonly Dictionary<int, GroundItemEntity> _items = new Dictionary<int, GroundItemEntity>();

        public GroundItemSpawner(
            ISocketSession          socketSession,
            ISocketPacketState      packetState,
            CharacterPrefabSettings prefabs)
        {
            _socketSession = socketSession;
            _packetState   = packetState;
            _prefabs       = prefabs;
        }

        public UniTask StartAsync(CancellationToken ct)
        {
            // 바닥 아이템은 Dungeon(서버 연결) 에서만 존재. Main 등 비네트워크는 무시.
            if (_socketSession.State != SocketSessionState.Joined)
            {
                Debug.Log("[GroundItemSpawner] 비네트워크 모드 — 바닥 아이템 없음");
                return UniTask.CompletedTask;
            }

            _packetState.OnGroundItemSpawned += HandleSpawned;
            _packetState.OnGroundItemRemoved += HandleRemoved;
            _packetState.OnItemPickedUp      += HandlePickedUp;

            foreach (var snapshot in _packetState.GetAllGroundItems())
                Spawn(snapshot);

            Debug.Log($"[GroundItemSpawner] Dungeon 모드 — 초기 바닥 아이템={_items.Count}개");
            return UniTask.CompletedTask;
        }

        private void HandleSpawned(SocketGroundItemSnapshot snapshot) => Spawn(snapshot);
        private void HandleRemoved(int groundId) => Despawn(groundId);

        private void HandlePickedUp(string itemId, int qty)
        {
            // 정식 획득 토스트 위젯은 7.x UI 후속. 현재는 검증용 로그.
            Debug.Log($"[GroundItemSpawner] 아이템 획득 — ItemId={itemId} x{qty}");
        }

        private void Spawn(SocketGroundItemSnapshot snapshot)
        {
            if (_items.ContainsKey(snapshot.GroundId)) return;

            var prefab = _prefabs.GroundItemPrefab;
            if (prefab == null)
            {
                Debug.LogError("[GroundItemSpawner] GroundItemPrefab이 설정되지 않았습니다.");
                return;
            }

            var pos = new Vector3(snapshot.PosX, snapshot.PosY, snapshot.PosZ);
            var go  = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);

            var entity = go.GetComponent<GroundItemEntity>();
            if (entity == null)
            {
                Debug.LogError("[GroundItemSpawner] GroundItemPrefab에 GroundItemEntity 컴포넌트가 없습니다.");
                UnityEngine.Object.Destroy(go);
                return;
            }

            entity.Initialize(snapshot, _socketSession);
            _items[snapshot.GroundId] = entity;

            Debug.Log($"[GroundItemSpawner] 바닥 아이템 스폰 — GroundId={snapshot.GroundId} ItemId={snapshot.ItemId} x{snapshot.Qty}");
        }

        private void Despawn(int groundId)
        {
            if (!_items.TryGetValue(groundId, out var entity)) return;
            _items.Remove(groundId);
            if (entity != null) UnityEngine.Object.Destroy(entity.gameObject);
            Debug.Log($"[GroundItemSpawner] 바닥 아이템 디스폰 — GroundId={groundId}");
        }

        public void Dispose()
        {
            _packetState.OnGroundItemSpawned -= HandleSpawned;
            _packetState.OnGroundItemRemoved -= HandleRemoved;
            _packetState.OnItemPickedUp      -= HandlePickedUp;

            foreach (var entity in _items.Values)
                if (entity != null) UnityEngine.Object.Destroy(entity.gameObject);
            _items.Clear();
        }
    }
}
