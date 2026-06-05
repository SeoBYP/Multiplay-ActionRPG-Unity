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
    /// 서버 권위 몬스터 스포너(Dungeon 전용). CharacterSpawner 의 자매 — 플레이어가 아닌 몬스터 담당.
    ///
    /// - OnMonsterSpawned → MonsterPrefab 인스턴스화 + MonsterEntity 초기화(보간 시작)
    /// - OnMonsterDead    → 디스폰
    /// 구독 전 이미 도착한 몬스터는 GetAllMonsters() 로 초기 스폰한다(로스터 유실 방지).
    /// </summary>
    public class MonsterSpawner : IAsyncStartable, IDisposable
    {
        private readonly ISocketSession         _socketSession;
        private readonly ISocketPacketState     _packetState;
        private readonly CharacterPrefabSettings _prefabs;

        private readonly Dictionary<int, MonsterEntity> _monsters = new Dictionary<int, MonsterEntity>();

        public MonsterSpawner(
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
            // 몬스터는 Dungeon(서버 연결) 에서만 존재. Main 씬 등 비네트워크는 무시.
            if (_socketSession.State != SocketSessionState.Joined)
            {
                Debug.Log("[MonsterSpawner] 비네트워크 모드 — 몬스터 스폰 없음");
                return UniTask.CompletedTask;
            }

            // 구독 먼저 → 구독과 초기 스폰 사이 도착분 유실 방지(중복은 ContainsKey 가드로 흡수).
            _packetState.OnMonsterSpawned += HandleSpawned;
            _packetState.OnMonsterDead    += HandleDead;

            foreach (var snapshot in _packetState.GetAllMonsters())
                Spawn(snapshot);

            Debug.Log($"[MonsterSpawner] Dungeon 모드 — 초기 몬스터={_monsters.Count}마리");
            return UniTask.CompletedTask;
        }

        private void HandleSpawned(SocketMonsterSnapshot snapshot) => Spawn(snapshot);
        private void HandleDead(int instanceId) => Despawn(instanceId);

        private void Spawn(SocketMonsterSnapshot snapshot)
        {
            if (_monsters.ContainsKey(snapshot.InstanceId)) return;

            var prefab = _prefabs.MonsterPrefab;
            if (prefab == null)
            {
                Debug.LogError("[MonsterSpawner] MonsterPrefab이 설정되지 않았습니다.");
                return;
            }

            var pos = new Vector3(snapshot.PosX, snapshot.PosY, snapshot.PosZ);
            var rot = Quaternion.Euler(0f, snapshot.RotY, 0f);
            var go  = UnityEngine.Object.Instantiate(prefab, pos, rot);

            var entity = go.GetComponent<MonsterEntity>();
            if (entity == null)
            {
                Debug.LogError("[MonsterSpawner] MonsterPrefab에 MonsterEntity 컴포넌트가 없습니다.");
                UnityEngine.Object.Destroy(go);
                return;
            }

            entity.Initialize(snapshot.InstanceId, _packetState);
            _monsters[snapshot.InstanceId] = entity;

            Debug.Log($"[MonsterSpawner] 몬스터 스폰 — InstanceId={snapshot.InstanceId} MonsterId={snapshot.MonsterId}");
        }

        private void Despawn(int instanceId)
        {
            if (!_monsters.TryGetValue(instanceId, out var entity)) return;
            _monsters.Remove(instanceId);
            entity.Dispose();
            if (entity != null) UnityEngine.Object.Destroy(entity.gameObject);
            Debug.Log($"[MonsterSpawner] 몬스터 디스폰 — InstanceId={instanceId}");
        }

        public void Dispose()
        {
            _packetState.OnMonsterSpawned -= HandleSpawned;
            _packetState.OnMonsterDead    -= HandleDead;

            foreach (var entity in _monsters.Values)
            {
                entity.Dispose();
                if (entity != null) UnityEngine.Object.Destroy(entity.gameObject);
            }
            _monsters.Clear();
        }
    }
}
