namespace Game.Gameplay.Character
{
    /// <summary>
    /// ActorId 로 지칭 가능한 화면상 액터(원격 플레이어·몬스터)의 <b>연출 재생 계약</b>.
    /// ActorRegistry 가 ActorId→IActorView 로 보관하고, 발동 신호(S_AbilityActivated)를 이 인터페이스로 위임한다.
    /// 구현체: <see cref="MonsterEntity"/>(던전 몬스터) · <see cref="RemoteDriver"/>(원격 플레이어) · <see cref="LocalMonster"/>(Main 몬스터).
    ///
    /// <b>뷰는 어빌리티 카탈로그를 모른다</b> — networkId→Cue 해석은 호출자(AbilityCueRouter / LocalMonster AI)가 하고,
    /// 뷰는 "이 트리거를 이 콤보단계로 재생하라"만 받는다(AC-B B3). 실제 Animator 파라미터 *이름* 은
    /// 프리팹의 CharacterAgentAnimations 가 갖는다 → 컨트롤러가 제각각인 몬스터도 같은 어빌리티를 공유(codemap §2.64).
    ///
    /// 지금은 애니 트리거만. VFX/SFX 는 이 뒤에 plug-in (확장점).
    /// </summary>
    public interface IActorView
    {
        /// <summary>어빌리티 발동 연출 재생. trigger=재생할 트리거의 의미, comboStep=ComboStep 파라미터 값(콤보 미사용=0).</summary>
        void PlayAbilityCue(AnimationTriggerType trigger, int comboStep);
    }
}
