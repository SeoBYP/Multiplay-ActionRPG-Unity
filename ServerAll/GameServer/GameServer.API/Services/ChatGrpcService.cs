using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Services.Chat.Interfaces;
using GameServer.Domain.Entities.Chat;
using GameServer.Grpc.Chat;
using Grpc.Core;

namespace GameServer.API.Services;

public class ChatGrpcService(
    IChatService chatService,
    IChatSubscriptionService subscriptionService) : ChatService.ChatServiceBase
{
    public override async Task ChatStream(
        IAsyncStreamReader<ChatClientMessage> requestStream,
        IServerStreamWriter<ChatServerMessage> responseStream,
        ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var sessionId = context.GetSessionId();

        // ConnectAsync 내부: 세션 조회 + ctx 생성 + Redis 구독
        var ctx = await subscriptionService.ConnectAsync(sessionId, ct);
        if (ctx is null)
            return;

        try
        {
            var receiveTask = ReceiveLoopAsync(requestStream, ctx, sessionId, ct);
            var sendTask = SendLoopAsync(responseStream, ctx, ct);

            // WhenAll만 쓰면 Receive가 먼저 끝났을 때 Send가 큐 대기로 걸릴 수 있음.
            // 먼저 하나라도 종료되면 ctx.Stop()로 SendLoop을 안전하게 깨운 뒤 둘 다 종료 대기.
            await Task.WhenAny(receiveTask, sendTask);

            ctx.Stop(); // SendLoop/ReceiveLoop 모두 종료 유도

            await Task.WhenAll(receiveTask, sendTask);
        }
        finally
        {
            // DisconnectAsync 내부에서 ctx.Stop() + UnsubscribeAll + registry 제거
            await subscriptionService.DisconnectAsync(ctx, context.CancellationToken);
        }
    }

    private async Task ReceiveLoopAsync(
        IAsyncStreamReader<ChatClientMessage> requestStream,
        GameServer.Application.Services.Chat.UserChatContext ctx,
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
                        // ChatService가 ChatType 결정 + 저장 + Redis Publish
                        // Redis → OnRedisMessage 콜백 → ctx.Outbound에 push
                        _ = await chatService.SendMessageAsync(
                            sessionId,
                            msg.Chat.Message,
                            string.IsNullOrWhiteSpace(msg.Chat.TargetUserNickname) ? null : msg.Chat.TargetUserNickname,
                            ct);

                        break;
                    }

                    case ChatClientMessage.PayloadOneofCase.Reconnect:
                    {
                        // 밀린 메시지 조회 후 큐에 직접 Push
                        var history = await chatService.GetMessagesAfterAsync(
                            sessionId,
                            msg.Reconnect.LastMessageId,
                            ct);

                        foreach (var chatMsg in history)
                        {
                            // bounded channel이라 가득 차면 TryWrite 실패 가능
                            // (정책상 DropOldest라면 대부분 성공; 실패하면 그냥 넘어가도 됨)
                            ctx.Outbound.Writer.TryWrite(chatMsg);
                        }

                        break;
                    }

                    case ChatClientMessage.PayloadOneofCase.None:
                    default:
                        // ignore
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
        catch (Exception)
        {
            // TODO: log
        }
        finally
        {
            // ReceiveLoop이 끝나면 SendLoop이 영원히 대기하지 않도록 종료 신호
            ctx.Stop();
        }
    }

    private async Task SendLoopAsync(
        IServerStreamWriter<ChatServerMessage> responseStream,
        GameServer.Application.Services.Chat.UserChatContext ctx,
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
        catch (Exception)
        {
            // TODO: log
        }
        finally
        {
            // SendLoop이 먼저 끝나도 ReceiveLoop를 종료시키기 위해 stop
            ctx.Stop();
        }
    }


}