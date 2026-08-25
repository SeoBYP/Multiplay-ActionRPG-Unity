using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.System.Player
{
    /// <summary>Main 마지막 위치(도메인 DTO). proto 타입은 Network 레이어에 가둔다.</summary>
    public readonly struct PlayerPosition
    {
        public readonly string MapId;
        public readonly float X, Y, Z, RotY;

        public PlayerPosition(string mapId, float x, float y, float z, float rotY)
        {
            MapId = mapId; X = x; Y = y; Z = z; RotY = rotY;
        }
    }

    /// <summary>
    /// Main 위치 지속화(B7). 주기 보고 → 재접속 시 그 자리에서 시작.
    ///
    /// 서버는 맵 경계만 검증하고 밖이면 저작 스폰으로 스냅한다. 클라는 복원 좌표를 **지면에 스냅**해서 쓴다
    /// (내비메시는 클라 자산이라 서버가 볼 수 없다) — 각 계층이 아는 것만 검증한다.
    /// </summary>
    public interface IPlayerPositionService
    {
        /// <summary>위치 보고. 실패해도 게임플레이를 막지 않는다(편의 기능).</summary>
        UniTask SaveAsync(PlayerPosition position, CancellationToken ct = default);

        /// <summary>마지막 위치. 없으면 null → 호출자는 기본 스폰으로 폴백한다.</summary>
        UniTask<PlayerPosition?> GetLastAsync(CancellationToken ct = default);
    }
}
