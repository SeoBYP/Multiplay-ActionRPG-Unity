using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// S_ApplyEffect 수신 → 상태 저장소에 기록(이벤트 발행). 상위 EffectReceiver가 ASC에 적용한다.
    /// (네트워크 레이어는 ASC/카탈로그를 모른다 — 이동 동기화와 동일한 state 경유 패턴)
    /// </summary>
    public class EffectApplyPacketHandler : PacketHandlerBase<S_ApplyEffect>
    {
        private readonly ISocketPacketState _state;

        public EffectApplyPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_ApplyEffect packet)
        {
            _state.ApplyEffect(new SocketEffectApply(
                packet.EffectId,
                packet.InstanceId,
                packet.TargetId,
                packet.SourceId,
                packet.StartTick,
                packet.Stacks,
                packet.Amount));
            return UniTask.CompletedTask;
        }
    }

    /// <summary>S_RemoveEffect 수신 → 상태 저장소에 제거 이벤트 발행.</summary>
    public class EffectRemovePacketHandler : PacketHandlerBase<S_RemoveEffect>
    {
        private readonly ISocketPacketState _state;

        public EffectRemovePacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_RemoveEffect packet)
        {
            _state.RemoveEffect(packet.InstanceId);
            return UniTask.CompletedTask;
        }
    }
}
