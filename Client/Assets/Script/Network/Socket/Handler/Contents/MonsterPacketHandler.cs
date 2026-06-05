using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// S_SpawnMonster 수신 → 상태 저장소에 몬스터 추가(이벤트 발행). MonsterSpawner가 엔티티를 스폰한다.
    /// (네트워크 레이어는 프리팹/씬을 모른다 — 플레이어 동기화와 동일한 state 경유 패턴)
    /// </summary>
    public class SpawnMonsterPacketHandler : PacketHandlerBase<S_SpawnMonster>
    {
        private readonly ISocketPacketState _state;

        public SpawnMonsterPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_SpawnMonster packet)
        {
            _state.AddMonster(new SocketMonsterSnapshot(
                packet.InstanceId,
                packet.MonsterId,
                packet.PosX, packet.PosY, packet.PosZ,
                packet.RotY,
                packet.Hp, packet.MaxHp,
                phase: 0)); // 스폰 시 페이즈 미포함 → Idle(0). 이후 S_MonsterState로 갱신.
            return UniTask.CompletedTask;
        }
    }

    /// <summary>S_MonsterState 수신(이동/HP/페이즈) → 상태 저장소 갱신 + OnMonsterMoved 발행(MonsterEntity 보간).</summary>
    public class MonsterStatePacketHandler : PacketHandlerBase<S_MonsterState>
    {
        private readonly ISocketPacketState _state;

        public MonsterStatePacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_MonsterState packet)
        {
            _state.UpdateMonster(
                packet.InstanceId,
                packet.PosX, packet.PosY, packet.PosZ,
                packet.RotY,
                packet.Hp,
                packet.Phase);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>S_MonsterDead 수신 → 상태 저장소에서 제거 + OnMonsterDead 발행(MonsterSpawner 디스폰).</summary>
    public class MonsterDeadPacketHandler : PacketHandlerBase<S_MonsterDead>
    {
        private readonly ISocketPacketState _state;

        public MonsterDeadPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_MonsterDead packet)
        {
            _state.RemoveMonster(packet.InstanceId);
            return UniTask.CompletedTask;
        }
    }
}
