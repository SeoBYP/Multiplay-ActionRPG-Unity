using System;
using System.Collections.Generic;
using Game.Gameplay.Abilities;
using Game.System.Player;
using Game.System.Progression;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// Main(싱글) 전용 클라 권위 몬스터. 던전의 MonsterEntity(서버 권위·보간 전용)와 달리
    /// HP·간단 AI·사망 판정을 클라가 로컬로 소유한다(loot-drop.md §1.5). 싱글이라 동기화·경쟁이 없어 가능.
    ///
    /// - AI: LocalPlayerContext 의 플레이어가 chaseRange 안이면 추격, 밖이면 제자리(Idle). (간단 — Patrol/Attack 없음)
    /// - 전투: LocalCombat 가 HitboxMath 로 적중 판정 후 TakeDamage 호출 → HP≤0 → Die.
    /// - 사망: OnDied(this) 발행(MainMonsterSpawner 가 디스폰·드랍 처리) 후 자기 파괴.
    /// 콜라이더 필요(LocalCombat 의 Physics.OverlapSphere 가 찾는다). UnityEngine 의존이므로 서버와 무관.
    /// </summary>
    public sealed class LocalMonster : MonoBehaviour, IActorView, IMonsterHealth
    {
        [Header("식별")]
        [Tooltip("드랍 테이블/카탈로그 키(예: creepy_demon, goblin). DropTableDefinition 과 정렬.")]
        [SerializeField] private string monsterId = "creepy_demon";

        [Header("전투")]
        [SerializeField] private int maxHp = 40;
        [Tooltip("HitboxMath 적중 판정용 타겟 반경(구).")]
        [SerializeField] private float targetRadius = 0.5f;
        [Tooltip("이 몬스터 공격의 GameplayAbility id(발동 로그·식별). 비우면 '{monsterId}_attack' 자동.")]
        [SerializeField] private string attackAbilityId = "creepy_demon_attack";

        [Header("간단 AI")]
        [SerializeField] private float chaseRange = 6f;
        [SerializeField] private float moveSpeed = 2f;

        [Header("플레이어 공격(Main 사망 트리거)")]
        [Tooltip("이 거리 안이면 추격을 멈추고 공격한다.")]
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private int attackDamage = 10;
        [SerializeField] private float attackCooldownSec = 1.5f;

        [Header("상태이상(CC) — 적중 시 부여")]
        [Tooltip("적중 시 부여할 CC 효과 id(GameplayEffectCatalog). 비우면 없음. 예: slow_3s · stun_1_5s")]
        [SerializeField] private string onHitCcId = "";

        [Header("넉백 — 적중 시 밀어냄(테스트용, 추후 Ability 융합)")]
        [SerializeField] private bool knockbackOnHit = false;
        [SerializeField] private float knockbackDistance = 2f;
        [SerializeField] private float knockbackDuration = 0.2f;

        [Header("애니(Animator 파라미터 구동 — 이름 배선은 CharacterAgentAnimations)")]
        [Tooltip("Speed 파라미터 평활화 계수. 지터가 Idle/Walk 전이를 떨게 하는 것을 막는다.")]
        [SerializeField] private float speedSmoothing = 10f;
        [Tooltip("die 애니 재생 후 파괴까지 지연(초).")]
        [SerializeField] private float deathDespawnDelay = 2.0f;

        [Tooltip("연출용 — 이 몬스터의 주공격 어빌리티(SFX/VFX 타임라인 저작처). 지정하면 공격 시 cueEvents 를 재생한다.\n" +
                 "Main 은 라우터가 없어 AI 가 직접 넘긴다(던전 몬스터는 라우터가 카탈로그에서 해석). 미지정이면 애니만.")]
        [SerializeField] private AbilityDefinition attackAbility;

        [Inject] private readonly LocalPlayerContext _localPlayer = null;
        // CC 효과 정의 조회(Main 클라 권위). 던전은 서버 S_ApplyEffect 경로. 미주입 시 CC 미적용(데미지만).
        [Inject] private readonly GameplayEffectCatalog _effectCatalog = null;
        // Main 클라 스탯 캐시(서버 권위 GetProgression). 플레이어 Defense 를 공격 데미지에서 차감하는 데 쓴다.
        // 던전(서버권위)의 Room.TickMonsters Defense 반영과 동일 산식의 Main(클라권위) 대응판. 미주입 시 Defense=0 폴백.
        [Inject] private readonly PlayerProgressionHolder _progression = null;

        private int _hp;

        // ── IMonsterHealth (체력바 표시 계약) ──
        // Main 은 HP 권위가 **클라 로컬**이다(던전은 서버). 체력바는 그 차이를 몰라도 되게 계약만 노출한다.
        public int Hp => _hp;
        public int MaxHp => maxHp;

        /// <summary>HP 변경 시 발행(초기 seed·사망 0 확정 포함). <see cref="MonsterHealthBar"/> 가 구독.</summary>
        public event Action<IMonsterHealth> HpChanged;
        private long _lastAttackMs;      // 발동 게이트(AbilityActivationMath)용 마지막 공격 시각(ms)
        private Vector3 _prevPos;
        private CharacterAgentAnimations _animations;
        private AbilityCuePlayer _cuePlayer;
        private float _animSpeed;

        /// <summary>사망 시 발행(자기 자신 전달). MainMonsterSpawner 가 디스폰·드랍(B-lite 클레임)에 사용.</summary>
        public event Action<LocalMonster> OnDied;

        public string MonsterId => monsterId;
        public float TargetRadius => targetRadius;
        public bool IsDead { get; private set; }

        /// <summary>이 몬스터 공격의 GameplayAbility id. 인스펙터 미설정 시 '{monsterId}_attack' 규약.</summary>
        private string AttackAbilityId => string.IsNullOrEmpty(attackAbilityId) ? $"{monsterId}_attack" : attackAbilityId;

        /// <summary>스폰 레이아웃 슬롯(B-lite). 줍기 시 이 슬롯으로 ClaimKill → 서버 검증·roll. 0=미설정.</summary>
        public int SlotId { get; private set; }

        /// <summary>이 몬스터가 속한 맵 id(ClaimKill 키). MainMonsterSpawner 가 슬롯 스폰 시 주입.</summary>
        public string MapId { get; private set; } = string.Empty;

        /// <summary>슬롯 기반 스폰 시 식별자 주입(spawn-layouts 슬롯). 직렬화 기본값을 덮어쓴다.</summary>
        public void Configure(string monsterId, string mapId, int slotId)
        {
            this.monsterId = monsterId;
            MapId = mapId;
            SlotId = slotId;
        }

        private void Awake()
        {
            _hp = maxHp;
            _animations = GetComponent<CharacterAgentAnimations>();
            _cuePlayer = GetComponent<AbilityCuePlayer>();
            _prevPos = transform.position;
            HpChanged?.Invoke(this); // 체력바 최초 표시(구독 전이면 Start 의 seed 가 받는다)
        }

        private void Update()
        {
            if (IsDead) return;

            // 지난 프레임 대비 실제 이동 변위 → walk/idle (추격 중=walk, 정지/공격=idle).
            var moved = transform.position - _prevPos; moved.y = 0f;
            float instant = Time.deltaTime > 0f ? moved.magnitude / Time.deltaTime : 0f;
            _prevPos = transform.position;
            // 실제 이동 속도 → Speed 파라미터. 컨트롤러가 임계로 Idle↔Walk 를 전이한다(상태를 직접 고르지 않는다).
            if (_animations != null && Time.deltaTime > 0f)
            {
                _animSpeed = Mathf.Lerp(_animSpeed, instant, Time.deltaTime * speedSmoothing);
                if (_animSpeed < 0.01f) _animSpeed = 0f;
                _animations.SetFloat(AnimationFloatType.Speed, _animSpeed);
            }

            var asc = _localPlayer?.AbilitySystem;
            if (asc == null) return;

            // 다운(사망)한 플레이어는 공격/추격하지 않는다(던전 _downed 제외와 동일 취지).
            if (asc.HasTag(GameplayTags.Dead)) return;

            var to = asc.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > chaseRange) return; // 범위 밖 = 정지

            var dir = dist > 0.01f ? to / dist : transform.forward;
            transform.rotation = Quaternion.LookRotation(dir);

            if (dist <= attackRange)
            {
                TryAttack(asc); // 사거리 내 = 멈추고 공격
                return;
            }

            transform.position += dir * (moveSpeed * Time.deltaTime); // 사거리 밖 = 추격
        }

        /// <summary>쿨다운마다 플레이어 ASC 에 즉발 피해(로컬 권위). HP≤0 → PlayerCharacterAgent 가 다운 처리.</summary>
        private void TryAttack(AbilitySystemComponent target)
        {
            // 발동 게이트 = AbilityActivationMath(던전 몬스터·플레이어와 동일 Shared 규칙). Main 은 마나·차단태그 없어 쿨다운만 먹인다.
            long nowMs = (long)(Time.time * 1000f);
            if (!AbilityActivationMath.CanActivate(nowMs, _lastAttackMs, (int)(attackCooldownSec * 1000f),
                    manaCost: 0, currentMana: 0, blocked: false))
                return;
            _lastAttackMs = nowMs;

            // AC: 발동 = 스윙 애니(헛스윙 포함). i-frame 판정보다 먼저 재생한다(던전 MonsterEntity 와 동일).
            // 몬스터 주공격은 아직 카탈로그 밖(어빌리티화 = B4) → 기본 공격 Cue 고정.
            PlayAbilityCue(AnimationTriggerType.Attack, comboStep: 0);
            if (attackAbility != null) PlayAbilityCues(attackAbility); // SFX/VFX 타임라인(지정 시). Main 은 라우터 없어 AI 가 직접.

            // 회피 무적(i-frame): 무적 중이면 이 공격은 빗나간다(쿨다운은 소모 = 헛스윙). Main 클라 권위 게이트.
            // 던전(서버 권위)은 서버 TickMonsters 가 동일하게 막는다(authority-model 정합).
            if (target.HasTag(GameplayTags.Invulnerable)) return;

            // 데미지 = attackDamage − 플레이어 Defense (던전 Room.TickMonsters 와 동일 Shared 산식, 최소1).
            // 홀더 미주입/미갱신 시 Defense=0 → attackDamage 그대로(하위호환).
            int dmg = StatCombatMath.MeleeDamage(attackDamage, 0, _progression?.Defense ?? 0);
            target.ApplyEffect(BuildAttackEffect(dmg));

            // CC 부여(Main 클라 권위) — 던전의 서버 S_ApplyEffect 경로와 동일 효과(같은 카탈로그 id).
            if (!string.IsNullOrEmpty(onHitCcId) && _effectCatalog != null)
            {
                var cc = _effectCatalog.Get(onHitCcId);
                if (cc != null) target.ApplyEffect(cc);
            }

            // 넉백(테스트 배선) — 플레이어를 몬스터 반대 방향으로 밀어낸다. 추후 Ability 가 ApplyKnockback 으로 대체.
            if (knockbackOnHit)
            {
                var agent = target.GetComponent<PlayerCharacterAgent>();
                if (agent != null) agent.ApplyKnockback(transform.position, knockbackDistance, knockbackDuration);
            }

            Debug.Log($"[GameplayAbility] {monsterId} 발동: '{AttackAbilityId}' → dmg={dmg} (attackDamage {attackDamage} − Defense {_progression?.Defense ?? 0}){(string.IsNullOrEmpty(onHitCcId) ? "" : $" +CC {onHitCcId}")}");
        }

        /// <summary>즉발 Health 피해 effect 생성(클라 로컬 권위, Main 솔로). 던전 플레이어 피해는 서버 권위라 무관.</summary>
        private static GameplayEffectDefinition BuildAttackEffect(int damage)
            => new GameplayEffectDefinition(
                id: "local_monster_attack",
                category: EEffectCategory.AttackPower,
                policy: EDurationPolicy.Instant,
                durationMs: 0,
                modifiers: new List<GameplayAttributeModifier>
                {
                    GameplayAttributeModifier.Create(EGameplayAttribute.Health, -damage, EModifierType.Additive),
                });

        /// <summary>로컬 피격. HP 차감 후 0 이하면 사망 처리(1회).</summary>
        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return;

            int before = _hp;
            _hp -= amount;
            Debug.Log($"[Combat] {monsterId} 피격 dmg={amount} HP {before}→{Mathf.Max(0, _hp)}/{maxHp}");
            HpChanged?.Invoke(this);
            if (_hp > 0) return;

            // 사망 = HP 0 확정. 음수로 두면 체력바가 Clamp01 로 0 을 그리긴 하나, 계약(Hp)이 음수를 노출하면
            // 다른 소비자가 오독한다. 던전(MonsterEntity.HandleDead)과 같은 규칙.
            _hp = 0;
            HpChanged?.Invoke(this);

            IsDead = true;
            Debug.Log($"[Combat] {monsterId} 사망");
            _animations?.SetTrigger(AnimationTriggerType.Dead);
            // 사망 애니 중 재타격/재타겟 방지(HP guard 로 이미 no-op 이나 명시적으로 콜라이더 끔).
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            OnDied?.Invoke(this);                    // 스포너: 드랍·재스폰(비주얼과 독립, 즉시)
            Destroy(gameObject, deathDespawnDelay);  // die 애니 후 자체 파괴
        }

        /// <summary>
        /// 발동 연출(IActorView) — 로컬 권위. AI 의 TryAttack 이 발동 시 직접 호출한다(Main 은 네트워크·라우터 없음).
        /// 컨트롤러의 트리거 전이가 스윙 재생·복귀를 담당한다. 사망/미배선이면 조용히 무시.
        /// ※ 몬스터 어빌리티가 카탈로그에 오르면(B4) TryAttack 이 카탈로그에서 Cue 를 해석해 넘긴다 — 지금은 주공격 고정.
        /// </summary>
        public void PlayAbilityCue(AnimationTriggerType trigger, int comboStep)
        {
            if (IsDead) return;
            _animations?.SetInt(AnimationIntType.ComboStep, comboStep);
            _animations?.SetTrigger(trigger);
        }

        /// <summary>연출 타임라인(SFX/VFX) 재생 — Main 은 AI(TryAttack)가 attackAbility 를 직접 넘긴다. 사망/미부착이면 무시(IActorView).</summary>
        public void PlayAbilityCues(AbilityDefinition ability)
        {
            if (IsDead) return;
            _cuePlayer?.Play(ability);
        }
    }
}
