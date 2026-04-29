namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// GAS에서 실행 가능한 행동의 기본 단위다.
    /// AbilitySystemComponent가 Ability를 소유하고, 외부 입력이나 이벤트가 TryActivate를 호출한다.
    /// </summary>
    public abstract class Ability
    {
        public AbilitySystemComponent Owner { get; private set; }

        // Ability는 반드시 ASC를 통해 부여된다. 여기서 Owner가 결정된다.
        internal void GrantTo(AbilitySystemComponent owner)
        {
            Owner = owner;
            OnGranted();
        }

        // 모든 Ability 활성화는 CanActivate 검사를 통과한 뒤 실제 Activate로 들어간다.
        public bool TryActivate(AbilityActivationContext context)
        {
            if (!CanActivate(context))
                return false;

            Activate(context);
            return true;
        }

        protected virtual void OnGranted()
        {
        }

        // 공통 활성화 조건. 하위 Ability는 비용, 쿨다운, 대상 유효성 등을 여기에 추가한다.
        protected virtual bool CanActivate(AbilityActivationContext context)
        {
            return Owner != null;
        }

        protected abstract void Activate(AbilityActivationContext context);
    }
}
