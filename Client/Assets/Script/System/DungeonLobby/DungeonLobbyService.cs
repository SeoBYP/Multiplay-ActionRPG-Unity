using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Interfaces;
using GameServer.Grpc.DungeonLobby;
using UnityEngine;

namespace Game.System.DungeonLobby
{
    /// <summary>
    /// 던전 로비 오케스트레이션 서비스.
    /// gRPC 호출 결과를 DungeonLobbyResult로 변환하고,
    /// SubscribeRoom 스트림을 통해 실시간 방 이벤트를 이벤트로 노출한다.
    /// </summary>
    public class DungeonLobbyService : IDungeonLobbyService, IDisposable
    {
        // ── 서버 에러코드 (ErrorCodes.cs 기준) ────────────────────────────
        private const int ErrRoomNotFound      = 2000;
        private const int ErrRoomFull          = 2001;
        private const int ErrAlreadyInRoom     = 2002;
        private const int ErrNotInRoom         = 2003;
        private const int ErrNotRoomHost       = 2004;
        private const int ErrRoomNotWaiting    = 2005;
        private const int ErrRoomAlreadyPlaying= 2006;

        private readonly IDungeonLobbyGrpcService _grpc;
        private readonly DungeonLobbySession _session;

        /// SubscribeRoom 스트림 전용 취소 토큰.
        /// CreateRoom / JoinRoom 성공 시 새로 생성, LeaveRoom / GameSessionEvent 수신 시 취소.
        private CancellationTokenSource? _subscriptionCts;

        public bool IsInRoom     => _session.IsInRoom;
        public RoomInfo? CurrentRoom => _session.CurrentRoom;

        public event Action<RoomInfo>?  OnRoomUpdated;
        public event Action<RoomInfo>?  OnGameStarting;
        public event Action<string, int, long>? OnGameSessionReady;

        public DungeonLobbyService(IDungeonLobbyGrpcService grpc, DungeonLobbySession session)
        {
            _grpc    = grpc;
            _session = session;
        }

        // ── Public API ────────────────────────────────────────────────────

        public async UniTask<(DungeonLobbyResult, IReadOnlyList<RoomInfo>, int TotalCount)> GetRoomsAsync(
            int offset = 0, int limit = DungeonLobbyPaging.DefaultPageSize, CancellationToken ct = default)
        {
            var res = await _grpc.GetRoomsAsync(new GetRoomsRequest { RoomCount = limit, Offset = offset }, ct);
            if (!res.Result.Success)
                return (MapError(res.Result.ErrorCode), Array.Empty<RoomInfo>(), 0);

            Debug.Log($"[DungeonLobby] 방 목록 수신: {res.RoomInfos.Count}개 (offset {offset}, 전체 {res.TotalCount})");
            foreach (var room in res.RoomInfos)
                Debug.Log($"  └ [{room.RoomId}] {room.RoomName} ({room.CurrentPlayers.Count}/{room.MaxPlayers}) {room.Status}");

            return (DungeonLobbyResult.Success, res.RoomInfos, res.TotalCount);
        }

        public async UniTask<DungeonLobbyResult> CreateRoomAsync(string roomName, int maxPlayers, string mapId = "", CancellationToken ct = default)
        {
            var res = await _grpc.CreateRoomAsync(new CreateRoomRequest
            {
                RoomName   = roomName,
                MaxPlayers = maxPlayers,
                MapId      = mapId ?? ""   // 비우면 서버가 기본 맵(dungeon_01)으로 영속
            }, ct);

            if (!res.Result.Success)
                return MapError(res.Result.ErrorCode);

            _session.SetRoom(res.RoomInfo);
            StartSubscription(res.RoomInfo.RoomId);
            return DungeonLobbyResult.Success;
        }

        public async UniTask<DungeonLobbyResult> JoinRoomAsync(long roomId, CancellationToken ct = default)
        {
            var res = await _grpc.JoinRoomAsync(new JoinRoomRequest { RoomId = roomId }, ct);

            if (!res.Result.Success)
                return MapError(res.Result.ErrorCode);

            _session.SetRoom(res.RoomInfo);
            StartSubscription(roomId);
            return DungeonLobbyResult.Success;
        }

        public async UniTask<DungeonLobbyResult> LeaveRoomAsync(CancellationToken ct = default)
        {
            if (!_session.IsInRoom)
                return DungeonLobbyResult.NotInRoom;

            var roomId = _session.CurrentRoom!.RoomId;
            var res    = await _grpc.LeaveRoomAsync(new LeaveRoomRequest { RoomId = roomId }, ct);

            // 스트림은 서버 응답 여부와 무관하게 먼저 끊는다.
            StopSubscription();
            _session.ClearRoom();

            return res.Result.Success ? DungeonLobbyResult.Success : MapError(res.Result.ErrorCode);
        }

