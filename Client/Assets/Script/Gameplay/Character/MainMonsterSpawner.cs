using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Spawn;
using Game.Network.Socket;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Gameplay.Character
{
    /// <summary>Main 로컬 몬스터 스폰 설정 — 몬스터 프리팹 + 맵 id(스폰 슬롯은 spawn-layouts 에서) + 바닥아이템 프리팹.</summary>
    public sealed class MainMonsterSettings
    {
        public GameObject Prefab { get; }
        public string MapId { get; }
        public GameObject GroundItemPrefab { get; }

        public MainMonsterSettings(GameObject prefab, string mapId, GameObject groundItemPrefab)
        {
            Prefab = prefab;
            MapId = mapId;
            GroundItemPrefab = groundItemPrefab;
        }
    }

    /// <summary>
    /// Main(싱글) 로컬 몬스터 스포너 — B-lite. 던전 MonsterSpawner(서버 권위)의 자매.
    /// 스폰 위치·식별자는 서버와 같은 spawn-layouts(`SpawnLayoutProvider`)의 **몬스터 슬롯**에서 읽는다
    /// (서버가 ClaimKill 검증의 기준으로 보유 = 무한파밍 차단). 클라는 스폰·렌더·예측만.
    ///
    /// - 네트워크 미연결(Main)에서만 동작. Joined(던전)면 스폰 안 함(서버 스폰).
    /// - LocalMonster.OnDied → 사망 위치에 LocalGroundItem(슬롯) 스폰. **드랍 roll 은 클라가 안 함** —
    ///   줍기 시 서버 ClaimKill 이 슬롯 검증 후 권위 roll 로 지급한다. 정본 = main-spawn-claim.md.
    /// </summary>
    public sealed class MainMonsterSpawner : IAsyncStartable, IDisposable
    {
        private readonly ISocketSession      _socketSession;
        private readonly IObjectResolver     _container;
        private readonly MainMonsterSettings _settings;
        private readonly SpawnLayoutProvider _spawnLayouts;

        private readonly List<LocalMonster> _monsters = new();
        private readonly CancellationTokenSource _cts = new(); // 재스폰 대기 취소(Dispose)

        public MainMonsterSpawner(
            ISocketSession      socketSession,
            IObjectResolver     container,
            MainMonsterSettings settings,
            SpawnLayoutProvider spawnLayouts)
        {
            _socketSession = socketSession;
            _container     = container;
            _settings      = settings;
            _spawnLayouts  = spawnLayouts;
        }

        public UniTask StartAsync(CancellationToken ct)
        {
            // 던전(서버 연결)에서는 서버가 몬스터를 스폰한다 — Main 로컬 스폰 금지(이중 스폰 방지).
            if (_socketSession.State == SocketSessionState.Joined)
            {
                Debug.Log("[MainMonsterSpawner] 네트워크(던전) 모드 — 로컬 몬스터 스폰 없음");
                return UniTask.CompletedTask;
            }

            if (_settings.Prefab == null || string.IsNullOrEmpty(_settings.MapId))
            {
                Debug.LogWarning("[MainMonsterSpawner] 프리팹/맵 미설정 — 스폰 없음");
                return UniTask.CompletedTask;
            }

            var layout = _spawnLayouts.Get(_settings.MapId);
            foreach (var slot in layout.Monsters)
                Spawn(slot);

            Debug.Log($"[MainMonsterSpawner] Main 모드 — 슬롯 몬스터 {_monsters.Count}마리 스폰 (map {_settings.MapId})");
            return UniTask.CompletedTask;
        }

        private void Spawn(MonsterSlot slot)
        {
            var pos = new Vector3(slot.X, slot.Y, slot.Z);
            var go = UnityEngine.Object.Instantiate(_settings.Prefab, pos, Quaternion.identity);
            _container.InjectGameObject(go); // LocalMonster 의 [Inject] LocalPlayerContext 충족

            var monster = go.GetComponent<LocalMonster>();
            if (monster == null)
            {
                Debug.LogError("[MainMonsterSpawner] 프리팹에 LocalMonster 컴포넌트가 없습니다.");
                UnityEngine.Object.Destroy(go);
                return;
            }

            monster.Configure(slot.MonsterId, _settings.MapId, slot.SlotId); // 슬롯 식별자 주입(ClaimKill 키)
            monster.OnDied += HandleDied;
            _monsters.Add(monster);
        }

        private void HandleDied(LocalMonster monster)
        {
            monster.OnDied -= HandleDied;
            _monsters.Remove(monster);

            // 사망 위치에 전리품 오브 1개. 드랍 내용은 줍기 시 서버 ClaimKill 이 결정(클라 roll 없음).
            SpawnGroundItem(monster.MapId, monster.SlotId, monster.transform.position);
            Debug.Log($"[MainMonsterSpawner] 로컬 몬스터 사망 — {monster.MonsterId} (slot {monster.SlotId})");

            // 슬롯 쿨다운 후 같은 슬롯에 재스폰(서버 ClaimKill 쿨다운과 동일 값 → 보상도 그때 다시 가능).
            ScheduleRespawn(monster.SlotId).Forget();
        }

        /// <summary>슬롯의 RespawnCooldownMs 후 그 슬롯 몬스터를 다시 스폰한다. Dispose 시 취소.</summary>
        private async UniTaskVoid ScheduleRespawn(int slotId)
        {
            MonsterSlot slot = null;
            foreach (var s in _spawnLayouts.Get(_settings.MapId).Monsters)
                if (s.SlotId == slotId) { slot = s; break; }
            if (slot == null) return;

            int delayMs = Mathf.Max(slot.RespawnCooldownMs, 1000); // 데이터 0이면 1s 하한
            try
            {
                await UniTask.Delay(delayMs, cancellationToken: _cts.Token);
            }
            catch (OperationCanceledException)
            {
                return; // 씬 종료/Dispose
            }

            Spawn(slot);
            Debug.Log($"[MainMonsterSpawner] 슬롯 재스폰 — slot {slotId} ({delayMs}ms 경과)");
        }

        private void SpawnGroundItem(string mapId, int slotId, Vector3 pos)
        {
            if (_settings.GroundItemPrefab == null || slotId <= 0)
                return;

            var go = UnityEngine.Object.Instantiate(_settings.GroundItemPrefab, pos, Quaternion.identity);
            _container.InjectGameObject(go); // LocalGroundItem 의 [Inject] IInventoryGrpcService 충족

            var item = go.GetComponent<LocalGroundItem>();
            if (item == null)
            {
                Debug.LogError("[MainMonsterSpawner] GroundItemPrefab 에 LocalGroundItem 컴포넌트가 없습니다.");
                UnityEngine.Object.Destroy(go);
                return;
            }

            item.Initialize(mapId, slotId, pos);
            Debug.Log($"[MainMonsterSpawner] 전리품 오브 스폰 — map {mapId} slot {slotId}");
        }

        public void Dispose()
        {
            _cts.Cancel(); // 대기 중인 재스폰 취소
            _cts.Dispose();

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
