using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Script.System.GamePlayAbilitySystem;
using Serilog.Context;
using Server.Combat;
using Server.Room;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using Shared.Packet.Packets;

namespace Server.Consumer;

// 던전 밖(방 없음)이면 no-op — Main 솔로 회복은 클라 로컬.
public class PlayerConsumedConsumer(
    PlayerConsumedMessageQueue queue,
    RoomManager roomManager,
    ILogger<PlayerConsumedConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => ResilientStreamConsumer.RunAsync<PlayerConsumedMessage>(
            nameof(PlayerConsumedConsumer),
            queue.DequeueAllAsync,
            ProcessAsync,
            logger,
            stoppingToken);

    private Task ProcessAsync(PlayerConsumedMessage msg, CancellationToken ct)
    {
        using (LogContext.PushProperty("TraceId", msg.TraceId))
        {
            var room = roomManager.GetAssignedRoom(msg.UserId);
            if (room is null)
                return Task.CompletedTask;

            // at-least-once — 회복(+heal)은 비멱등이라 방 단위로 1회만 적용.
            if (!room.TryMarkConsumeHandled(msg.ConsumeId))
            {
                logger.LogInformation("[PlayerConsumed] 중복 통지 ConsumeId={ConsumeId} (User {UserId}) — 스킵",
                    msg.ConsumeId, msg.UserId);
                return Task.CompletedTask;
            }

            var mods = CombatEffectCatalog.Resolve(msg.EffectId);
            if (mods.Count == 0)
            {
                logger.LogWarning("[PlayerConsumed] 알 수 없는 EffectId={EffectId} (User {UserId}) — 무시", msg.EffectId, msg.UserId);
                return Task.CompletedTask;
            }

            var (newHp, _, _) = room.Progress.ApplyPlayerEffect(msg.UserId, mods);

            room.Sessions.Broadcast(new S_ApplyEffect
            {
                InstanceId = room.NextEffectInstanceId(),
                EffectId = msg.EffectId,
                TargetId = msg.UserId,
                SourceId = msg.UserId,
                StartTick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Stacks = 1,
            });

            logger.LogInformation("[PlayerConsumed] User {UserId} heal {EffectId} → HP {Hp}", msg.UserId, msg.EffectId, newHp);
            return Task.CompletedTask;
        }
    }
}
