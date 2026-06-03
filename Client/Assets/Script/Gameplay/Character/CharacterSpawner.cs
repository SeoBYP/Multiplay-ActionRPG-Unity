using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Spawn;
using Game.Network.Socket;
using Game.System.Auth;
using Game.System.Player;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// Main / Dungeon 씬 공통 캐릭터 스포너.
    ///
    /// - 항상: 로컬 플레이어 캐릭터 스폰
    /// - SocketSession.State == Joined (Dungeon) 시 추가:
    ///     * 로컬 캐릭터에 MoveSyncSender 동적 부착
    ///     * 현재 방 원격 플레이어 스폰
    ///     * 이후 OnPlayerJoined / OnPlayerLeft 구독으로 동적 스폰/디스폰
    /// </summary>
    public class CharacterSpawner : IAsyncStartable, IDisposable
    {
        private readonly ISocketSession         _socketSession;
        private readonly ISocketPacketState     _packetState;
        private readonly AuthSession            _authSession;
        private readonly IObjectResolver        _container;
        private readonly CharacterPrefabSettings _prefabs;
        private readonly LocalPlayerContext     _localPlayer;
        private readonly SpawnLayoutProvider    _spawnLayouts;

        private GameObject _localCharacterGo;
        private readonly Dictionary<long, RemoteDriver> _remotes = new Dictionary<long, RemoteDriver>();

        public CharacterSpawner(
            ISocketSession          socketSession,
            ISocketPacketState      packetState,
            AuthSession             authSession,
            IObjectResolver         container,
            CharacterPrefabSettings prefabs,
            LocalPlayerContext      localPlayer,
            SpawnLayoutProvider     spawnLayouts)
        {
            _socketSession = socketSession;
            _packetState   = packetState;
            _authSession   = authSession;
            _container     = container;
            _prefabs       = prefabs;
            _localPlayer   = localPlayer;
            _spawnLayouts  = spawnLayouts;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            Debug.Log($"[CharacterSpawner] StartAsync — SocketState={_socketSession.State}");

            await SpawnLocalAsync(ct);

            if (_socketSession.State != SocketSessionState.Joined)
            {
                Debug.Log("[CharacterSpawner] Main 씬 모드 — 네트워크 동기화 없음");
                return;
            }

            AttachMoveSyncSender();
            // 구독을 먼저 한다 — 구독과 초기 스폰 사이에 도착하는 로스터 패킷 유실 방지.
            // 중복은 SpawnRemote의 _remotes.ContainsKey 가드로 흡수된다.
            SubscribeNetworkEvents();
            SpawnInitialRemotes();
            Debug.Log($"[CharacterSpawner] Dungeon 모드 — 초기 원격 플레이어={_remotes.Count}명");
        }

        // ── 로컬 캐릭터 ──────────────────────────────────

        private async UniTask SpawnLocalAsync(CancellationToken ct)
        {
            var prefab = _prefabs.LocalPlayerPrefab;
            if (prefab == null)
            {
                Debug.LogError("[CharacterSpawner] LocalPlayerPrefab이 설정되지 않았습니다.");
                return;
            }

            var (spawnPos, spawnRot) = await ResolveLocalSpawnPoseAsync(ct);
            var go = UnityEngine.Object.Instantiate(prefab, spawnPos, spawnRot);
            _container.InjectGameObject(go);
            _localCharacterGo = go;

            await UniTask.Yield(ct); // 프레임 경계 대기 — Awake/Start 완료 보장

            // 로컬 ASC를 공유 컨텍스트에 등록 → InGameModel이 HUD로 스탯을 중계한다.
            var asc = go.GetComponent<AbilitySystemComponent>();
            if (asc != null)
                _localPlayer.Set(asc);
            else
                Debug.LogError("[CharacterSpawner] 로컬 프리팹에 AbilitySystemComponent가 없습니다.");

            Debug.Log($"[CharacterSpawner] 로컬 캐릭터 스폰 완료 — pos={spawnPos} prefab={prefab.name}");
        }

        /// <summary>
        /// Dungeon: MapId 레이아웃 + 내 SpawnIndex 로 스폰 포즈를 결정론 계산(서버와 동일 결과).
        ///          self S_PlayerJoined 수신까지 대기해 SpawnIndex/MapId 를 확보한 뒤 계산.
        /// Main:    원점(PVE 배치는 추후 씬 SpawnPoint로 개선).
        /// </summary>
        private async UniTask<(Vector3 pos, Quaternion rot)> ResolveLocalSpawnPoseAsync(CancellationToken ct)
        {
            if (_socketSession.State != SocketSessionState.Joined)
                return (Vector3.zero, Quaternion.identity);

            if (!await WaitForSelfSnapshotAsync(ct)
                || !_packetState.TryGetPlayer(_authSession.UserId, out var self))
            {
                Debug.LogWarning("[CharacterSpawner] self 스냅샷 대기 타임아웃 — 원점 스폰으로 폴백");
                return (Vector3.zero, Quaternion.identity);
            }

            var layout = _spawnLayouts.Get(_packetState.MapId);
            var sp = SpawnResolver.Resolve(layout, self.SpawnIndex);
            return (new Vector3(sp.X, sp.Y, sp.Z), Quaternion.Euler(0f, sp.RotY, 0f));
        }

        /// <summary>self S_PlayerJoined(SpawnIndex+MapId) 수신까지 대기. 타임아웃 시 false.</summary>
        private async UniTask<bool> WaitForSelfSnapshotAsync(CancellationToken ct)
        {
            const float timeoutSec = 5f;
            var deadline = Time.realtimeSinceStartup + timeoutSec;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!string.IsNullOrEmpty(_packetState.MapId)
                    && _packetState.TryGetPlayer(_authSession.UserId, out _))
                    return true;
                await UniTask.Yield(ct);
            }
            return false;
        }

        private void AttachMoveSyncSender()
        {
            if (_localCharacterGo == null) return;
            var sender = _localCharacterGo.AddComponent<MoveSyncSender>();
            _container.Inject(sender);
            Debug.Log("[CharacterSpawner] MoveSyncSender 부착 완료 — C_Move 송신 활성화");
        }

        // ── 원격 캐릭터 ──────────────────────────────────

        private void SpawnInitialRemotes()
        {
            foreach (var snapshot in _packetState.GetAllPlayers())
            {
                if (snapshot.UserId == _authSession.UserId) continue;
                SpawnRemote(snapshot);
            }
        }

        private void SubscribeNetworkEvents()
        {
            _packetState.OnPlayerJoined += HandlePlayerJoined;
            _packetState.OnPlayerLeft   += HandlePlayerLeft;
        }

        private void HandlePlayerJoined(SocketPlayerSnapshot snapshot)
        {
            if (snapshot.UserId == _authSession.UserId) return;
            SpawnRemote(snapshot);
        }

        private void HandlePlayerLeft(long userId) => DespawnRemote(userId);

        private void SpawnRemote(SocketPlayerSnapshot snapshot)
        {
            if (_remotes.ContainsKey(snapshot.UserId)) return;

            var prefab = _prefabs.RemotePlayerPrefab;
            if (prefab == null)
            {
                Debug.LogError("[CharacterSpawner] RemotePlayerPrefab이 설정되지 않았습니다.");
                return;
            }

            var pos = new Vector3(snapshot.PosX, snapshot.PosY, snapshot.PosZ);
            var rot = Quaternion.Euler(0f, snapshot.RotY, 0f);
            var go  = UnityEngine.Object.Instantiate(prefab, pos, rot);

            var driver = go.GetComponent<RemoteDriver>();
            if (driver == null)
            {
                Debug.LogError("[CharacterSpawner] RemotePlayerPrefab에 RemoteDriver 컴포넌트가 없습니다.");
                UnityEngine.Object.Destroy(go);
                return;
            }

            driver.Initialize(snapshot.UserId, _packetState);
            _remotes[snapshot.UserId] = driver;

            Debug.Log($"[CharacterSpawner] 원격 캐릭터 스폰 — UserId={snapshot.UserId} Nickname={snapshot.Nickname}");
        }

        private void DespawnRemote(long userId)
        {
            if (!_remotes.TryGetValue(userId, out var driver)) return;
            _remotes.Remove(userId);
            driver.Dispose();
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            Debug.Log($"[CharacterSpawner] 원격 캐릭터 디스폰 — UserId={userId}");
        }

        // ── 정리 ────────────────────────────────────────

        public void Dispose()
        {
            _localPlayer.Clear();
            _packetState.OnPlayerJoined -= HandlePlayerJoined;
            _packetState.OnPlayerLeft   -= HandlePlayerLeft;

            foreach (var driver in _remotes.Values)
            {
                driver.Dispose();
                if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            }
            _remotes.Clear();
        }
    }
}
