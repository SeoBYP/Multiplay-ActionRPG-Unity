using System.Collections.Generic;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// ActorId → <see cref="IActorView"/> 조회 레지스트리(방 스코프, 단일 인스턴스).
    /// 전투 상호작용 패킷(발동·효과)이 실은 종족을 몰라도 되게 하는 통합 라우팅층 — actor-combat-architecture §2·§5.3.
    ///
    /// 이득: 이벤트를 전체 브로드캐스트하고 각 엔티티가 자기 것인지 필터하던 O(N²) 대신, ActorId 로 O(1) 직접 조회.
    /// 등록/해제는 스포너(CharacterSpawner/MonsterSpawner)의 스폰/디스폰이 담당한다(수명 = 엔티티 수명).
    /// </summary>
    public sealed class ActorRegistry
    {
        private readonly Dictionary<long, IActorView> _actors = new Dictionary<long, IActorView>();

        /// <summary>ActorId 로 뷰를 등록(스폰 시). 같은 ActorId 재등록은 덮어쓴다(재스폰 안전).</summary>
        public void Register(long actorId, IActorView view)
        {
            if (view != null)
                _actors[actorId] = view;
        }

        /// <summary>등록 해제(디스폰 시). 미등록 ActorId 는 무시.</summary>
        public void Unregister(long actorId) => _actors.Remove(actorId);

        /// <summary>ActorId 의 뷰 조회. 미등록이면 false.</summary>
        public bool TryGet(long actorId, out IActorView view) => _actors.TryGetValue(actorId, out view);

        /// <summary>씬/스코프 종료 시 일괄 정리.</summary>
        public void Clear() => _actors.Clear();

        /// <summary>현재 등록 수(디버그·테스트).</summary>
        public int Count => _actors.Count;
    }
}
