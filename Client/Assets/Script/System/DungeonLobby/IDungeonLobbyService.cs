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

        /// <summary>
        /// 활성 방 목록을 한 페이지 조회한다(9.6). <paramref name="limit"/> 상한·정렬(최신 먼저)은 서버가 강제한다.
        /// </summary>
        /// <returns>결과 · 해당 페이지 방 목록 · <b>전체</b> 활성 방 수(페이저용).</returns>
        UniTask<(DungeonLobbyResult, IReadOnlyList<RoomInfo>, int TotalCount)> GetRoomsAsync(
            int offset = 0, int limit = DungeonLobbyPaging.DefaultPageSize, CancellationToken ct = default);
        UniTask<DungeonLobbyResult> CreateRoomAsync(string roomName, int maxPlayers, string mapId = "", CancellationToken ct = default);
        UniTask<DungeonLobbyResult> JoinRoomAsync(long roomId, CancellationToken ct = default);
        UniTask<DungeonLobbyResult> LeaveRoomAsync(CancellationToken ct = default);
        UniTask<DungeonLobbyResult> StartGameAsync(CancellationToken ct = default);

        /// <summary>
        /// 대기실에서 자신의 준비 상태를 토글한다. 호스트는 준비 개념이 없어 서버가 거부한다.
        /// 성공하면 방 전원에게 <see cref="OnRoomUpdated"/> 로 갱신된 RoomInfo 가 흘러온다.
        /// </summary>
        UniTask<DungeonLobbyResult> SetReadyAsync(bool isReady, CancellationToken ct = default);

        /// <summary>
        /// 이미 서버에 입장된 방을 세션/구독만 복원한다 (JoinRoom API 호출 없음).
        /// 재로그인 시 AlreadyInRoom 오류 없이 방 상태를 되돌린다.
        /// </summary>
        UniTask<DungeonLobbyResult> RestoreRoomAsync(long roomId, CancellationToken ct = default);
    }
}
