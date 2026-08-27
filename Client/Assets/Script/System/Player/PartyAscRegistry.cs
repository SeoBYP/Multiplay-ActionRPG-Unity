using System;
using System.Collections.Generic;
using Script.System.GamePlayAbilitySystem;

namespace Game.System.Player
{
    /// <summary>
    /// 파티원(로컬+원격) UserId → ASC 레지스트리. `LocalPlayerContext`(로컬 전용)의 확장 — 파티 HP HUD 가
    /// 각 플레이어 HP 를 GAS 로 읽고, `EffectReceiver` 가 서버 S_ApplyEffect 를 TargetId 로 라우팅하는 데 쓴다.
    ///
    /// 생산자: CharacterSpawner(Game.Gameplay) — 로컬/원격 스폰 시 Register, 디스폰 시 Unregister.
    /// 소비자: EffectReceiver(Game.Presentation, 타겟 라우팅) · PartyModel(파티 리스트 집계).
    /// Gameplay↔Presentation 형제라 공통 하위 Game.System 에 둔다(LocalPlayerContext 와 동일 위치).
    ///
    /// HP 진실원 = 서버 권위(S_ApplyEffect). 원격 MaxHp 는 서버 동기화가 없어 prefab 기본값을 쓴다(근사).
    /// </summary>
    public sealed class PartyAscRegistry
    {
        private readonly Dictionary<long, GasComponent> _byUserId = new();

        /// <summary>등록/해제 시 발행(파티 구성 변경). PartyModel 이 구독해 리스트를 갱신한다.</summary>
        public event Action Changed;

        public IReadOnlyDictionary<long, GasComponent> Entries => _byUserId;

        public void Register(long userId, GasComponent asc)
        {
            if (userId <= 0 || asc == null) return;
            _byUserId[userId] = asc;
            Changed?.Invoke();
        }

        public void Unregister(long userId)
        {
            if (_byUserId.Remove(userId))
                Changed?.Invoke();
        }

        public bool TryGet(long userId, out GasComponent asc) => _byUserId.TryGetValue(userId, out asc);

        public void Clear()
        {
            _byUserId.Clear();
            Changed?.Invoke();
        }
    }
}
