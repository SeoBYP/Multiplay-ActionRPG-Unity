using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.GameSession.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Grpc.DungeonLobby;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using DungeonLobbyService = GameServer.Grpc.DungeonLobby.DungeonLobbyService;

namespace GameServer.API.Services;

public class DungeonLobbyGrpcService(IDungeonLobbyService dungeonLobbyService,
    IDungeonLobbySubscriptionService subscriptionService,
    IGameSessionRepository gameSessionRepository,
    IUserRepository userRepository,
    IDungeonRoomPlayerRepository dungeonRoomPlayerRepository,
    ILogger<DungeonLobbyGrpcService> logger) : DungeonLobbyService.DungeonLobbyServiceBase
{
    public override async Task<CreateRoomResponse> CreateRoom(CreateRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("CreateRoom rejected because session id was missing");
            return new CreateRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null,
                CreatedAt = new DateTimeOffset(DateTime.MinValue).ToUnixTimeSeconds(),
            };
        }

        logger.LogInformation("CreateRoom request received for session {SessionId}", sessionId);
        var result = await dungeonLobbyService.CreateDungeonRoomAsync(sessionId, request.RoomName, request.MaxPlayers, context.CancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            logger.LogWarning("CreateRoom failed for session {SessionId} with code {ErrorCode}", sessionId, result.InternalErrorCode);
            return new CreateRoomResponse { Result = result.ToGrpcResult() };
        }

        logger.LogInformation("CreateRoom succeeded for session {SessionId} with room {RoomId}", sessionId, result.Value.RoomId);
        return new CreateRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = await result.Value.ToRoomInfo(userRepository, dungeonRoomPlayerRepository),
            CreatedAt = new DateTimeOffset(result.Value.CreatedAt).ToUnixTimeSeconds(),
        };
    }

    public override async Task<GetRoomResponse> GetRoom(GetRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("GetRoom rejected because session id was missing");
            return new GetRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null,
            };
        }

        logger.LogInformation("GetRoom request received for room {RoomId}", request.RoomId);
        var result = await dungeonLobbyService.GetDungeonRoomAsync(request.RoomId, context.CancellationToken);
        if (!result.IsSuccess)
            logger.LogWarning("GetRoom failed for room {RoomId} with code {ErrorCode}", request.RoomId, result.InternalErrorCode);
        return new GetRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value is null ? null : await result.Value.ToRoomInfo(userRepository, dungeonRoomPlayerRepository),
        };
    }

    public override async Task<GetRoomsResponse> GetRooms(GetRoomsRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("GetRooms rejected because session id was missing");
            return new GetRoomsResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfos = { }
            };
        }

        logger.LogInformation("GetRooms request received for session {SessionId}", sessionId);
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
            response.RoomInfos.Add(await dungeonRoom.ToRoomInfo(userRepository, dungeonRoomPlayerRepository));
        }

        logger.LogInformation("GetRooms succeeded for session {SessionId} with {RoomCount} rooms", sessionId, response.RoomInfos.Count);
        return response;
    }

    public override async Task<JoinRoomResponse> JoinRoom(JoinRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("JoinRoom rejected because session id was missing");
            return new JoinRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null
            };
        }

        logger.LogInformation("JoinRoom request received for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        var result = await dungeonLobbyService.JoinRoomAsync(sessionId, request.RoomId, context.CancellationToken);
        if (result.IsSuccess)
            logger.LogInformation("JoinRoom succeeded for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        else
            logger.LogWarning("JoinRoom failed for session {SessionId} and room {RoomId} with code {ErrorCode}", sessionId, request.RoomId, result.InternalErrorCode);
        return new JoinRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value is null ? null : await result.Value.ToRoomInfo(userRepository, dungeonRoomPlayerRepository)
        };
    }

    public override async Task<LeaveRoomResponse> LeaveRoom(LeaveRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("LeaveRoom rejected because session id was missing");
            return new LeaveRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
            };
        }

        logger.LogInformation("LeaveRoom request received for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        var result = await dungeonLobbyService.LeaveRoomAsync(sessionId, request.RoomId, context.CancellationToken);
        if (result.IsSuccess)
            logger.LogInformation("LeaveRoom succeeded for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        else
            logger.LogWarning("LeaveRoom failed for session {SessionId} and room {RoomId} with code {ErrorCode}", sessionId, request.RoomId, result.InternalErrorCode);
        return new LeaveRoomResponse
        {
            Result = result.ToGrpcResult(),
        };
    }

    public override async Task<StartRoomResponse> StartRoom(StartRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("StartRoom rejected because session id was missing");
            return new StartRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null
            };
        }
        var traceId = context.GetHttpContext().Items["TraceId"] as string ?? "";
        logger.LogInformation("StartRoom request received for session {SessionId}, room {RoomId}, trace {TraceId}", sessionId, request.RoomId, traceId);
        var result = await dungeonLobbyService.StartGameAsync(sessionId, request.RoomId,traceId ,context.CancellationToken);
        if (result.IsSuccess)
            logger.LogInformation("StartRoom succeeded for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        else
            logger.LogWarning("StartRoom failed for session {SessionId} and room {RoomId} with code {ErrorCode}", sessionId, request.RoomId, result.InternalErrorCode);
        return new StartRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value is null ? null : await result.Value.ToRoomInfo(userRepository, dungeonRoomPlayerRepository),
        };
    }

    public override async Task<UpdateRoomResponse> UpdateRoom(UpdateRoomRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
        {
            logger.LogWarning("UpdateRoom rejected because session id was missing");
            return new UpdateRoomResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult(),
                RoomInfo = null
            };
        }

        logger.LogInformation("UpdateRoom request received for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        var result = await dungeonLobbyService.UpdateRoomSettingsAsync(sessionId, request.RoomId, request.RoomName, request.MaxPlayers, context.CancellationToken);
        if (result.IsSuccess)
            logger.LogInformation("UpdateRoom succeeded for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        else
            logger.LogWarning("UpdateRoom failed for session {SessionId} and room {RoomId} with code {ErrorCode}", sessionId, request.RoomId, result.InternalErrorCode);
        return new UpdateRoomResponse
        {
            Result = result.ToGrpcResult(),
            RoomInfo = result.Value is null ? null : await result.Value.ToRoomInfo(userRepository, dungeonRoomPlayerRepository)
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
        logger.LogInformation("SubscribeRoom started for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
        
        // ConnectAsync 내부: 세션 조회 + ctx 생성 + Redis 구독
        var ctx = await subscriptionService.SubscribeAsync(sessionId, request.RoomId, ct);
        if (ctx is null)
        {
            logger.LogWarning("SubscribeRoom rejected for session {SessionId} and room {RoomId}", sessionId, request.RoomId);
            return;
        }

        try
        {
            await SendLoopAsync(responseStream, ctx, ct);
        }
        finally
        {
            await subscriptionService.UnsubscribeAsync(ctx, ct);
            logger.LogInformation("SubscribeRoom ended for user {UserId} and room {RoomId}", ctx.UserId, ctx.RoomId);
        }
    }

    private async Task SendLoopAsync(
        IServerStreamWriter<SubscribeRoomResponse> responseStream,
        UserRoomContext ctx,
        CancellationToken ct)
    {
        await foreach (var roomId in ctx.Outbound.Reader.ReadAllAsync(ct))
        {
            var room = await dungeonLobbyService.GetDungeonRoomAsync(roomId, ct);
            if (room.IsSuccess == false) continue;
            if (room.Value is null) continue;

            var serverMsg = new SubscribeRoomResponse();
            switch (room.Value.Status)
            {
                case RoomStatus.Playing:
                    var gameSession = await gameSessionRepository.GetByRoomIdAsync(room.Value.RoomId, ct);
                    if (gameSession is null)
                    {
                        logger.LogWarning("Game session not found for playing room {RoomId}", room.Value.RoomId);
                        continue;
                    }

                    serverMsg.GameSessionEvent = new GameSessionReadyEvent
                    {
                        Ip = gameSession.SocketIp,
                        Port = gameSession.SocketPort
                    };
                    break;

                default:
                    serverMsg.UpdateEvent = new RoomUpdatedEvent
                    {
                        RoomInfo = await room.Value.ToRoomInfo(userRepository, dungeonRoomPlayerRepository),
                    };
                    break;
            }

            await responseStream.WriteAsync(serverMsg, ct);
        }
    }
}
