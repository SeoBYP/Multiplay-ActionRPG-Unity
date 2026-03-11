using System.Security.Claims;
using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Grpc.DungeonLobby;
using Grpc.Core;
using Microsoft.IdentityModel.JsonWebTokens;
using DungeonLobbyService = GameServer.Grpc.DungeonLobby.DungeonLobbyService;

namespace GameServer.API.Services;

public class DungeonLobbyGrpcService(IDungeonLobbyService dungeonLobbyService,
    IDungeonLobbySubscriptionService subscriptionService,
    IUserRepository userRepository) : DungeonLobbyService.DungeonLobbyServiceBase
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

        var result = await dungeonLobbyService.CreateDungeonRoomAsync(sessionId, request.RoomName, request.MaxPlayers, context.CancellationToken);
        if (!result.IsSuccess || result.Value is null)
            return new CreateRoomResponse { Result = result.ToGrpcResult() };
        return new CreateRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = await result.Value.ToRoomInfo(userRepository),
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

        var result = await dungeonLobbyService.GetDungeonRoomAsync(request.RoomId, context.CancellationToken);
        return new GetRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = await result.Value?.ToRoomInfo(userRepository),
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
        var result = await dungeonLobbyService.GetActiveDungeonRoomsAsync(context.CancellationToken);
        
        var response = new GetRoomsResponse
        {
            Result = result.ToGrpcResult(),
        };
        // TODO : ROOM Count 방안 고민
        if (result.Value is null) 
            throw new InvalidOperationException("Room List is null");
        foreach (var dungeonRoom in result.Value)
        {
            response.RoomInfos.Add(await dungeonRoom.ToRoomInfo(userRepository));
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
        var result = await dungeonLobbyService.JoinRoomAsync(sessionId, request.RoomId, context.CancellationToken);
        return new JoinRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo =  await result.Value?.ToRoomInfo(userRepository)
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
        var result = await dungeonLobbyService.LeaveRoomAsync(sessionId, request.RoomId, context.CancellationToken);
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
        var result = await dungeonLobbyService.StartGameAsync(sessionId, request.RoomId, context.CancellationToken);
        return new StartRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = await result.Value?.ToRoomInfo( userRepository),
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
        var result = await dungeonLobbyService.UpdateRoomSettingsAsync(sessionId, request.RoomId, request.RoomName, request.MaxPlayers, context.CancellationToken);
        return new UpdateRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = await result.Value?.ToRoomInfo(userRepository)
        };
    }

    public override async Task SubscribeRoom(SubscribeRoomRequest request,
        IServerStreamWriter<SubscribeRoomResponse> responseStream,
        ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
            throw new InvalidOperationException("Session ID cannot be null");
        
        // ConnectAsync 내부: 세션 조회 + ctx 생성 + Redis 구독
        var ctx = await subscriptionService.SubscribeAsync(sessionId,request.RoomId, ct);
        if (ctx is null)
            return;

        try
        {
            await SendLoopAsync(responseStream, ctx, ct);  // 이것만 있으면 돼요
        }
        finally
        {
            await subscriptionService.UnsubscribeAsync(ctx, ct);
        }
    }

    private async Task SendLoopAsync(
        IServerStreamWriter<SubscribeRoomResponse> responseStream,
        UserRoomContext ctx,
        CancellationToken ct)
    {
        try
        {
            await foreach (var roomId in ctx.Outbound.Reader.ReadAllAsync(ct))
            {
                var room = await dungeonLobbyService.GetDungeonRoomAsync(roomId, ct);
                if(room.IsSuccess == false) continue;
                var serverMsg = new SubscribeRoomResponse();
                switch (room.Value.Status)
                {
                    case RoomStatus.Playing:
                        serverMsg.StartEvent = new GameStartedEvent
                        {
                            RoomInfo = await room.Value.ToRoomInfo(userRepository),
                            Ip = "127.0.0.1", // Test용 IP
                            Port = 12345, // Test용 Port
                        };
                        break;
                    default:
                        serverMsg.UpdateEvent = new RoomUpdatedEvent
                        {
                            RoomInfo = await room.Value.ToRoomInfo(userRepository),
                        };
                        break;
                }
                await responseStream.WriteAsync(serverMsg, ct);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}