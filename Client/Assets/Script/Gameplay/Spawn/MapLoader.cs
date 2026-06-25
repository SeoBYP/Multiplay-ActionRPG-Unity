using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer.Unity;

namespace Game.Gameplay.Spawn
{
    /// <summary>
    /// Dungeon 진입 시 서버가 지정한 mapId 의 MapDefinition.visualPrefab(배경 모델)을 인스턴스화한다.
    ///
    /// 스폰 좌표(JSON, 서버 공용)와 달리 비주얼 프리팹은 클라 전용이라 SO(MapDefinition)에서 직접 읽는다.
    /// MapDefinition 은 Assets/GameData/Maps/{mapId}.asset 에 위치, **Addressables**(address=에셋 경로)로 로드.
    /// (Resources 폐기 — 맵이 늘어도 빌드에 전부 포함되지 않도록 on-demand 번들.)
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

            var address = $"Assets/GameData/Maps/{mapId}.asset";
            var handle = Addressables.LoadAssetAsync<MapDefinition>(address);
            MapDefinition def;
            try
            {
                def = await handle.Task.AsUniTask().AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MapLoader] 맵 로드 실패 address={address}: {e.Message}");
                if (handle.IsValid()) Addressables.Release(handle);
                return;
            }

            // 키 미등록/주소 불일치 시 일부 버전은 예외 대신 Result=null 로 완료.
            if (def == null)
            {
                Debug.LogError($"[MapLoader] MapDefinition 로드 결과 없음 — Addressable 주소 미등록? address={address}");
                if (handle.IsValid()) Addressables.Release(handle);
                return;
            }
            if (def.visualPrefab == null)
            {
                Debug.LogWarning($"[MapLoader] '{mapId}' 의 visualPrefab 이 미할당 — 맵 비주얼 없음.");
                Addressables.Release(handle);
                return;
            }

            _mapInstance = UnityEngine.Object.Instantiate(def.visualPrefab);
            Debug.Log($"[MapLoader] 맵 비주얼 스폰 — mapId={mapId} prefab={def.visualPrefab.name}");
            // 인스턴스(클론)는 독립 → SO 핸들 해제(visualPrefab 원본 언로드, 클론 유지).
            Addressables.Release(handle);
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
