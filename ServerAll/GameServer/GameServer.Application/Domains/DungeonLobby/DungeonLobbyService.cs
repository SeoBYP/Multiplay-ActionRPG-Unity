using GameServer.Application.Common;
using GameServer.Application.Common.Interfaces;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.Outbox;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Domain.Entities.Outbox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;

namespace GameServer.Application.Domains.DungeonLobby;

public class DungeonLobbyService(
    IDungeonRoomRepository dungeonRoomRepository,
    IDungeonLobbySubscriptionService dungeonLobbySubscriptionService,
    IDungeonRoomPlayerRepository dungeonRoomPlayerRepository,
    IOutboxRepository outboxRepository,
    IUserSessionRepository userSessionRepository,
    IChatSubscriptionService chatSubscriptionService,
    IUserProfileRepository userProfileRepository,
    IProgressionService progressionService,
    IUserPositionService userPositionService,
    IRoomReadyStore roomReadyStore,
    IDistributedLock distributedLock,
    IOptions<DungeonRoomReaperOptions> reaperOptions,
    ILogger<DungeonLobbyService> logger) : IDungeonLobbyService
{
    /// <summary>방 단위 임계구역 키. 검사와 쓰기가 갈라진 구간(인원 확인 → 입장)을 감싼다. 동시 다중 방 입장은 UserId UNIQUE 제약이 막는다.</summary>
    private static string RoomLockKey(long roomId) => $"room:{roomId}";

    public async Task<Result<DungeonRoom>> CreateDungeonRoomAsync(string sessionId, string roomName, int maxPlayers, string mapId = "", CancellationToken ct = default)
    {
        try
        {
            // 서버 권위 검증: 비우면 기본 맵, 알 수 없는 맵이면 거부
            var effectiveMapId = string.IsNullOrEmpty(mapId) ? MapIds.Default : mapId;
            if (!SpawnLayoutTable.IsKnown(effectiveMapId))
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (userSession is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var existingRoomPlayer = await dungeonRoomPlayerRepository.GetByUserIdAsync(userSession.UserId, ct);
            if (existingRoomPlayer is not null)
                return Result<DungeonRoom>.Failure(ErrorCodes.AlreadyInRoom, ErrorMessages.AlreadyInRoom);

            var newRoom = await dungeonRoomRepository.CreateAsync(userSession.UserId, roomName, maxPlayers, effectiveMapId, ct);
            if (newRoom is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);

            await dungeonRoomPlayerRepository.CreateAsync(newRoom.RoomId, userSession.UserId, ct);
            return Result<DungeonRoom>.Success(newRoom);
        }
        catch (PlayerAlreadyInRoomException)
        {
            // check-then-act 가 경합에서 뚫렸을 때의 최종 방어선(UNIQUE 제약)
            return Result<DungeonRoom>.Failure(ErrorCodes.AlreadyInRoom, ErrorMessages.AlreadyInRoom);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create dungeon room");
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<ActiveRoomPage>> GetActiveDungeonRoomsAsync(
        int offset, int limit, CancellationToken ct = default)
    {
        try
        {
            // 정렬·건너뛰기·자르기는 DB 가 한다. 안정 정렬(RoomId 내림차순)이 페이징의 전제
            var page = await dungeonRoomRepository.GetActiveRoomsPageAsync(
                DungeonLobbyPaging.ClampOffset(offset),
                DungeonLobbyPaging.ClampLimit(limit),
                ct);

            return Result<ActiveRoomPage>.Success(new ActiveRoomPage(page.Rooms, (int)page.TotalCount))!;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get active dungeon rooms");
            return Result<ActiveRoomPage>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<DungeonRoom>> GetDungeonRoomAsync(long roomId, CancellationToken ct = default)
    {
        try
        {
            var room = await dungeonRoomRepository.GetByIdAsync(roomId, ct);
            if (room is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);

            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get dungeon room {RoomId}", roomId);
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<DungeonRoom>> UpdateRoomSettingsAsync(
        string sessionId,
        long roomId,
        string? roomName = null,
        int? maxPlayers = null,
        CancellationToken ct = default)
    {
        try
        {
            var room = await dungeonRoomRepository.GetByIdAsync(roomId, ct);
            if (room is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);

            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (userSession is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var players = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);

            try
            {
                room.UpdateRoomSettings(userSession.UserId, players.Count, roomName, maxPlayers);
            }
            catch (UnauthorizedAccessException)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.NotRoomHost, ErrorMessages.NotRoomHost);
            }
            catch (ArgumentException)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            }
            catch (InvalidOperationException)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.UpdateRoomFailed, ErrorMessages.UpdateRoomFailed);
            }

            var updated = await dungeonRoomRepository.UpdateAsync(room, ct);
            if (!updated)
                return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.UpdateRoomFailed);

            await dungeonLobbySubscriptionService.PublishAsync(roomId, ct);
            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update room settings for room {RoomId}", roomId);
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<DungeonRoom>> JoinRoomAsync(string sessionId, long roomId, CancellationToken ct = default)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (userSession is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            // 정원 검사와 입장 기록 사이에 다른 요청이 끼면 정원을 넘기므로 방 단위 임계구역으로 묶는다
            DungeonRoom room;
            await using (await distributedLock.AcquireAsync(RoomLockKey(roomId), ct))
            {
                var found = await dungeonRoomRepository.GetByIdAsync(roomId, ct);
                if (found is null)
                    return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);

                room = found;

                if (room.Status != RoomStatus.Waiting)
                    return Result<DungeonRoom>.Failure(ErrorCodes.JoinRoomFailed, ErrorMessages.RoomNotWaiting);

                var existingRoomPlayer = await dungeonRoomPlayerRepository.GetByUserIdAsync(userSession.UserId, ct);
                if (existingRoomPlayer is not null)
                    return Result<DungeonRoom>.Failure(ErrorCodes.AlreadyInRoom, ErrorMessages.AlreadyInRoom);

                var currentPlayers = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);
                if (currentPlayers.Count >= room.MaxPlayers)
                    return Result<DungeonRoom>.Failure(ErrorCodes.JoinRoomFailed, ErrorMessages.RoomFull);

                await dungeonRoomPlayerRepository.CreateAsync(roomId, userSession.UserId, ct);
            }

            // 이전 방의 잔재를 막으려 항상 미준비로 시작
            await roomReadyStore.SetReadyAsync(roomId, userSession.UserId, false, ct);
            await chatSubscriptionService.UpdateRoomSubscriptionAsync(sessionId, roomId, ct);

            await dungeonLobbySubscriptionService.PublishAsync(roomId, ct);
            return Result<DungeonRoom>.Success(room);
        }
        catch (PlayerAlreadyInRoomException)
        {
            return Result<DungeonRoom>.Failure(ErrorCodes.AlreadyInRoom, ErrorMessages.AlreadyInRoom);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to join room {RoomId}", roomId);
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<DungeonRoom>> SetReadyAsync(string sessionId, long roomId, bool isReady, CancellationToken ct = default)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (userSession is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var room = await dungeonRoomRepository.GetByIdAsync(roomId, ct);
            if (room is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);

            var roomPlayer = await dungeonRoomPlayerRepository.GetByUserIdAsync(userSession.UserId, ct);
            if (roomPlayer is null || roomPlayer.RoomId != roomId)
                return Result<DungeonRoom>.Failure(ErrorCodes.NotInRoom, ErrorMessages.NotInRoom);

            if (room.Status != RoomStatus.Waiting)
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotWaiting, ErrorMessages.RoomNotWaiting);

            // 호스트는 준비 개념이 없다 — 시작 버튼이 곧 의사표시
            if (room.IsHost(userSession.UserId))
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            await roomReadyStore.SetReadyAsync(roomId, userSession.UserId, isReady, ct);
            await dungeonLobbySubscriptionService.PublishAsync(roomId, ct);

            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to set ready state for room {RoomId}", roomId);
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<DungeonRoom>> LeaveRoomAsync(string sessionId, long roomId, CancellationToken ct = default)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (userSession is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var room = await dungeonRoomRepository.GetByIdAsync(roomId, ct);
            if (room is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);

            var roomPlayer = await dungeonRoomPlayerRepository.GetByUserIdAsync(userSession.UserId, ct);
            if (roomPlayer is null || roomPlayer.RoomId != roomId)
                return Result<DungeonRoom>.Failure(ErrorCodes.NotInRoom, ErrorMessages.NotInRoom);

            var players = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);
            var remainingPlayers = players
                .Where(player => player.UserId != userSession.UserId)
                .OrderBy(player => player.JoinedAt)
                .ToList();

            await dungeonRoomPlayerRepository.RemoveAsync(roomId, userSession.UserId, ct);
            await roomReadyStore.SetReadyAsync(roomId, userSession.UserId, false, ct);
            await chatSubscriptionService.UpdateRoomSubscriptionAsync(sessionId, 0, ct);

            if (remainingPlayers.Count == 0)
            {
                room.Close();
                await dungeonRoomPlayerRepository.RemoveByRoomIdAsync(roomId, ct);
                await roomReadyStore.ClearAsync(roomId, ct);

                var deleted = await dungeonRoomRepository.DeleteAsync(roomId, ct);
                if (!deleted)
                    return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.DeleteRoomFailed);
            }
            else
            {
                if (room.IsHost(userSession.UserId))
                    room.ChangeHost(remainingPlayers[0].UserId);

                var updated = await dungeonRoomRepository.UpdateAsync(room, ct);
                if (!updated)
                    return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.UpdateRoomFailed);

                await dungeonLobbySubscriptionService.PublishAsync(roomId, ct);
            }

            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to leave room {RoomId}", roomId);
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<DungeonRoom>> StartGameAsync(string sessionId, long roomId, string traceId, string mapId = "", CancellationToken ct = default)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (userSession is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var room = await dungeonRoomRepository.GetByIdAsync(roomId, ct);
            if (room is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);

            // 우선순위: 명시 mapId > 방에 영속된 MapId > 기본 맵
            var effectiveMapId = !string.IsNullOrEmpty(mapId) ? mapId
                : !string.IsNullOrEmpty(room.MapId) ? room.MapId
                : MapIds.Default;

            if (!room.IsHost(userSession.UserId))
                return Result<DungeonRoom>.Failure(ErrorCodes.NotRoomHost, ErrorMessages.NotRoomHost);

            var players = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);

            // 준비 게이트 — 호스트를 뺀 전원이 준비해야 한다(클라 버튼 비활성화는 UX일 뿐).
            // Waiting 일 때만: Starting/Playing 재시도 경로는 준비 목록이 이미 비워진 뒤다.
            if (room.Status == RoomStatus.Waiting)
            {
                var readyUserIds = await roomReadyStore.GetReadyUserIdsAsync(roomId, ct);
                var notReady = players
                    .Where(player => !room.IsHost(player.UserId) && !readyUserIds.Contains(player.UserId))
                    .Select(player => player.UserId)
                    .ToList();

                if (notReady.Count > 0)
                {
                    logger.LogInformation(
                        "StartGame rejected for room {RoomId} — {Count} player(s) not ready: {UserIds}",
                        roomId, notReady.Count, string.Join(",", notReady));
                    return Result<DungeonRoom>.Failure(ErrorCodes.NotAllPlayersReady, ErrorMessages.NotAllPlayersReady);
                }
            }

            var playerInfos = new List<PlayerInfo>();
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var profile = await userProfileRepository.GetByIdAsync(player.UserId, ct);
                // 합산 전투 스탯을 메시지에 실어 SocketServer 가 DB 없이 쓰게 한다
                var stats = await progressionService.GetStatsAsync(player.UserId, ct);
                playerInfos.Add(new PlayerInfo
                {
                    UserId = player.UserId,
                    Nickname = profile?.NickName ?? $"Player_{player.UserId}",
                    SpawnIndex = i,
                    MaxHealth = stats.MaxHealth,
                    MaxMana = stats.MaxMana,
                    AttackPower = stats.AttackPower,
                    Defense = stats.Defense,
                });
            }

            try
            {
                room.StartGame(userSession.UserId, players.Count);
            }
            catch (UnauthorizedAccessException)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.NotRoomHost, ErrorMessages.NotRoomHost);
            }
            catch (InvalidOperationException)
            {
                // 이미 Starting/Playing — 멱등 재처리
                if (room.Status == RoomStatus.Starting)
                {
                    // 이전 GameStartRequested 유실 대비 — 방 상태 변경 없이 재발행
                    var retryMessage = new GameStartRequestedMessage
                    {
                        RoomId = room.RoomId,
                        PlayerInfos = playerInfos,
                        TraceId = traceId,
                        MapId = effectiveMapId
                    };
                    var retryOutbox = OutboxMessage.Create(
                        OutboxTopics.GameStartRequested,
                        System.Text.Json.JsonSerializer.Serialize(retryMessage));
                    await outboxRepository.AddWithRoomUpdateAsync(room, retryOutbox, ct);
                    await dungeonRoomRepository.InvalidateCacheAsync(roomId, ct);
                    await dungeonLobbySubscriptionService.PublishAsync(roomId, ct);
                    return Result<DungeonRoom>.Success(room);
                }

                if (room.Status == RoomStatus.Playing)
                {
                    // SocketServer 재시작으로 메모리가 비었을 때 _userRoomIndex 재구성용 재발행 (CreateRoom() 은 멱등)
                    var retryMessage = new GameStartRequestedMessage
                    {
                        RoomId = room.RoomId,
                        PlayerInfos = playerInfos,
                        TraceId = traceId,
                        MapId = effectiveMapId
                    };
                    var retryOutbox = OutboxMessage.Create(
                        OutboxTopics.GameStartRequested,
                        System.Text.Json.JsonSerializer.Serialize(retryMessage));
                    await outboxRepository.AddWithRoomUpdateAsync(room, retryOutbox, ct);
                    await dungeonRoomRepository.InvalidateCacheAsync(roomId, ct);
                    await dungeonLobbySubscriptionService.PublishAsync(roomId, ct);
                    return Result<DungeonRoom>.Success(room);
                }

                return Result<DungeonRoom>.Failure(ErrorCodes.RoomAlreadyPlaying, ErrorMessages.RoomAlreadyPlaying);
            }

            // Main 이탈 시점에 휘발(Redis) 위치를 DB 로 확정한다. 실패해도 시작은 막지 않는다 — 유실되면 저작 스폰으로 폴백
            foreach (var player in players)
            {
                try
                {
                    await userPositionService.FlushAsync(player.UserId, ct);
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "StartGame: 위치 확정 실패 user {UserId} (게임 시작은 계속)", player.UserId);
                }
            }

            var message = new GameStartRequestedMessage
            {
                RoomId = room.RoomId,
                PlayerInfos = playerInfos,
                TraceId = traceId,
                MapId = effectiveMapId
            };

            var outboxMessage = OutboxMessage.Create(
                OutboxTopics.GameStartRequested,
                System.Text.Json.JsonSerializer.Serialize(message));

            await outboxRepository.AddWithRoomUpdateAsync(room, outboxMessage, ct);
            await dungeonRoomRepository.InvalidateCacheAsync(roomId, ct);
            await roomReadyStore.ClearAsync(roomId, ct);

            await dungeonLobbySubscriptionService.PublishAsync(roomId, ct);
            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to start game for room {RoomId}", roomId);
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<long>> ValidateSubscriptionAsync(string sessionId, long roomId, CancellationToken ct = default)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (userSession is null)
                return Result<long>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var room = await dungeonRoomRepository.GetByIdAsync(roomId, ct);
            if (room is null)
                return Result<long>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);

            var roomPlayer = await dungeonRoomPlayerRepository.GetByUserIdAsync(userSession.UserId, ct);
            if (roomPlayer is null || roomPlayer.RoomId != roomId)
                return Result<long>.Failure(ErrorCodes.NotInRoom, ErrorMessages.NotInRoom);

            return Result<long>.Success(userSession.UserId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to validate subscription for session {SessionId} and room {RoomId}", sessionId, roomId);
            return Result<long>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<DungeonRoom>> RemovePlayerFromRoomAsync(long roomId, long userId, CancellationToken ct = default)
    {
        try
        {
            // GetByIdAsync 는 없는 방에 KeyNotFoundException 을 던진다. PlayerLeft 는 at-least-once 라
            // 삭제된 방으로 중복 전달될 수 있으므로 not-found 는 멱등 no-op 으로 처리한다
            DungeonRoom? room;
            try
            {
                room = await dungeonRoomRepository.GetByIdAsync(roomId, ct);
            }
            catch (KeyNotFoundException)
            {
                room = null;
            }

            if (room is null)
            {
                logger.LogInformation("RemovePlayerFromRoom: Room {RoomId} already gone — idempotent no-op", roomId);
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);
            }

            // 이미 소속이 아니면 멱등 성공
            var roomPlayer = await dungeonRoomPlayerRepository.GetByUserIdAsync(userId, ct);
            if (roomPlayer is null || roomPlayer.RoomId != roomId)
                return Result<DungeonRoom>.Success(room);

            var players = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);
            var remainingPlayers = players
                .Where(player => player.UserId != userId)
                .OrderBy(player => player.JoinedAt)
                .ToList();

            // association 제거 — 재로그인 시 CurrentRoomId 로 복원되지 않게
            await dungeonRoomPlayerRepository.RemoveAsync(roomId, userId, ct);
            await roomReadyStore.SetReadyAsync(roomId, userId, false, ct);

            var userSession = await userSessionRepository.GetSessionByUserIdAsync(userId, ct);
            if (userSession is not null)
                await chatSubscriptionService.UpdateRoomSubscriptionAsync(userSession.SessionId, 0, ct);

            if (remainingPlayers.Count == 0)
            {
                await dungeonRoomPlayerRepository.RemoveByRoomIdAsync(roomId, ct);
                await roomReadyStore.ClearAsync(roomId, ct);
                var deleted = await dungeonRoomRepository.DeleteAsync(roomId, ct);
                if (!deleted)
                    return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.DeleteRoomFailed);

                room.Close(); // 반환 객체 상태만 일관화 (DB row 는 삭제됨)
                logger.LogInformation("RemovePlayerFromRoom: Room {RoomId} emptied and deleted (last user {UserId})", roomId, userId);
            }
            else
            {
                // 호스트가 떠났으면 다음 플레이어로 이양
                if (room.IsHost(userId))
                    room.ChangeHost(remainingPlayers[0].UserId);

                var updated = await dungeonRoomRepository.UpdateAsync(room, ct);
                if (!updated)
                    return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.UpdateRoomFailed);

                await dungeonLobbySubscriptionService.PublishAsync(roomId, ct);
                logger.LogInformation("RemovePlayerFromRoom: User {UserId} left room {RoomId}, {Remaining} remain", userId, roomId, remainingPlayers.Count);
            }

            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            logger.LogError(e, "RemovePlayerFromRoomAsync failed for room {RoomId} user {UserId}", roomId, userId);
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<bool>> ReapRoomIfAbandonedAsync(long roomId, CancellationToken ct = default)
    {
        try
        {
            DungeonRoom? room;
            try
            {
                room = await dungeonRoomRepository.GetByIdAsync(roomId, ct);
            }
            catch (KeyNotFoundException)
            {
                room = null;
            }

            if (room is null || room.Status == RoomStatus.Closed)
                return Result<bool>.Success(false);

            var players = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);

            if (players.Count == 0)
            {
                // 고아 방 — 뺄 사람이 없으니 직접 삭제
                await dungeonRoomRepository.DeleteAsync(roomId, ct);
                logger.LogInformation("[Reaper] Deleted orphan room {RoomId} (no players)", roomId);
                return Result<bool>.Success(true);
            }

            var silentBefore = DateTime.UtcNow - reaperOptions.Value.Grace;
            if (!await AllPlayersSilentAsync(players, silentBefore, ct))
                return Result<bool>.Success(false);

            // 퇴장 경로 재사용 — 구독 해제·호스트 이양·빈 방 삭제가 그 안에 있다
            foreach (var player in players)
                await RemovePlayerFromRoomAsync(roomId, player.UserId, ct);

            logger.LogInformation("[Reaper] Reaped abandoned room {RoomId} ({PlayerCount} silent players)",
                roomId, players.Count);
            return Result<bool>.Success(true);
        }
        catch (Exception e)
        {
            logger.LogError(e, "ReapRoomIfAbandonedAsync failed for room {RoomId}", roomId);
            return Result<bool>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    private async Task<bool> AllPlayersSilentAsync(
        IReadOnlyCollection<DungeonRoomPlayer> players, DateTime silentBefore, CancellationToken ct)
    {
        foreach (var player in players)
        {
            var activeUntil = await userSessionRepository.GetSessionActiveUntilAsync(player.UserId, ct);

            // 신호 없음(세션이 사라짐) = 조용함
            if (activeUntil is null)
                continue;

            // 한 명이라도 유예 안에 활동했으면 건드리지 않는다
            if (activeUntil.Value > silentBefore)
                return false;
        }

        return true;
    }
}
