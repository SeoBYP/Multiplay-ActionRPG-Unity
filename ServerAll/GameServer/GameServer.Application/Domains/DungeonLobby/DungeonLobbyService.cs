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
    /// <summary>
    /// 방 단위 임계구역 키. "인원 수를 읽고 → 한 명 넣는다" 처럼 검사와 쓰기가 갈라진 구간을 감싼다.
    /// (한 유저가 동시에 두 방에 들어가는 축은 dungeon_room_players.UserId UNIQUE 제약이 막는다)
    /// </summary>
    private static string RoomLockKey(long roomId) => $"room:{roomId}";

    public async Task<Result<DungeonRoom>> CreateDungeonRoomAsync(string sessionId, string roomName, int maxPlayers, string mapId = "", CancellationToken ct = default)
    {
        try
        {
            // 던전 선택값 정규화·검증(서버 권위): 비우면 기본 맵, 알 수 없는 맵이면 거부.
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
            // check-then-act 가 경합에서 뚫렸을 때의 최종 방어선(UNIQUE 제약).
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
            // 정렬·건너뛰기·자르기는 DB 가 한다(B4). 안정 정렬(RoomId 내림차순 = 최신 먼저)이
            // 페이징의 전제라 저장소 계약에 못 박아 두었다.
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

            // 정원 검사와 입장 기록 사이에 다른 요청이 끼어들면 정원을 넘긴다.
            // 검사~쓰기 전체를 방 단위 임계구역으로 묶는다(F1).
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

            // 새로 들어온 사람은 항상 미준비 상태로 시작한다(이전 방의 잔재 차단).
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

            // 시작 절차에 들어간 방의 준비 상태를 뒤집는 것은 의미가 없다.
            if (room.Status != RoomStatus.Waiting)
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotWaiting, ErrorMessages.RoomNotWaiting);

            // 호스트는 준비 개념이 없다 — 시작 버튼이 곧 호스트의 의사표시다.
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

            // 진실의 원천 = 방에 영속된 MapId(생성 시 결정). 명시 mapId(StartRoomRequest.map_id, E2E override)가 오면 우선.
            // 둘 다 비면 기본 맵으로 폴백(구버전 캐시·미설정 방어).
            var effectiveMapId = !string.IsNullOrEmpty(mapId) ? mapId
                : !string.IsNullOrEmpty(room.MapId) ? room.MapId
                : MapIds.Default;

            if (!room.IsHost(userSession.UserId))
                return Result<DungeonRoom>.Failure(ErrorCodes.NotRoomHost, ErrorMessages.NotRoomHost);

            var players = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);

            // 준비 게이트 — 호스트를 뺀 전원이 준비해야 시작된다(서버 권위).
            // 클라의 버튼 비활성화는 UX일 뿐이라 여기서 다시 판정한다.
            // Waiting 일 때만 본다: Starting/Playing 재시도 경로는 이미 준비 목록이 비워진 뒤라 판정 대상이 아니다.
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
                // 합산 전투 스탯(서버 권위)을 계산해 메시지에 적재 — SocketServer 는 DB 접근 없이 이 결과를 쓴다(§4c).
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
                // Room is already Starting or Playing — handle idempotently
                if (room.Status == RoomStatus.Starting)
                {
                    // Previous GameStartRequested was lost; re-trigger without changing room state
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
                    // SocketServer memory is empty after restart — re-send GameStartRequestedMessage
                    // so SocketServer re-initializes _userRoomIndex. CreateRoom() is idempotent (null if already exists).
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

            // Main 을 떠나는 시점 — 휘발(Redis) 위치를 DB 로 확정한다(B7).
            // 이 한 번 덕분에 "던전 갔다 오면 입장 직전 자리에서 시작"이 별도 로직 없이 성립한다.
            // 실패해도 게임 시작을 막지 않는다 — 위치는 편의 기능이고, 유실되면 저작 스폰으로 폴백된다.
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
            // 시작이 확정된 방의 준비 목록은 더 이상 의미가 없다.
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
            // 실제 DungeonRoomRepository.GetByIdAsync는 없는 방에 null이 아니라 KeyNotFoundException을
            // 던진다. PlayerLeft는 at-least-once 스트림이라 이미 삭제된 방으로 중복 전달될 수 있으므로,
            // not-found는 멱등 no-op으로 처리해 generic catch(→INTERNAL_SERVER_ERROR)로 빠지지 않게 한다.
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

            // 이미 이 방 소속이 아니면 멱등 성공 (at-least-once 중복 소비 안전).
            var roomPlayer = await dungeonRoomPlayerRepository.GetByUserIdAsync(userId, ct);
            if (roomPlayer is null || roomPlayer.RoomId != roomId)
                return Result<DungeonRoom>.Success(room);

            var players = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);
            var remainingPlayers = players
                .Where(player => player.UserId != userId)
                .OrderBy(player => player.JoinedAt)
                .ToList();

            // 1. association 제거 — 재로그인 시 CurrentRoomId로 복원되지 않게 한다.
            await dungeonRoomPlayerRepository.RemoveAsync(roomId, userId, ct);
            await roomReadyStore.SetReadyAsync(roomId, userId, false, ct);

            // 2. 채팅 방 구독 해제 — userId로 세션을 찾아 처리.
            var userSession = await userSessionRepository.GetSessionByUserIdAsync(userId, ct);
            if (userSession is not null)
                await chatSubscriptionService.UpdateRoomSubscriptionAsync(userSession.SessionId, 0, ct);

            if (remainingPlayers.Count == 0)
            {
                // 3a. 빈 방 → 삭제 (gRPC LeaveRoom 빈방 경로와 통일).
                await dungeonRoomPlayerRepository.RemoveByRoomIdAsync(roomId, ct);
                await roomReadyStore.ClearAsync(roomId, ct);
                var deleted = await dungeonRoomRepository.DeleteAsync(roomId, ct);
                if (!deleted)
                    return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.DeleteRoomFailed);

                room.Close(); // 반환 객체 상태만 일관화 (DB row는 삭제됨)
                logger.LogInformation("RemovePlayerFromRoom: Room {RoomId} emptied and deleted (last user {UserId})", roomId, userId);
            }
            else
            {
                // 3b. 호스트가 떠났으면 다음 플레이어로 이양.
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

            // 이미 사라진 방 = 멱등 no-op.
            if (room is null || room.Status == RoomStatus.Closed)
                return Result<bool>.Success(false);

            var players = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);

            if (players.Count == 0)
            {
                // association 이 하나도 없는 고아 방 — 뺄 사람이 없으니 방을 직접 지운다.
                await dungeonRoomRepository.DeleteAsync(roomId, ct);
                logger.LogInformation("[Reaper] Deleted orphan room {RoomId} (no players)", roomId);
                return Result<bool>.Success(true);
            }

            var silentBefore = DateTime.UtcNow - reaperOptions.Value.Grace;
            if (!await AllPlayersSilentAsync(players, silentBefore, ct))
                return Result<bool>.Success(false);

            // 기존 퇴장 경로를 그대로 쓴다 — 채팅 구독 해제·준비 상태·호스트 이양·빈 방 삭제가
            // 이미 그 안에서 처리된다(마지막 한 명을 빼는 순간 방이 사라진다).
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

            // 신호가 없으면(세션 자체가 사라짐) 조용한 것으로 본다.
            if (activeUntil is null)
                continue;

            // 한 명이라도 유예 안에 활동 흔적이 있으면 그 방은 건드리지 않는다.
            if (activeUntil.Value > silentBefore)
                return false;
        }

        return true;
    }
}
