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
        private readonly PartyAscRegistry       _partyRegistry;
        private readonly ActorRegistry          _actors;   // ActorId(=UserId) → RemoteDriver, 발동 Cue 라우팅용(몬스터와 공용)

        private GameObject _localCharacterGo;
        private readonly Dictionary<long, RemoteDriver> _remotes = new Dictionary<long, RemoteDriver>();

        public CharacterSpawner(
            ISocketSession          socketSession,
            ISocketPacketState      packetState,
            AuthSession             authSession,
            IObjectResolver         container,
            CharacterPrefabSettings prefabs,
            LocalPlayerContext      localPlayer,
            SpawnLayoutProvider     spawnLayouts,
            PartyAscRegistry        partyRegistry,
            ActorRegistry           actors)
        {
            _socketSession = socketSession;
            _packetState   = packetState;
            _authSession   = authSession;
            _container     = container;
            _prefabs       = prefabs;
            _localPlayer   = localPlayer;
            _spawnLayouts  = spawnLayouts;
            _partyRegistry = partyRegistry;
            _actors        = actors;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            Debug.Log($"[CharacterSpawner] StartAsync — SocketState={_socketSession.State}");

            await SpawnLocalAsync(ct);

            if (_socketSession.State != SocketSessionState.Joined)
            {
                AttachLocalCombat(); // Main(싱글): 로컬 권위 전투(클라 히트 판정). 던전은 CombatSyncSender(서버 권위).
                Debug.Log("[CharacterSpawner] Main 씬 모드 — 네트워크 동기화 없음");
                return;
            }

            AttachMoveSyncSender();
            AttachCombatSyncSender();
            AttachDodgeSyncSender();
            AttachReviveInteractor();
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
            {
                _localPlayer.Set(asc);
                _partyRegistry.Register(_authSession.UserId, asc); // 파티 HP HUD 용 로컬 등록
            }
            else
                Debug.LogError("[CharacterSpawner] 로컬 프리팹에 AbilitySystemComponent가 없습니다.");

            // 서버 권위 레벨 MaxHealth 를 ASC 에 정렬(prefab 100 ↔ 서버 레벨값 desync 해소). Main·던전 공통.
            // 홀더가 스코프에 있으면 연결(Main/던전), 없으면(미등록 테스트 하네스) 생략 — 스폰은 정상 진행.
            var statApplier = go.AddComponent<PlayerStatApplier>();
            if (_container.TryResolve(typeof(Game.System.Progression.PlayerProgressionHolder), out var holderObj))
                statApplier.Bind((Game.System.Progression.PlayerProgressionHolder)holderObj);

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

        private void AttachCombatSyncSender()
        {
            if (_localCharacterGo == null) return;
            var sender = _localCharacterGo.AddComponent<CombatSyncSender>();
            _container.Inject(sender);
            Debug.Log("[CharacterSpawner] CombatSyncSender 부착 완료 — C_Attack 송신 활성화");
        }

        private void AttachDodgeSyncSender()
        {
            if (_localCharacterGo == null) return;
            var sender = _localCharacterGo.AddComponent<DodgeSyncSender>();
            _container.Inject(sender);
            Debug.Log("[CharacterSpawner] DodgeSyncSender 부착 완료 — C_Dodge 송신 활성화");
        }

        /// <summary>던전(서버 권위): 로컬 캐릭터에 ReviveInteractor 부착 → 다운 아군 부활 시전(C_Revive).</summary>
        private void AttachReviveInteractor()
        {
            if (_localCharacterGo == null) return;
            var interactor = _localCharacterGo.AddComponent<ReviveInteractor>();
            // 입력은 ReviveInteractor 가 GetComponent(같은 GO) — 세션만 주입.
            interactor.Configure(_socketSession);
            Debug.Log("[CharacterSpawner] ReviveInteractor 부착 완료 — Co-op 부활 시전 활성화");
        }

        /// <summary>Main(비네트워크): 로컬 캐릭터에 LocalCombat 부착 → 클라 권위 히트 판정(서버 미관여).</summary>
        private void AttachLocalCombat()
        {
            if (_localCharacterGo == null) return;
            var combat = _localCharacterGo.AddComponent<LocalCombat>();
            _container.Inject(combat); // PlayerProgressionHolder(스탯) 주입 — 동적 부착이라 수동 주입 필요
            Debug.Log("[CharacterSpawner] LocalCombat 부착 완료 — Main 로컬 전투 활성화");
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
            _packetState.OnPlayerJoined  += HandlePlayerJoined;
            _packetState.OnPlayerLeft    += HandlePlayerLeft;
            _packetState.OnPlayerDead    += HandlePlayerDead;
            _packetState.OnPlayerRevived += HandlePlayerRevived;
        }

        private void HandlePlayerJoined(SocketPlayerSnapshot snapshot)
        {
            if (snapshot.UserId == _authSession.UserId) return;
            SpawnRemote(snapshot);
        }

        private void HandlePlayerLeft(long userId) => DespawnRemote(userId);

        /// <summary>
        /// 한 플레이어의 다운(S_PlayerDead) 처리.
        /// 로컬: 자기 캐릭터는 destroy 하지 않는다 — State.Dead 게이트(2.5.1 ⓔ-1)로 입력이 정지됐고,
        ///       자기 GO 를 지우면 카메라 타깃/HUD 가 깨진다. 로그만(다운 포즈 도입 시 교체).
        /// 원격(2.5.2 변경): **Destroy 하지 않고 다운 보존** — DownedAllyMarker 를 부착해 부활 대상으로 남긴다.
        ///       (기존엔 DespawnRemote 였으나 부활하려면 다운 아군이 상호작용 대상으로 살아 있어야 함.)
        /// </summary>
        private void HandlePlayerDead(long userId)
        {
            if (userId == _authSession.UserId)
            {
                Debug.Log($"[CharacterSpawner] 로컬 플레이어 다운 — UserId={userId} (입력 게이트 처리됨, 캐릭터 유지)");
                return;
            }

            if (!_remotes.TryGetValue(userId, out var driver) || driver == null)
            {
                Debug.LogWarning($"[CharacterSpawner] 다운된 원격 캐릭터를 찾지 못함 — UserId={userId}");
                return;
            }

            var go = driver.gameObject;
            if (go.GetComponent<DownedAllyMarker>() == null)
                go.AddComponent<DownedAllyMarker>().Setup(userId);
            Debug.Log($"[CharacterSpawner] 원격 플레이어 다운 — UserId={userId} (다운 보존, 부활 대상)");
        }

        /// <summary>
        /// Co-op 부활(2.5.2) 확정(S_PlayerRevived) 처리.
        /// 로컬: PlayerCharacterAgent.ReviveInPlace(hp) — State.Dead 해제 + 서버 권위 HP, 제자리(텔레포트 X).
        /// 원격: DownedAllyMarker 제거 → 정상 캐릭터로 복귀(부활 대상에서 빠짐).
        /// </summary>
        private void HandlePlayerRevived(long userId, int hp)
        {
            if (userId == _authSession.UserId)
            {
                var agent = _localCharacterGo != null ? _localCharacterGo.GetComponent<PlayerCharacterAgent>() : null;
                agent?.ReviveInPlace(hp);
                Debug.Log($"[CharacterSpawner] 로컬 플레이어 부활 — UserId={userId} Hp={hp}");
                return;
            }

            if (_remotes.TryGetValue(userId, out var driver) && driver != null)
            {
                var marker = driver.gameObject.GetComponent<DownedAllyMarker>();
                if (marker != null) UnityEngine.Object.Destroy(marker);
            }
            Debug.Log($"[CharacterSpawner] 원격 플레이어 부활 — UserId={userId} (다운 보존 해제)");
        }

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
            // AC: ActorId(=UserId, 양수)로 레지스트리 등록 → S_AbilityActivated 발동 신호가 이 원격 플레이어의 스윙 Cue 로 라우팅됨(몬스터와 동일 파이프).
            _actors.Register(ActorIds.FromPlayer(snapshot.UserId), driver);

            // 파티 HP HUD 용 원격 ASC 등록(S_ApplyEffect 를 TargetId 로 라우팅해 HP 추적).
            // 서버 권위 HP 기준선(S_PlayerJoined Hp/MaxHp)으로 정렬 — 이후 델타가 정확한 기준선 위에 얹힌다.
            // MaxHp==0 이면 서버 미전송(레거시/테스트) → prefab 기본값(100) 유지.
            var remoteAsc = go.GetComponent<AbilitySystemComponent>();
            if (remoteAsc != null)
            {
                if (snapshot.MaxHp > 0)
                {
                    var hpAttr = remoteAsc.GetAttribute(EGameplayAttribute.Health);
                    if (hpAttr != null)
                    {
                        hpAttr.SetMax(snapshot.MaxHp);
                        hpAttr.SetCurrent(snapshot.Hp);
                    }
                }
                _partyRegistry.Register(snapshot.UserId, remoteAsc);
            }

            Debug.Log($"[CharacterSpawner] 원격 캐릭터 스폰 — UserId={snapshot.UserId} Nickname={snapshot.Nickname}");
        }

        private void DespawnRemote(long userId)
        {
            if (!_remotes.TryGetValue(userId, out var driver)) return;
            _remotes.Remove(userId);
            _partyRegistry.Unregister(userId);
            _actors.Unregister(ActorIds.FromPlayer(userId));
            driver.Dispose();
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            Debug.Log($"[CharacterSpawner] 원격 캐릭터 디스폰 — UserId={userId}");
        }

        // ── 정리 ────────────────────────────────────────

        public void Dispose()
        {
            _localPlayer.Clear();
            _partyRegistry.Clear();
            _packetState.OnPlayerJoined  -= HandlePlayerJoined;
            _packetState.OnPlayerLeft    -= HandlePlayerLeft;
            _packetState.OnPlayerDead    -= HandlePlayerDead;
            _packetState.OnPlayerRevived -= HandlePlayerRevived;

            foreach (var kv in _remotes)
            {
                _actors.Unregister(ActorIds.FromPlayer(kv.Key));
                kv.Value.Dispose();
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value.gameObject);
            }
            _remotes.Clear();
        }
    }
}
