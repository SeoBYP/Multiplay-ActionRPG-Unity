using System;
using Game.Gameplay.Abilities;
using Game.Network.Socket;
using VContainer.Unity;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 서버 발동 신호(S_AbilityActivated)를 <see cref="ActorRegistry"/> 로 라우팅해 해당 액터의 연출을 재생하는 단일 구독자.
    /// 각 엔티티가 자기 것인지 필터하며 구독하던 것을 대체 — ActorId 로 O(1) 조회 후 IActorView 위임(actor-combat-architecture §5.3).
    ///
    /// <b>networkId→Cue 해석의 유일한 지점</b>(AC-B B3): 어빌리티 카탈로그에서 cueTrigger/cueComboStep 을 읽어
    /// 뷰에 넘긴다 → 뷰·드라이버의 하드코딩 콤보 switch 가 사라지고, 기획자가 SO 에서 연출을 편집한다.
    ///
    /// 던전(네트워크) 전용. Main(솔로)은 로컬 발동(LocalMonster)이라 패킷 경로가 없다.
    /// </summary>
    public sealed class AbilityCueRouter : IInitializable, IDisposable
    {
        private readonly ISocketPacketState _state;
        private readonly ActorRegistry _actors;
        private readonly AbilityCatalogProvider _abilities;

        public AbilityCueRouter(ISocketPacketState state, ActorRegistry actors, AbilityCatalogProvider abilities)
        {
            _state = state;
            _actors = actors;
            _abilities = abilities;
        }

        public void Initialize() => _state.OnAbilityActivated += Route;

        public void Dispose() => _state.OnAbilityActivated -= Route;

        /// <summary>ActorId 로 뷰를 찾아 발동 Cue 재생. 미등록 액터(아직 스폰 전/이미 디스폰)면 조용히 무시.</summary>
        private void Route(long actorId, int skillId)
        {
            if (!_actors.TryGet(actorId, out var view)) return;

            // 카탈로그에서 연출 조회(데이터 주도). 미등록 networkId(예: 아직 어빌리티화 전인 몬스터 주공격=0)는
            // 기본 공격 연출로 폴백 — 신호는 왔는데 애니가 안 나가는 것보다 낫다. (몬스터 어빌리티화 = B4)
            var ability = _abilities?.Get(skillId);
            var trigger = ability?.cueTrigger ?? AnimationTriggerType.Attack;
            int comboStep = ability?.cueComboStep ?? 0;

            view.PlayAbilityCue(trigger, comboStep);
        }
    }
}
