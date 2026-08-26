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

/// <summary>
/// 소모품 소비 통지 소비(GameServer 검증·차감 완료 → 서버 권위 회복). authority-model §4.
/// userId 로 방을 찾아 `Room.ApplyPlayerEffect(+heal)` 로 서버 HP 를 올리고 S_ApplyEffect 로 브로드캐스트한다
/// (클라는 즉발 미러). 던전 밖(방 없음)이면 no-op — Main 솔로 회복은 클라 로컬(§2).
/// </summary>
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
                return Task.CompletedTask; // 던전 밖 — 회복은 클라 로컬(Main). no-op.

            // at-least-once 라 같은 통지가 다시 올 수 있다. 회복(+heal)은 비멱등이므로 방 단위로 1회만 적용한다.
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

            // 클라 미러용 브로드캐스트 — EffectReceiver 가 같은 effectId 로 로컬 ASC 적용(서버 HP=진실).
            room.Sessions.Broadcast(new S_ApplyEffect
            {
                InstanceId = room.NextEffectInstanceId(),
                EffectId = msg.EffectId,
                TargetId = msg.UserId,
                SourceId = msg.UserId, // 자기 회복
                StartTick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Stacks = 1,
            });

            logger.LogInformation("[PlayerConsumed] User {UserId} heal {EffectId} → HP {Hp}", msg.UserId, msg.EffectId, newHp);
            return Task.CompletedTask;
        }
    }
}
