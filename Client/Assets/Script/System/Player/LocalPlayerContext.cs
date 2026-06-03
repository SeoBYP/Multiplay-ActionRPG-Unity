using System;
using Script.System.GamePlayAbilitySystem;

namespace Game.System.Player
{
    /// <summary>
    /// 로컬 플레이어의 게임플레이 진입점(현재는 ASC)을 보관하는 scope 단일 인스턴스.
    ///
    /// 생산자: CharacterSpawner(Game.Gameplay)가 로컬 캐릭터 스폰 후 Set.
    /// 소비자: InGameModel(Game.Presentation)이 OnSet 구독 → 스탯 변화를 HUD로 중계.
    ///
    /// Gameplay와 Presentation은 서로 참조하지 않으므로(형제 레이어),
    /// 둘의 공통 하위 레이어인 Game.System에 둔다.
    /// </summary>
    public sealed class LocalPlayerContext
    {
        public AbilitySystemComponent AbilitySystem { get; private set; }

        /// <summary>로컬 ASC가 준비되면 발행.</summary>
        public event Action<AbilitySystemComponent> OnSet;

        public void Set(AbilitySystemComponent abilitySystem)
        {
            AbilitySystem = abilitySystem;
            OnSet?.Invoke(abilitySystem);
        }

        public void Clear()
        {
            AbilitySystem = null;
        }
    }
}
