using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameServer.Grpc.DungeonLobby;

namespace Game.System.DungeonLobby
{
    public interface IDungeonLobbyService
    {
        bool IsInRoom { get; }
        RoomInfo? CurrentRoom { get; }

        /// <summary>방 정보가 바뀔 때마다 발생 (입장/퇴장/갱신).</summary>
        event Action<RoomInfo> OnRoomUpdated;

        /// <summary>호스트가 게임 시작을 눌렀을 때 발생.</summary>
        event Action<RoomInfo> OnGameStarting;

        /// <summary>
        /// SocketServer가 준비 완료됐을 때 발생.
        /// (ip, port, roomId) — TCP 접속 + 방 입장에 바로 사용한다.
        /// </summary>
        event Action<string, int, long> OnGameSessionReady;

        UniTask<(DungeonLobbyResult, IReadOnlyList<RoomInfo>)> GetRoomsAsync(CancellationToken ct = default);
        UniTask<DungeonLobbyResult> CreateRoomAsync(string roomName, int maxPlayers, CancellationToken ct = default);
        UniTask<DungeonLobbyResult> JoinRoomAsync(long roomId, CancellationToken ct = default);
        UniTask<DungeonLobbyResult> LeaveRoomAsync(CancellationToken ct = default);
        UniTask<DungeonLobbyResult> StartGameAsync(CancellationToken ct = default);

        /// <summary>
        /// 이미 서버에 입장된 방을 세션/구독만 복원한다 (JoinRoom API 호출 없음).
        /// 재로그인 시 AlreadyInRoom 오류 없이 방 상태를 되돌린다.
        /// </summary>
        UniTask<DungeonLobbyResult> RestoreRoomAsync(long roomId, CancellationToken ct = default);
    }
}
