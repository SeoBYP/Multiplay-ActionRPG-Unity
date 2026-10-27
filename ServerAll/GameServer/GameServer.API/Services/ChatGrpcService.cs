using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Domains.Chat;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Domain.Entities.Chat;
using GameServer.Grpc.Chat;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ChatService = GameServer.Grpc.Chat.ChatService;

namespace GameServer.API.Services;

public class ChatGrpcService(
    IChatService chatService,
    IChatSubscriptionService subscriptionService,
    ILogger<ChatGrpcService> logger) : ChatService.ChatServiceBase
{
    public override async Task ChatStream(
        IAsyncStreamReader<ChatClientMessage> requestStream,
        IServerStreamWriter<ChatServerMessage> responseStream,
        ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var sessionId = context.GetSessionId();
        logger.LogInformation("Chat stream connected for session {SessionId}", sessionId);

        var ctx = await subscriptionService.ConnectAsync(sessionId, ct);
        if (ctx is null)
        {
            logger.LogWarning("Chat stream connection rejected for session {SessionId}", sessionId);
            return;
        }

        try
        {
            var receiveTask = ReceiveLoopAsync(requestStream, ctx, sessionId, ct);
            var sendTask = SendLoopAsync(responseStream, ctx, ct);

            // WhenAll 만 쓰면 Receive 가 먼저 끝났을 때 Send 가 큐 대기에 걸린다 — 하나라도 끝나면 ctx.Stop() 으로 깨운다.
            await Task.WhenAny(receiveTask, sendTask);

            ctx.Stop();

            await Task.WhenAll(receiveTask, sendTask);
        }
        finally
        {
            await subscriptionService.DisconnectAsync(ctx, context.CancellationToken);
            logger.LogInformation("Chat stream disconnected for user {UserId}", ctx.UserId);
        }
    }

    private async Task ReceiveLoopAsync(
        IAsyncStreamReader<ChatClientMessage> requestStream,
        UserChatContext ctx,
        string sessionId,
        CancellationToken ct)
    {
        try
        {
            await foreach (var msg in requestStream.ReadAllAsync(ct))
            {
                switch (msg.PayloadCase)
                {
                    case ChatClientMessage.PayloadOneofCase.Chat:
                    {
                        logger.LogDebug("Received chat message from user {UserId}", ctx.UserId);
                        // 수신은 Redis → OnRedisMessage 콜백 → ctx.Outbound 경로로 돌아온다
                        _ = await chatService.SendMessageAsync(
                            sessionId,
                            msg.Chat.Message,
                            string.IsNullOrWhiteSpace(msg.Chat.TargetUserNickname) ? null : msg.Chat.TargetUserNickname,
                            ct);

                        break;
                    }

                    case ChatClientMessage.PayloadOneofCase.Reconnect:
                    {
                        logger.LogInformation("Received chat reconnect for user {UserId} from message {LastMessageId}", ctx.UserId, msg.Reconnect.LastMessageId);
                        var history = await chatService.GetMessagesAfterAsync(
                            sessionId,
                            msg.Reconnect.LastMessageId,
                            ct);

                        foreach (var chatMsg in history)
                        {
                            // bounded channel 이 가득 차면 TryWrite 가 실패할 수 있다 — 무시
                            ctx.Outbound.Writer.TryWrite(chatMsg);
                        }

                        break;
                    }

                    case ChatClientMessage.PayloadOneofCase.None:
                    default:
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // client disconnect / server cancel
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Cancelled or StatusCode.Unavailable)
        {
            // stream cancelled/unavailable
        }
        finally
        {
            // ReceiveLoop이 끝나면 SendLoop이 영원히 대기하지 않도록 종료 신호
            ctx.Stop();
        }
    }

    private async Task SendLoopAsync(
        IServerStreamWriter<ChatServerMessage> responseStream,
        UserChatContext ctx,
        CancellationToken ct)
    {
        try
        {
            await foreach (var chatMsg in ctx.Outbound.Reader.ReadAllAsync(ct))
            {
                var serverMsg = new ChatServerMessage
                {
                    Chat = chatMsg.ToGrpc()
                };

                await responseStream.WriteAsync(serverMsg, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // client disconnect / server cancel
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Cancelled or StatusCode.Unavailable)
        {
            // stream cancelled/unavailable
        }
        finally
        {
            // SendLoop이 먼저 끝나도 ReceiveLoop를 종료시키기 위해 stop
            ctx.Stop();
        }
    }


}
