using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Interfaces;
using GameServer.Grpc.User;
using UnityEngine;

namespace Game.System.Player
{
    /// <summary>
    /// <see cref="IPlayerPositionService"/> 구현 — proto(GameServer.Grpc.User)를 여기서 숨긴다.
    ///
    /// 위치는 **편의 기능**이라 실패를 삼킨다. 저장이 실패하면 다음 주기에 다시 보내면 되고,
    /// 조회가 실패하면 호출자가 기본 스폰으로 폴백한다 — 어느 쪽도 플레이를 막지 않는다.
    /// </summary>
    public sealed class PlayerPositionService : IPlayerPositionService
    {
        private readonly IUserGrpcService _user;

        public PlayerPositionService(IUserGrpcService user) => _user = user;

        public async UniTask SaveAsync(PlayerPosition position, CancellationToken ct = default)
        {
            try
            {
                await _user.SavePositionAsync(new SavePositionRequest
                {
                    Position = new Position
                    {
                        MapId = position.MapId,
                        X = position.X,
                        Y = position.Y,
                        Z = position.Z,
                        RotY = position.RotY,
                    },
                }, ct);
            }
            catch (OperationCanceledException)
            {
                // 정상 종료(씬 전환/앱 종료) — 조용히 끝낸다.
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerPosition] 저장 실패(다음 주기에 재시도): {e.Message}");
            }
        }

        public async UniTask<PlayerPosition?> GetLastAsync(CancellationToken ct = default)
        {
            try
            {
                var res = await _user.GetLastPositionAsync(new GetLastPositionRequest(), ct);
                if (res?.Result is not { Success: true } || !res.HasPosition || res.Position == null)
                    return null;

                var p = res.Position;
                return new PlayerPosition(p.MapId, p.X, p.Y, p.Z, p.RotY);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerPosition] 조회 실패(기본 스폰으로 폴백): {e.Message}");
                return null;
            }
        }
    }
}
