using GameServer.Application.Common;
using GameServer.Application.Services.DungeonLobby.Interfaces;
using GameServer.Domain.Entities;

namespace GameServer.Application.Services.DungeonLobby;

public class DungeonLobbyService(IDungeonRoomRepository dungeonRoomRepository) : IDungeonLobbyService
{
    public async Task<Result<DungeonRoom>> CreateDungeonRoomAsync(long userId, string roomName, int maxPlayers)
    {
        try
        {
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
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, e.Message);
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
            return Result<IEnumerable<DungeonRoom>>.Failure(ErrorCodes.InternalServerError, e.Message);
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
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, e.Message);
        }
    }
    
    public async Task<Result<DungeonRoom>> UpdateRoomSettingsAsync(
        long userId, 
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
        
            // 2. 도메인 로직 (설정 변경)
            try
            {
                room.UpdateRoomSettings(userId, roomName, maxPlayers);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.NotRoomHost, ex.Message);
            }
            catch (ArgumentException ex)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.InvalidRequest, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Result<DungeonRoom>.Failure(ErrorCodes.UpdateRoomFailed, ex.Message);
            }
            
            // 3. 저장
            var updated = await dungeonRoomRepository.UpdateAsync(room);
            if (!updated)
                return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, "방 업데이트 실패");
        
            return Result<DungeonRoom>.Success(room);
        }
        catch (Exception e)
        {
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, e.Message);
        }
    }

    public async Task<Result<DungeonRoom>> JoinRoomAsync(long userId, long roomId)
    {
        try
        {
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
                return Result<DungeonRoom>.Failure(ErrorCodes.JoinRoomFailed, ex.Message);
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
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, e.Message);
        }
    }

    public async Task<Result<DungeonRoom>> LeaveRoomAsync(long userId, long roomId)
    {
        try
        {
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
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, e.Message);
        }
    }

    public async Task<Result<DungeonRoom>> StartGameAsync(long userId, long roomId)
    {
        try
        {
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
            return Result<DungeonRoom>.Failure(ErrorCodes.InternalServerError, e.Message);
        }
    }
}