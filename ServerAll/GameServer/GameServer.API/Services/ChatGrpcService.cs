using System.Security.Claims;
using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Services.Chat.Interfaces;
using GameServer.Grpc.Chat;
using Grpc.Core;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.API.Services;

public class ChatGrpcService(IChatService chatService,
    IChatSubscriptionService subscriptionService) : ChatService.ChatServiceBase
{
    public override async Task<SendChatResponse> SendChat(SendChatRequest request, ServerCallContext context)
    {
        var httpContext = context.GetHttpContext();
        var sessionId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sid);
        if (sessionId is null) 
            return new SendChatResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult()
            };

        var result = await chatService.SendMessageAsync(sessionId,
            request.ChatType.ToDomain(), 
            request.Message,
            request.RoomId > 0 ? request.RoomId : null,
            request.TargetUserId > 0 ? request.TargetUserId : null);

        return new SendChatResponse
        {
            Result = result.ToGrpcResult()
        };
    }

    public override async Task SubscribeChat(SubscribeChatRequest request, IServerStreamWriter<ChatMessageInfo> responseStream, ServerCallContext context)
    {
        var httpContext = context.GetHttpContext();
        var sessionId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sid);
        if (sessionId is null) 
            return;

        var chatStream = request.ChatType switch
        {
            ChatType.Global => subscriptionService.SubscribeGlobalAsync(sessionId, context.CancellationToken),
            ChatType.Room   => subscriptionService.SubscribeRoomAsync(sessionId, request.RoomId, context.CancellationToken),
            _ => subscriptionService.SubscribeGlobalAsync(sessionId, context.CancellationToken)
        };
        
        try
        {
            await foreach (var msg in chatStream)
            {
                // 3. 변환 후 전송
                var info = msg.ToGrpc();
                await responseStream.WriteAsync(info);
            }
        }catch(OperationCanceledException e)
        {
            Console.WriteLine(e);
        }
    }
}