using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.DungeonLobby;
using Game.System.DungeonLobby;
using UnityEngine;

namespace Game.Presentation.DungeonLobby
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

        /// <summary>방 목록 한 페이지(9.6). <c>TotalCount</c> = 전체 활성 방 수(페이저용).</summary>
        public async UniTask<(bool IsSuccess, IReadOnlyList<RoomInfo> Rooms, int TotalCount, string Error)>
            GetRoomsAsync(int offset = 0, int limit = DungeonLobbyPaging.DefaultPageSize, CancellationToken ct = default)
        {
            Debug.Log($"[LobbyRepository] GetRooms 요청 offset={offset} limit={limit}");
            var (result, rooms, total) = await _service.GetRoomsAsync(offset, limit, ct);
            Debug.Log($"[LobbyRepository] GetRooms 응답: {result} ({rooms?.Count ?? 0}개 / 전체 {total})");
            return result == DungeonLobbyResult.Success
                ? (true, rooms, total, null)
                : (false, null, 0, result.ToString());
        }

        public async UniTask<(bool IsSuccess, RoomInfo Room, string Error)>
            CreateRoomAsync(string name, int maxPlayers, string mapId = "", CancellationToken ct = default)
        {
            Debug.Log($"[LobbyRepository] CreateRoom 요청 name={name} maxPlayers={maxPlayers} mapId={mapId}");
            var result = await _service.CreateRoomAsync(name, maxPlayers, mapId, ct);
            Debug.Log($"[LobbyRepository] CreateRoom 응답: {result}");
            return result == DungeonLobbyResult.Success
                ? (true, _service.CurrentRoom, null)
                : (false, null, result.ToString());
        }

        public async UniTask<(bool IsSuccess, RoomInfo Room, string Error)>
            JoinRoomAsync(long roomId, CancellationToken ct = default)
        {
            Debug.Log($"[LobbyRepository] JoinRoom 요청 roomId={roomId}");
            var result = await _service.JoinRoomAsync(roomId, ct);
            Debug.Log($"[LobbyRepository] JoinRoom 응답: {result}");
            return result == DungeonLobbyResult.Success
                ? (true, _service.CurrentRoom, null)
                : (false, null, result.ToString());
        }

        public async UniTask<(bool IsSuccess, string Error)>
            StartGameAsync(CancellationToken ct = default)
        {
            Debug.Log("[LobbyRepository] StartGame 요청");
            var result = await _service.StartGameAsync(ct);
            Debug.Log($"[LobbyRepository] StartGame 응답: {result}");
            return result == DungeonLobbyResult.Success
                ? (true, null)
                : (false, result.ToString());
        }

        public async UniTask<(bool IsSuccess, string Error)>
            SetReadyAsync(bool isReady, CancellationToken ct = default)
        {
            Debug.Log($"[LobbyRepository] SetReady 요청 isReady={isReady}");
            var result = await _service.SetReadyAsync(isReady, ct);
            Debug.Log($"[LobbyRepository] SetReady 응답: {result}");
            return result == DungeonLobbyResult.Success
                ? (true, null)
                : (false, result.ToString());
        }

        public async UniTask<(bool IsSuccess, string Error)>
            LeaveRoomAsync(CancellationToken ct = default)
        {
            Debug.Log("[LobbyRepository] LeaveRoom 요청");
            var result = await _service.LeaveRoomAsync(ct);
            Debug.Log($"[LobbyRepository] LeaveRoom 응답: {result}");
            return result == DungeonLobbyResult.Success
                ? (true, null)
                : (false, result.ToString());
        }

        public async UniTask<(bool IsSuccess, RoomInfo Room, string Error)>
            RestoreRoomAsync(long roomId, CancellationToken ct = default)
        {
            Debug.Log($"[LobbyRepository] RestoreRoom 요청 roomId={roomId}");
            var result = await _service.RestoreRoomAsync(roomId, ct);
            Debug.Log($"[LobbyRepository] RestoreRoom 응답: {result}");
            return result == DungeonLobbyResult.Success
                ? (true, _service.CurrentRoom, null)
                : (false, null, result.ToString());
        }
    }
}