        public async UniTask<DungeonLobbyResult> RestoreRoomAsync(long roomId, CancellationToken ct = default)
        {
            // 이미 세션에 같은 방이 있으면 중복 복원 방지
            if (_session.IsInRoom && _session.CurrentRoom?.RoomId == roomId)
                return DungeonLobbyResult.Success;

            // GetRooms(전체 목록) 대신 GetRoom(단일 조회)로 최소 요청
            var res = await _grpc.GetRoomAsync(new GetRoomRequest { RoomId = roomId }, ct);
            if (!res.Result.Success)
                return MapError(res.Result.ErrorCode);

            if (res.RoomInfo == null)
            {
                Debug.LogWarning($"[DungeonLobbyService] RestoreRoom: roomId={roomId} RoomInfo가 null");
                return DungeonLobbyResult.RoomNotFound;
            }

            _session.SetRoom(res.RoomInfo);
            StartSubscription(roomId);
            Debug.Log($"[DungeonLobbyService] RestoreRoom 완료: roomId={roomId} name={res.RoomInfo.RoomName}");
            return DungeonLobbyResult.Success;
        }

        public async UniTask<DungeonLobbyResult> StartGameAsync(CancellationToken ct = default)
        {
            if (!_session.IsInRoom)
                return DungeonLobbyResult.NotInRoom;

            var res = await _grpc.StartRoomAsync(new StartRoomRequest
            {
                RoomId = _session.CurrentRoom!.RoomId
            }, ct);

            return res.Result.Success ? DungeonLobbyResult.Success : MapError(res.Result.ErrorCode);
        }

        // ── Subscription 관리 ─────────────────────────────────────────────

        /// <summary>새 스트림 구독을 시작한다. 이전 구독이 있으면 먼저 취소한다.</summary>
        private void StartSubscription(long roomId)
        {
            Debug.Log($"[DungeonLobbyService] StartSubscription roomId={roomId}");
            StopSubscription();
            _subscriptionCts = new CancellationTokenSource();
            SubscribeLoopAsync(roomId, _subscriptionCts.Token).Forget();
        }

        /// <summary>현재 스트림 구독을 취소한다.</summary>
        private void StopSubscription()
        {
            if (_subscriptionCts == null) return;

            Debug.Log($"[DungeonLobbyService] StopSubscription 호출됨\n{Environment.StackTrace}");
            _subscriptionCts.Cancel();
            _subscriptionCts.Dispose();
            _subscriptionCts = null;
        }

        private async UniTaskVoid SubscribeLoopAsync(long roomId, CancellationToken ct)
        {
            try
            {
                await _grpc.SubscribeRoomAsync(
                    new SubscribeRoomRequest { RoomId = roomId },
                    HandleSubscribeResponse,
                    ct);
            }
            catch (OperationCanceledException)
            {
                // 정상 취소 (LeaveRoom / GameSessionReady 수신 후)
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled)
            {
                // gRPC 레이어의 정상 취소
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DungeonLobbyService] SubscribeRoom error: {ex.Message}");
            }
        }

        private void HandleSubscribeResponse(SubscribeRoomResponse response)
        {
            Debug.Log($"[DungeonLobbyService] SubscribeRoom 이벤트 수신: {response.PayloadCase}");
            switch (response.PayloadCase)
            {
                case SubscribeRoomResponse.PayloadOneofCase.UpdateEvent:
                    _session.SetRoom(response.UpdateEvent.RoomInfo);
                    OnRoomUpdated?.Invoke(response.UpdateEvent.RoomInfo);
                    break;

                case SubscribeRoomResponse.PayloadOneofCase.StartEvent:
                    _session.SetRoom(response.StartEvent.RoomInfo);
                    OnGameStarting?.Invoke(response.StartEvent.RoomInfo);
                    break;

                case SubscribeRoomResponse.PayloadOneofCase.GameSessionEvent:
                    // SocketServer 준비 완료 — roomId를 먼저 캡처한 뒤 세션을 비우고 스트림을 끊는다.
                    var ip     = response.GameSessionEvent.Ip;
                    var port   = response.GameSessionEvent.Port;
                    var roomId = _session.CurrentRoom!.RoomId;
                    StopSubscription();
                    _session.ClearRoom();
                    OnGameSessionReady?.Invoke(ip, port, roomId);
                    break;
            }
        }

        // ── 에러 코드 변환 ─────────────────────────────────────────────────

        private static DungeonLobbyResult MapError(int errorCode) => errorCode switch
        {
            ErrRoomNotFound       => DungeonLobbyResult.RoomNotFound,
            ErrRoomFull           => DungeonLobbyResult.RoomFull,
            ErrAlreadyInRoom      => DungeonLobbyResult.AlreadyInRoom,
            ErrNotInRoom          => DungeonLobbyResult.NotInRoom,
            ErrNotRoomHost        => DungeonLobbyResult.NotHost,
            ErrRoomNotWaiting     => DungeonLobbyResult.RoomNotWaiting,
            ErrRoomAlreadyPlaying => DungeonLobbyResult.RoomAlreadyPlaying,
            _                     => DungeonLobbyResult.Failed,
        };

        // ── IDisposable ───────────────────────────────────────────────────

        public void Dispose()
        {
            Debug.Log("[DungeonLobbyService] Dispose 호출됨");
            StopSubscription();
        }
    }
}
