using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// Actor 통합 발동 연출: 서버가 <b>어떤 액터(플레이어·몬스터)가 어빌리티를 발동했다</b>는 S_AbilityActivated 를
    /// 브로드캐스트하면, 그 신호(OnAbilityActivated)를 발행한다. AbilityCueRouter 가 ActorId 로 대상을 찾아 Cue 를 재생한다.
    ///
    /// <b>연출 전용</b> — 적중·데미지는 서버 권위(S_ApplyEffect)로 별도 반영. 이 패킷으로 로컬 판정하지 않는다.
    /// </summary>
    public class AbilityActivatedPacketHandler : PacketHandlerBase<S_AbilityActivated>
    {
        private readonly ISocketPacketState _state;

        public AbilityActivatedPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_AbilityActivated packet)
        {
            // 진단(AC-C1b): 서버가 발동 게이트를 통과시켰다는 신호의 도착 시각. 이게 안 오면 gate 에 막힌 것(LikelyGated).
            Diagnostics.CombatTraceRecorder.Shared.RecordAbilityActivated(
                Diagnostics.CombatTraceRecorder.NowMs, packet.ActorId, packet.SkillId);

            _state.NotifyAbilityActivated(packet.ActorId, packet.SkillId);
            return UniTask.CompletedTask;
        }
    }
}
