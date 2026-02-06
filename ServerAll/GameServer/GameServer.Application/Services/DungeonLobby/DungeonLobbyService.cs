using GameServer.Application.Common;
using GameServer.Application.Services.DungeonLobby.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Domain.Interfaces.DungeonRoom;
using GameServer.Domain.Interfaces.User;

namespace GameServer.Application.Services.DungeonLobby;

public class DungeonLobbyService(IDungeonRoomRepository dungeonRoomRepository,IUserSessionRepository userSessionRepository) : IDungeonLobbyService
{
    public async Task<Result<DungeonRoom>> CreateDungeonRoomAsync(string sessionId, string roomName, int maxPlayers)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId);
            if (userSession is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            var userId = userSession.UserId;
            
            var existingRoom = await dungeonRoomRepository.GetByUserIdAsync(userId);
            if (existingRoom is not null)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.AlreadyInRoom,
                    ErrorMessages.AlreadyInRoom);
            }

            var newRoom = await dungeonRoomRepository.CreateAsync(userId, roomName, maxPlayers);
            if (newRoom is null)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
            }

            return Result<DungeonRoom>.Success(newRoom);
        }
        catch (Exception e)
        {
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<IEnumerable<DungeonRoom>>> GetActiveDungeonRoomsAsync()
    {
        try
        {
            var result = await dungeonRoomRepository.GetAllActiveRoomsAsync();
            
            return Result<IEnumerable<DungeonRoom>>.Success(result.Where(data => data.Status != RoomStatus.Closed));
        }
        catch (Exception e)
        {
            return Result<IEnumerable<DungeonRoom>>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<DungeonRoom>> GetDungeonRoomAsync(long roomId)
    {
        try
        {
            var room = await dungeonRoomRepository.GetByIdAsync(roomId);
            if (room is null)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);
            }

            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }
    
    public async Task<Result<DungeonRoom>> UpdateRoomSettingsAsync(
        string sessionId,  
        long roomId, 
        string? roomName = null, 
        int? maxPlayers = null)
    {
        try
        {
            // 1. 방 조회
            var room = await dungeonRoomRepository.GetByIdAsync(roomId);
            if (room is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);
        
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId);
            if (userSession is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            var userId = userSession.UserId;
            
            // 2. 도메인 로직 (설정 변경)
            try
            {
                room.UpdateRoomSettings(userId, roomName, maxPlayers);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.NotRoomHost, ErrorMessages.InternalServerError);
            }
            catch (ArgumentException ex)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InternalServerError);
            }
            catch (InvalidOperationException ex)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.UpdateRoomFailed, ErrorMessages.InternalServerError);
            }
            
            // 3. 저장
            var updated = await dungeonRoomRepository.UpdateAsync(room);
            if (!updated)
                return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, "방 업데이트 실패");
        
            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<DungeonRoom>> JoinRoomAsync(string sessionId, long roomId)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId);
            if (userSession is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            var userId = userSession.UserId;
            
            // 이미 다른 방이 있는지 확인
            var existingRoom = await dungeonRoomRepository.GetByUserIdAsync(userId);
            if (existingRoom is not null && existingRoom.RoomId != roomId)
            {
                return Result<DungeonRoom>.Failure(
                    ErrorCodes.AlreadyInRoom,
                    ErrorMessages.AlreadyInRoom);
            }

            // 2. 방 조회
            var room = await dungeonRoomRepository.GetByIdAsync(roomId);
            if (room is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);
            
            if(room.IsExist(userId))
                return Result<DungeonRoom>.Failure(ErrorCodes.AlreadyInRoom, ErrorMessages.AlreadyInRoom);
            
            // 3. 도메인 로직 (Join)
            try
            {
                room.Join(userId);
            }
            catch (InvalidOperationException ex)
            {
                // room.Join에서 이미 검증함 (중복, Full, Status)
                return Result<DungeonRoom>.Failure(ErrorCodes.JoinRoomFailed, ErrorMessages.InternalServerError);
            }

            // 4. 저장
            var success = await dungeonRoomRepository.UpdateAsync(room);
            if (!success)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.JoinRoomFailed);
            }

            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<DungeonRoom>> LeaveRoomAsync(string sessionId, long roomId)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId);
            if (userSession is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            var userId = userSession.UserId;
            
            // 방이 있는지 확인
            var room = await dungeonRoomRepository.GetByIdAsync(roomId);
            if (room is null)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);
            }

            if (room.IsExist(userId) == false)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.NotInRoom, ErrorMessages.NotInRoom);
            }

            // 방 Leave 처리 
            // 내부적으로 ErrorMessage 처리 
            room.Leave(userId);

            if (room.Status == RoomStatus.Closed)
            {
                var deleted = await dungeonRoomRepository.DeleteAsync(roomId);
                if (!deleted)
                    return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, "방 삭제 실패");
            }
            else
            {
                var updated = await dungeonRoomRepository.UpdateAsync(room);
                if (!updated)
                    return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, "방 업데이트 실패");
            }

            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }

    public async Task<Result<DungeonRoom>> StartGameAsync(string sessionId, long roomId)
    {
        try
        {
            var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId);
            if (userSession is null)
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            var userId = userSession.UserId;
            
            // 방이 있는지 확인
            var room = await dungeonRoomRepository.GetByIdAsync(roomId);
            if (room is null)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.RoomNotFound, ErrorMessages.RoomNotFound);
            }

            // 2. 방장 확인
            if (!room.IsHost(userId))
                return Result<DungeonRoom>.Failure(ErrorCodes.NotRoomHost, ErrorMessages.NotRoomHost);

            room.StartGame(userId);

            var updated = await dungeonRoomRepository.UpdateAsync(room);
            if (!updated)
                return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, "방 업데이트 실패");

            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
    }
}