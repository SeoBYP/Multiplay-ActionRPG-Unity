using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.System.Player;
using UnityEngine;
using VContainer.Unity;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// Main 위치 주기 보고(B7). 재접속 시 이 좌표에서 시작한다.
    ///
    /// **고정 주기가 아니라 "움직였을 때만"** 보낸다 — 가만히 서 있는 유저가 트래픽을 만들지 않게.
    /// 두 조건을 모두 만족해야 전송한다: 마지막 전송 이후 <see cref="MinIntervalSeconds"/> 경과 &amp;&amp;
    /// <see cref="MinMoveDistance"/> 이상 이동.
    ///
    /// 던전(소켓 Joined)에서는 동작하지 않는다 — 그쪽 위치는 서버 권위라 클라가 보고할 것이 없다.
    /// </summary>
    public sealed class MainPositionReporter : IAsyncStartable, IDisposable
    {
        private const float MinIntervalSeconds = 5f;
        private const float MinMoveDistance = 2f;

        private readonly IPlayerPositionService _positions;
        private readonly LocalPlayerContext _localPlayer;
        private readonly ISocketSession _socketSession;
        private readonly string _mapId;
        private readonly CancellationTokenSource _cts = new();

        public MainPositionReporter(
            IPlayerPositionService positions,
            LocalPlayerContext localPlayer,
            ISocketSession socketSession,
            MainMonsterSettings settings)
        {
            _positions = positions;
            _localPlayer = localPlayer;
            _socketSession = socketSession;
            _mapId = settings.MapId;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            if (_socketSession.State == SocketSessionState.Joined)
                return; // 던전 — 위치는 서버 권위다.

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
            var token = linked.Token;

            Vector3? lastSent = null;

            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(MinIntervalSeconds), cancellationToken: token)
                    .SuppressCancellationThrow();
                if (token.IsCancellationRequested) return;

                // 로컬 플레이어 트랜스폼은 ASC(MonoBehaviour)를 통해 얻는다 — 스폰 전이면 null.
                var asc = _localPlayer.AbilitySystem;
                if (asc == null) continue;
                var t = asc.transform;

                var pos = t.position;
                if (lastSent.HasValue && Vector3.Distance(lastSent.Value, pos) < MinMoveDistance)
                    continue;              // 사실상 제자리 — 보내지 않는다

                await _positions.SaveAsync(
                    new PlayerPosition(_mapId, pos.x, pos.y, pos.z, t.eulerAngles.y), token);
                lastSent = pos;
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
