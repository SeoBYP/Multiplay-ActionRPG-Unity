using System.Security.Claims;
using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Services.DungeonLobby.Interfaces;
using GameServer.Grpc.DungeonLobby;
using Grpc.Core;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.API.Services;

public class DungeonLobbyGrpcService(IDungeonLobbyService dungeonLobbyService) : DungeonLobbyService.DungeonLobbyServiceBase
{
    public override async Task<CreateRoomResponse> CreateRoom(CreateRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
            return new CreateRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null,
                CreatedAt = new DateTimeOffset(DateTime.MinValue).ToUnixTimeSeconds(),
            };

        var result = await dungeonLobbyService.CreateDungeonRoomAsync(sessionId, request.RoomName, request.MaxPlayers);
        if (!result.IsSuccess || result.Value is null)
            return new CreateRoomResponse { Result = result.ToGrpcResult() };
        return new CreateRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value.ToRoomInfo(),
            CreatedAt = new DateTimeOffset(result.Value.CreatedAt).ToUnixTimeSeconds(),
        };
    }

    public override async Task<GetRoomResponse> GetRoom(GetRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
            return new GetRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null,
            };

        var result = await dungeonLobbyService.GetDungeonRoomAsync(request.RoomId);
        return new GetRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value?.ToRoomInfo(),
        };
    }

    public override async Task<GetRoomsResponse> GetRooms(GetRoomsRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
            return new GetRoomsResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfos = { }
            };
        var result = await dungeonLobbyService.GetActiveDungeonRoomsAsync();
        
        var response = new GetRoomsResponse
        {
            Result = result.ToGrpcResult(),
        };
        // TODO : ROOM Count 방안 고민
        foreach (var dungeonRoom in result.Value!)
        {
            response.RoomInfos.Add(dungeonRoom.ToRoomInfo());
        }
        return response;
    }

    public override async Task<JoinRoomResponse> JoinRoom(JoinRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
            return new JoinRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null
            };
        var result = await dungeonLobbyService.JoinRoomAsync(sessionId, request.RoomId);
        return new JoinRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value?.ToRoomInfo()
        };
    }

    public override async Task<LeaveRoomResponse> LeaveRoom(LeaveRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
            return new LeaveRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
            };
        var result = await dungeonLobbyService.LeaveRoomAsync(sessionId, request.RoomId);
        return new LeaveRoomResponse
        {
            Result = result.ToGrpcResult(),
        };
    }

    public override async Task<StartRoomResponse> StartRoom(StartRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
            return new StartRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null
            };
        var result = await dungeonLobbyService.StartGameAsync(sessionId, request.RoomId);
        return new StartRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value?.ToRoomInfo(),
        };
    }

    public override async Task<UpdateRoomResponse> UpdateRoom(UpdateRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
            return new UpdateRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null
            };
        var result = await dungeonLobbyService.UpdateRoomSettingsAsync(sessionId, request.RoomId, request.RoomName, request.MaxPlayers);
        return new UpdateRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value?.ToRoomInfo()
        };
    }
}