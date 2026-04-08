using GameServer.Application.Common;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.Outbox;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Domain.Entities.Outbox;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Messages;

namespace GameServer.Application.Domains.DungeonLobby;

public class DungeonLobbyService(
    IDungeonRoomRepository dungeonRoomRepository,
    IDungeonLobbySubscriptionService dungeonLobbySubscriptionService,
    IDungeonRoomPlayerRepository dungeonRoomPlayerRepository,
    IOutboxRepository outboxRepository,
    IUserSessionRepository userSessionRepository,
    IChatSubscriptionService chatSubscriptionService,
    ILogger<DungeonLobbyService> logger) : IDungeonLobbyService
{
    public async Task<Result<DungeonRoom>> CreateDungeonRoomAsync(string sessionId, string roomName, int maxPlayers, CancellationToken ct = default)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (userSession is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var existingRoomPlayer = await dungeonRoomPlayerRepository.GetByUserIdAsync(userSession.UserId, ct);
            if (existingRoomPlayer is not null)
                return Result<DungeonRoom>.Failure(ErrorCodes.AlreadyInRoom, ErrorMessages.AlreadyInRoom);

            var newRoom = await dungeonRoomRepository.CreateAsync(userSession.UserId, roomName, maxPlayers, ct);
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

    public async Task<Result<DungeonRoom>> StartGameAsync(string sessionId, long roomId, string traceId, CancellationToken ct = default)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
            if (userSession is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);

            var room = await dungeonRoomRepository.GetByIdAsync(roomId, ct);
            if (room is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);

            if (!room.IsHost(userSession.UserId))
                return Result<DungeonRoom>.Failure(ErrorCodes.NotRoomHost, ErrorMessages.NotRoomHost);

            var players = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);
            room.StartGame(userSession.UserId, players.Count);

            var outboxMessage = OutboxMessage.Create(
                OutboxTopics.GameStartRequested,
                System.Text.Json.JsonSerializer.Serialize(new GameStartRequestedMessage
                {
                    RoomId = room.RoomId,
                    PlayerIds = players.Select(player => player.UserId).ToList(),
                    TraceId = traceId
                }));

            await outboxRepository.AddWithRoomUpdateAsync(room, outboxMessage, ct);

            await dungeonLobbySubscriptionService.PublishAsync(roomId, ct);
            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to start game for room {RoomId}", roomId);
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }
}
