using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using UnityEngine;
using VContainer.Unity;

namespace Game.Gameplay.Spawn
{
    /// <summary>
    /// Dungeon 진입 시 서버가 지정한 mapId 의 MapDefinition.visualPrefab(배경 모델)을 인스턴스화한다.
    ///
    /// 스폰 좌표(JSON, 서버 공용)와 달리 비주얼 프리팹은 클라 전용이라 SO(MapDefinition)에서 직접 읽는다.
    /// MapDefinition 은 Resources/Maps/{mapId}.asset 에 위치(Export 툴이 같은 폴더에 생성/갱신).
    /// </summary>
    public sealed class MapLoader : IAsyncStartable
    {
        private readonly ISocketSession    _socketSession;
        private readonly ISocketPacketState _packetState;

        private GameObject _mapInstance;

        public MapLoader(ISocketSession socketSession, ISocketPacketState packetState)
        {
            _socketSession = socketSession;
            _packetState   = packetState;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            if (_socketSession.State != SocketSessionState.Joined)
                return; // Main 씬 등 — 던전 맵 비주얼 로드 안 함

            var mapId = await WaitForMapIdAsync(ct);
            if (string.IsNullOrEmpty(mapId))
            {
                Debug.LogWarning("[MapLoader] mapId 대기 타임아웃 — 맵 비주얼 로드 생략");
                return;
            }

            var def = Resources.Load<MapDefinition>($"Maps/{mapId}");
            if (def == null)
            {
                Debug.LogError($"[MapLoader] Resources/Maps/{mapId}.asset (MapDefinition) 를 찾지 못했습니다.");
                return;
            }
            if (def.visualPrefab == null)
            {
                Debug.LogWarning($"[MapLoader] '{mapId}' 의 visualPrefab 이 미할당 — 맵 비주얼 없음.");
                return;
            }

            _mapInstance = Object.Instantiate(def.visualPrefab);
            Debug.Log($"[MapLoader] 맵 비주얼 스폰 — mapId={mapId} prefab={def.visualPrefab.name}");
        }

        /// <summary>서버가 내려준 mapId(S_PlayerJoined) 수신까지 대기. 타임아웃 시 null.</summary>
        private async UniTask<string> WaitForMapIdAsync(CancellationToken ct)
        {
            const float timeoutSec = 5f;
            var deadline = Time.realtimeSinceStartup + timeoutSec;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!string.IsNullOrEmpty(_packetState.MapId)) return _packetState.MapId;
                await UniTask.Yield(ct);
            }
            return null;
        }
    }
}
