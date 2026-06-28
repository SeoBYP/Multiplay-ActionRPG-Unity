using GameServer.Application.Common;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.Outbox;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Domain.Entities.Outbox;
using Microsoft.Extensions.Logging;
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
    ILogger<DungeonLobbyService> logger) : IDungeonLobbyService
{
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
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create dungeon room");
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<IEnumerable<DungeonRoom>>> GetActiveDungeonRoomsAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await dungeonRoomRepository.GetAllActiveRoomsAsync(ct);
            return Result<IEnumerable<DungeonRoom>>.Success(result.Where(data => data.Status != RoomStatus.Closed));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get active dungeon rooms");
            return Result<IEnumerable<DungeonRoom>>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
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

            var room = await dungeonRoomRepository.GetByIdAsync(roomId, ct);
            if (room is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);

            if (room.Status != RoomStatus.Waiting)
                return Result<DungeonRoom>.Failure(ErrorCodes.JoinRoomFailed, ErrorMessages.RoomNotWaiting);

            var existingRoomPlayer = await dungeonRoomPlayerRepository.GetByUserIdAsync(userSession.UserId, ct);
            if (existingRoomPlayer is not null)
                return Result<DungeonRoom>.Failure(ErrorCodes.AlreadyInRoom, ErrorMessages.AlreadyInRoom);

            var currentPlayers = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);
            if (currentPlayers.Count >= room.MaxPlayers)
                return Result<DungeonRoom>.Failure(ErrorCodes.JoinRoomFailed, ErrorMessages.RoomFull);

            await dungeonRoomPlayerRepository.CreateAsync(roomId, userSession.UserId, ct);
            await chatSubscriptionService.UpdateRoomSubscriptionAsync(sessionId, roomId, ct);

            await dungeonLobbySubscriptionService.PublishAsync(roomId, ct);
            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to join room {RoomId}", roomId);
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
            await chatSubscriptionService.UpdateRoomSubscriptionAsync(sessionId, 0, ct);

            if (remainingPlayers.Count == 0)
            {
                room.Close();
                await dungeonRoomPlayerRepository.RemoveByRoomIdAsync(roomId, ct);

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

            // 2. 채팅 방 구독 해제 — userId로 세션을 찾아 처리.
            var userSession = await userSessionRepository.GetSessionByUserIdAsync(userId, ct);
            if (userSession is not null)
                await chatSubscriptionService.UpdateRoomSubscriptionAsync(userSession.SessionId, 0, ct);

            if (remainingPlayers.Count == 0)
            {
                // 3a. 빈 방 → 삭제 (gRPC LeaveRoom 빈방 경로와 통일).
                await dungeonRoomPlayerRepository.RemoveByRoomIdAsync(roomId, ct);
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
}
