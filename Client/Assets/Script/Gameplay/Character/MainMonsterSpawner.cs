using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Loot;
using Game.Network.Socket;
using Shared.Gameplay;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Gameplay.Character
{
    /// <summary>Main 로컬 몬스터 스폰 설정(몬스터 프리팹 + 스폰 위치 + 바닥아이템 프리팹). MainLifetimeScope 가 인스턴스로 등록.</summary>
    public sealed class MainMonsterSettings
    {
        public GameObject Prefab { get; }
        public IReadOnlyList<Vector3> SpawnPoints { get; }
        public GameObject GroundItemPrefab { get; }

        public MainMonsterSettings(GameObject prefab, IReadOnlyList<Vector3> spawnPoints, GameObject groundItemPrefab)
        {
            Prefab = prefab;
            SpawnPoints = spawnPoints ?? Array.Empty<Vector3>();
            GroundItemPrefab = groundItemPrefab;
        }
    }

    /// <summary>
    /// Main(싱글) 로컬 몬스터 스포너. 던전 MonsterSpawner(서버 권위)의 자매 — 클라 권위 LocalMonster 담당.
    ///
    /// - 네트워크 미연결(Main)에서만 동작. Joined(던전)면 스폰하지 않는다(던전은 서버가 스폰).
    /// - 스폰 시 _container.InjectGameObject 로 LocalMonster 에 LocalPlayerContext 주입(AI 타깃).
    /// - LocalMonster.OnDied → 디스폰. 드랍(roll→GroundItem→GrantItem)은 9d 에서 이 자리에 합류.
    /// </summary>
    public sealed class MainMonsterSpawner : IAsyncStartable, IDisposable
    {
        private readonly ISocketSession      _socketSession;
        private readonly IObjectResolver     _container;
        private readonly MainMonsterSettings _settings;
        private readonly DropTableDefinition _dropTable;

        private readonly List<LocalMonster> _monsters = new();
        private readonly global::System.Random _rng = new();

        public MainMonsterSpawner(
            ISocketSession      socketSession,
            IObjectResolver     container,
            MainMonsterSettings settings,
            DropTableDefinition dropTable)
        {
            _socketSession = socketSession;
            _container     = container;
            _settings      = settings;
            _dropTable     = dropTable;
        }

        public UniTask StartAsync(CancellationToken ct)
        {
            // 던전(서버 연결)에서는 서버가 몬스터를 스폰한다 — Main 로컬 스폰 금지(이중 스폰 방지).
            if (_socketSession.State == SocketSessionState.Joined)
            {
                Debug.Log("[MainMonsterSpawner] 네트워크(던전) 모드 — 로컬 몬스터 스폰 없음");
                return UniTask.CompletedTask;
            }

            if (_settings.Prefab == null)
            {
                Debug.LogWarning("[MainMonsterSpawner] LocalMonster 프리팹 미할당 — 스폰 없음");
                return UniTask.CompletedTask;
            }

            foreach (var point in _settings.SpawnPoints)
                Spawn(point);

            Debug.Log($"[MainMonsterSpawner] Main 모드 — 로컬 몬스터 {_monsters.Count}마리 스폰");
            return UniTask.CompletedTask;
        }

        private void Spawn(Vector3 point)
        {
            var go = UnityEngine.Object.Instantiate(_settings.Prefab, point, Quaternion.identity);
            _container.InjectGameObject(go); // LocalMonster 의 [Inject] LocalPlayerContext 충족

            var monster = go.GetComponent<LocalMonster>();
            if (monster == null)
            {
                Debug.LogError("[MainMonsterSpawner] 프리팹에 LocalMonster 컴포넌트가 없습니다.");
                UnityEngine.Object.Destroy(go);
                return;
            }

            monster.OnDied += HandleDied;
            _monsters.Add(monster);
        }

        private void HandleDied(LocalMonster monster)
        {
            monster.OnDied -= HandleDied;
            _monsters.Remove(monster);

            // 사망 위치에서 드랍 roll → 바닥 아이템 스폰. 위치는 Destroy 전(이번 프레임) 유효.
            var dropPos = monster.transform.position;
            RollAndSpawnDrops(monster.MonsterId, dropPos);
            Debug.Log($"[MainMonsterSpawner] 로컬 몬스터 사망 — {monster.MonsterId}");
        }

        /// <summary>SO(DropTableDefinition) 데이터 → 공유 DropTableRoll(서버 던전과 동일 로직) → LocalGroundItem 스폰.</summary>
        private void RollAndSpawnDrops(string monsterId, Vector3 pos)
        {
            if (_dropTable == null || _settings.GroundItemPrefab == null)
                return;

            var entries = new List<DropEntry>();
            foreach (var d in _dropTable.Get(monsterId))
                entries.Add(new DropEntry(d.itemId, d.chance, d.minQty, d.maxQty));

            foreach (var drop in DropTableRoll.Roll(entries, _rng))
                SpawnGroundItem(drop.ItemId, drop.Qty, pos);
        }

        private void SpawnGroundItem(string itemId, int qty, Vector3 pos)
        {
            var go = UnityEngine.Object.Instantiate(_settings.GroundItemPrefab, pos, Quaternion.identity);
            _container.InjectGameObject(go); // LocalGroundItem 의 [Inject] IInventoryGrpcService 충족

            var item = go.GetComponent<LocalGroundItem>();
            if (item == null)
            {
                Debug.LogError("[MainMonsterSpawner] GroundItemPrefab 에 LocalGroundItem 컴포넌트가 없습니다.");
                UnityEngine.Object.Destroy(go);
                return;
            }

            item.Initialize(itemId, qty, pos);
            Debug.Log($"[MainMonsterSpawner] 드랍 스폰 — {itemId} x{qty}");
        }

        public void Dispose()
        {
            foreach (var monster in _monsters)
            {
                if (monster == null) continue;
                monster.OnDied -= HandleDied;
                UnityEngine.Object.Destroy(monster.gameObject);
            }
            _monsters.Clear();
        }
    }
}
