using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.DungeonLobby;
using Game.System.DungeonLobby;

namespace Game.OutGame.DungeonLobby
{
    /// <summary>
    /// IDungeonLobbyService를 LobbyModel이 직접 의존하지 않도록 감싸는 레이어.
    /// 네트워크 결과를 (IsSuccess, Data, Error) 형태로 정규화한다.
    /// </summary>
    public sealed class LobbyRepository
    {
        private readonly IDungeonLobbyService _service;

        public LobbyRepository(IDungeonLobbyService service)
        {
            _service = service;
        }

        public async UniTask<(bool IsSuccess, IReadOnlyList<RoomInfo> Rooms, string Error)>
            GetRoomsAsync(CancellationToken ct = default)
        {
            var (result, rooms) = await _service.GetRoomsAsync(ct);
            return result == DungeonLobbyResult.Success
                ? (true, rooms, null)
                : (false, null, result.ToString());
        }

        public async UniTask<(bool IsSuccess, RoomInfo Room, string Error)>
            CreateRoomAsync(string name, int maxPlayers, CancellationToken ct = default)
        {
            var result = await _service.CreateRoomAsync(name, maxPlayers, ct);
            return result == DungeonLobbyResult.Success
                ? (true, _service.CurrentRoom, null)
                : (false, null, result.ToString());
        }

        public async UniTask<(bool IsSuccess, RoomInfo Room, string Error)>
            JoinRoomAsync(long roomId, CancellationToken ct = default)
        {
            var result = await _service.JoinRoomAsync(roomId, ct);
            return result == DungeonLobbyResult.Success
                ? (true, _service.CurrentRoom, null)
                : (false, null, result.ToString());
        }
    }
}
