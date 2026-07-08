using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Abilities;
using Game.Gameplay.Camera;
using Game.Gameplay.Character.Input;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Character
{
    public class PlayerCharacterAgent : CharacterAgent
    {
        // 사망 상태 태그(GAS). HP 0 시 세우고, 입력/이동 게이트가 폴링한다. 던전=다운-잠금(씬 복귀 전까지 유지).
        private static readonly GameplayTag DeadTag = GameplayTags.Dead;
        // 스턴(CC) 태그. Duration 효과가 부여→자동 만료. 켜진 동안 입력/이동 정지(사망과 달리 시간 후 자동 해제).
        private static readonly GameplayTag StunTag = GameplayTags.Stun;

        [Header("락온(2.6.3)")]
        [Tooltip("락온 가능 최대 평면 사거리(m). 화면 안 + 이 거리 내 대상만 잡힌다.")]
        [SerializeField] private float lockOnRange = 15f;

        private InteractionDetector _interactionDetector;
        private DodgeDriver _dodge;
        private KnockbackDriver _knockback;
        private LockOnDriver _lockOn;

        // 스킬 데이터(쿨다운·마나) 조회 — 클라 예측용. DI 미주입(테스트) 시 null → 게이트 없음.
        private SkillCatalogProvider _skills;
        private readonly Dictionary<int, float> _lastCastTime = new();

        // 마나 리젠 소수부 누적(프레임 dt 비례 회복을 정수 Mana 로 환산). 서버 _manaRegenAccum 과 동일 방식.
        private float _manaRegenAccum;

        /// <summary>skillId(int) → 스킬 데이터 키. 서버 CombatHandler.ResolveSkill 동일 규약(0=basic·1=heavy).</summary>
        private static string SkillName(int skillId) => skillId switch { 1 => "heavy_swing", _ => "basic_swing" };

        [Inject]
        public void ConstructAbilities(SkillCatalogProvider skills) => _skills = skills;

        /// <summary>공격 입력으로 스윙이 발동될 때 발행(인자=skillId: 0=기본/좌클릭, 1=강공격/우클릭).
        /// 던전 `CombatSyncSender`가 C_Attack{SkillId} 송신, Main `LocalCombat`가 그 스킬 hitbox 로 판정.</summary>
        public event Action<int> OnAttackPerformed;

        /// <summary>회피가 발동될 때 발행. 던전 전용 `DodgeSyncSender`가 구독해 C_Dodge(서버 무적창)를 송신한다.</summary>
        public event Action OnDodgePerformed;

        /// <summary>로컬 플레이어가 사망(State.Dead)했는지. 입력·이동 게이트의 단일 판정.</summary>
        private bool IsDead => AbilitySystem != null && AbilitySystem.HasTag(DeadTag);

        /// <summary>스턴(State.Stun) 상태인지. 입력·이동 게이트가 폴링(자동 만료되면 해제).</summary>
        private bool IsStunned => AbilitySystem != null && AbilitySystem.HasTag(StunTag);

        protected override void Awake()
        {
            base.Awake();
            _interactionDetector = this.GetAroundComponent<InteractionDetector>();

            // 자기 HP 를 관찰해 0 이하가 되면 State.Dead 를 세운다(클라 결정론 HP 기준).
            if (AbilitySystem != null)
                AbilitySystem.OnAttributeChanged += OnAttributeChanged;

            Context = new CharacterStateContext
            {
                Motor                = Motor,
                GroundDetector       = GroundDetector,
                Animations           = AgentAnimations,
                InputSource          = InputSource,
                AbilitySystem        = AbilitySystem,
                LocomotionSettings   = settings,
            };

            _dodge = new DodgeDriver(Motor, AbilitySystem, AgentAnimations, settings);
            _knockback = new KnockbackDriver(Motor);

            // 락온 = 순수 클라 조준 보조. 카메라 Follow(피벗 회전 소유)와 Motor(facing) 에 매 프레임 push.
            var cameraFollow = this.GetAroundComponent<CharacterCameraFollow>();
            _lockOn = new LockOnDriver(transform, Motor, cameraFollow, lockOnRange);
        }

        /// <summary>
        /// 외부(몬스터 공격·스킬/Ability)가 호출하는 넉백 임펄스 진입점.
        /// 방향 = 공격자→피격자(밀려나는 방향), 거리/시간 동안 Motor 로 강제 변위(회전 없음).
        /// 지금은 테스트/몬스터 배선용 — 추후 GameplayEffect/Ability 가 이 API 로 융합한다.
        /// </summary>
        public void ApplyKnockback(Vector3 sourcePosition, float distance, float duration)
        {
            if (IsDead) return;

            Vector3 dir = transform.position - sourcePosition;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = -transform.forward; // 같은 위치면 뒤로 민다

            _dodge?.Cancel(); // 회피 중이면 끊고 넉백이 이동을 전담
            _knockback.Begin(dir, distance, duration);
        }

        protected override void Update()
        {
            // Action 이동잠금(Rooted) 만료 — 공격/줍기 지속 경과 시 자동 해제(사망/스턴 게이트보다 먼저 처리해 확실히 만료).
            if (_rootedUntil > 0f && Time.time >= _rootedUntil)
            {
                AbilitySystem?.RemoveTag(ActionTags.Rooted);
                _rootedUntil = 0f;
            }

            // 사망(다운) 시 두 축 모두 게이트: Action(공격/상호작용) 무시 + base.Update() 미호출로
            // Locomotion(이동) 정지. 던전 내 부활(2.5.2) 또는 씬 복귀 전까지 다운-잠금 유지.
            if (IsDead)
            {
                _lockOn?.ForceUnlock(); // 다운되면 락 해제(facing/카메라 원복)
                return;
            }

            // 마나 자연 회복(클라 예측). 서버 RegenMana 와 동일 rate → 만마에서 수렴. 스턴/회피 중에도 진행.
            RegenMana(Time.deltaTime);

            // 넉백(강제 변위): 스턴보다 우선 — 맞아서 밀려나는 중엔 stun 이어도 임펄스가 이동을 전담한다.
            if (_knockback.IsActive)
            {
                _knockback.Tick(Time.deltaTime);
                return;
            }

            // 스턴(CC): 입력/이동 정지. 사망과 달리 Duration 효과가 ASC.Tick 으로 자동 만료→해제.
            // 진행 중이던 회피는 끊는다(무적 태그도 정리) — 스턴에 맞아 회피가 깨진 것.
            if (IsStunned)
            {
                if (_dodge.IsActive) _dodge.Cancel();
                // base.Update() 미호출로 Motor 이동은 이미 멈춤. 애니 캐릭터의 걷기 클립/루트모션도 정지시킨다.
                AgentAnimations?.SetFloat(AnimationFloatType.Speed, 0f);
                return;
            }

            // 회피 중에는 두 축을 게이트 — 대시가 이동을 전담하고 Action(공격/상호작용)·Locomotion FSM 은 멈춘다.
            // (회피 자체는 FSM 상태가 아니라 Action 축 임펄스 — CA-1.)
            if (_dodge.IsActive)
            {
                _dodge.Tick(Time.deltaTime);
                return;
            }

            _interactionDetector?.DetectInteractable();
            if (HandleDodgeInput()) // 회피 시작 프레임엔 다른 Action/Locomotion 을 스킵(대시가 전담).
                return;
            HandleLockOnInput();
            HandleAttackInput();
            HandleHeavyAttackInput();
            HandleInteractInput();
            // facing 강제(타겟 향하기)를 base.Update()→Motor.Move 전에 적용. 유효성 이탈 시 자동 해제.
            _lockOn?.Tick();
            base.Update();
        }

        /// <summary>HP(서버 권위/클라 결정론)가 0 이하가 되면 State.Dead 를 1회 세우고 다운 포즈를 재생한다.</summary>
        private void OnAttributeChanged(EGameplayAttribute type, int current, int max)
        {
            if (type == EGameplayAttribute.Health && current <= 0 && AbilitySystem != null && !IsDead)
            {
                AbilitySystem.AddTag(DeadTag);
                AgentAnimations?.SetTrigger(AnimationTriggerType.Dead); // 다운 포즈(Animator "Dead" 클립 배선은 클라 발전 시).
                Debug.Log("[PlayerCharacterAgent] 로컬 다운 — HP≤0 → State.Dead (입력 게이트). ※다운 애니는 미배선(로그 대체)");
            }
        }

        /// <summary>
        /// 로컬 부활(Main 타이머 리스폰 전용 — 던전은 다운잠금이라 호출되지 않는다).
        /// State.Dead 해제 + HP 만피 복구 + 다운 트리거 리셋 + 스폰 지점 텔레포트. 게이트가 풀려 이동·Action 재개.
        /// </summary>
        public void Revive(Vector3 spawnPos)
        {
            if (AbilitySystem == null) return;

            _dodge?.Cancel(); // 진행 중이던 회피 무적/대시 정리
            _knockback?.Cancel();
            AbilitySystem.RemoveTag(DeadTag);
            var hp = AbilitySystem.GetAttribute(EGameplayAttribute.Health);
            hp?.SetCurrent(hp.MaxValue);

            AgentAnimations?.ResetTrigger(AnimationTriggerType.Dead);

            // CharacterController 텔레포트 — 비활성화 후 위치 설정해야 내부 위치와 어긋나지 않는다.
            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                transform.position = spawnPos;
                cc.enabled = true;
            }
            else
            {
                transform.position = spawnPos;
            }

            Debug.Log($"[PlayerCharacterAgent] 부활 — State.Dead 해제, HP 만피, 스폰 {spawnPos} 텔레포트. ※부활 애니는 미배선(로그 대체)");
        }

        /// <summary>
        /// Co-op 부활(2.5.2, 던전) — 다른 플레이어가 살려줄 때 서버 S_PlayerRevived 로 호출.
        /// State.Dead 해제 + 서버 권위 HP 적용(부분복구) + 다운 트리거 리셋. <b>텔레포트 없음</b>(제자리 부활).
        /// </summary>
        public void ReviveInPlace(int hp)
        {
            if (AbilitySystem == null) return;

            _dodge?.Cancel();
            _knockback?.Cancel();
            AbilitySystem.RemoveTag(DeadTag);
            var health = AbilitySystem.GetAttribute(EGameplayAttribute.Health);
            health?.SetCurrent(hp);
            AgentAnimations?.ResetTrigger(AnimationTriggerType.Dead);

            Debug.Log($"[PlayerCharacterAgent] Co-op 부활 — State.Dead 해제, HP={hp} (제자리). ※부활 애니 미배선(로그)");
        }

        private void OnDestroy()
        {
            if (AbilitySystem != null)
                AbilitySystem.OnAttributeChanged -= OnAttributeChanged;
        }

        /// <summary>
        /// 공격 = Action 축. 입력→스윙 애니 + OnAttackPerformed 발행.
        /// 적중 판정·데미지는 **서버 권위**(CombatSyncSender가 C_Attack 송신 → 서버 HitboxMath → S_ApplyEffect).
        /// 로컬은 연출만(피격 HitStop은 HitStopController가 HP 감소로 자동 트리거).
        /// </summary>
        private void HandleAttackInput()
        {
            if (InputSource == null || !InputSource.ConsumeAttackPressed())
                return;
            FireSkill(0); // 기본 공격(basic_swing)
        }

        /// <summary>락온 토글(Q) = Action 축 조준 보조. 토글 시 화면 중앙 대상 획득/해제(LockOnDriver 가 소유).</summary>
        private void HandleLockOnInput()
        {
            if (InputSource == null || !InputSource.ConsumeLockOnPressed())
                return;
            _lockOn?.Toggle();
        }

        /// <summary>강공격(우클릭) = skillId 1(heavy_swing).</summary>
        private void HandleHeavyAttackInput()
        {
            if (InputSource == null || !InputSource.ConsumeHeavyAttackPressed())
                return;
            FireSkill(1); // 강공격(heavy_swing)
        }

        /// <summary>스킬 발동 — 클라 마나+쿨다운 예측 게이트 통과 시 애니+OnAttackPerformed. 서버 게이트가 최종 권위.</summary>
        private void FireSkill(int skillId)
        {
            var skill = _skills?.Get(SkillName(skillId));
            int manaCost = skill?.ManaCost ?? 0;
            if (!HasMana(manaCost))            // 마나 부족 → 발동/송신 안 함(서버도 동일 거부)
                return;
            if (!SkillCooldownReady(skillId))  // 쿨다운(통과 시 발동시각 커밋)
                return;
            SpendMana(manaCost);               // 예측 차감 — 서버가 S_PlayerMana 로 정정

            // 발동한 GameplayAbility(스킬) 식별 로그 — "지금 어떤 어빌리티로 공격하는가".
            string abilityId = skill?.Id ?? SkillName(skillId);
            Debug.Log($"[GameplayAbility] 발동: '{abilityId}' (mana {manaCost}, cd {skill?.CooldownMs ?? 0}ms)");

            AgentAnimations?.SetTrigger(AnimationTriggerType.Attack);
            OnAttackPerformed?.Invoke(skillId);

            // 공격 발동 = Action 이동잠금. 스킬 타임라인(startup+active+recovery) 동안 수평 이동 금지(GroundState 폴링).
            float attackRootSec = skill != null ? (skill.StartupMs + skill.ActiveMs + skill.RecoveryMs) / 1000f : 0.4f;
            ApplyRoot(attackRootSec);
        }

        // 공격/상호작용 이동잠금(Rooted) 만료 시각(Time.time 기준). 0 = 잠금 없음.
        private float _rootedUntil;
        // 상호작용(줍기 등) 이동잠금 지속(초). 스킬과 달리 데이터 타임라인이 없어 고정값(튜닝 대상).
        private const float InteractRootSeconds = 0.6f;

        /// <summary>Action(공격·상호작용) 발동 시 <see cref="ActionTags.Rooted"/> 를 지속시간만큼 부여. Update 가 만료 시 해제.</summary>
        private void ApplyRoot(float seconds)
        {
            if (AbilitySystem == null || seconds <= 0f) return;
            AbilitySystem.AddTag(ActionTags.Rooted);
            _rootedUntil = Mathf.Max(_rootedUntil, Time.time + seconds);
        }

        /// <summary>마나 보유 확인(차감 X). 마나 속성 없는 캐릭터/테스트는 무료(true). cost &lt;= 0 도 true.</summary>
        private bool HasMana(int cost)
        {
            if (cost <= 0) return true;
            var mana = AbilitySystem?.GetAttribute(EGameplayAttribute.Mana);
            return mana == null || mana.CurrentValue >= cost;
        }

        /// <summary>마나 예측 차감. 서버가 권위로 검증·차감하고 S_PlayerMana 로 정정한다(되돌림 가능).</summary>
        private void SpendMana(int cost)
        {
            if (cost <= 0) return;
            var mana = AbilitySystem?.GetAttribute(EGameplayAttribute.Mana);
            mana?.ApplyModifier(GameplayAttributeModifier.Create(EGameplayAttribute.Mana, -cost, EModifierType.Additive));
        }

        /// <summary>시간 비례 마나 자연 회복(예측). <see cref="ManaConfig.RegenPerSecond"/> 누적 → 정수 단위 가산, Max 클램프.</summary>
        private void RegenMana(float dt)
        {
            var mana = AbilitySystem?.GetAttribute(EGameplayAttribute.Mana);
            if (mana == null || mana.CurrentValue >= mana.MaxValue)
            {
                _manaRegenAccum = 0f;
                return;
            }

            _manaRegenAccum += ManaConfig.RegenPerSecond * dt;
            int whole = (int)_manaRegenAccum;
            if (whole <= 0)
                return;

            _manaRegenAccum -= whole;
            mana.ApplyModifier(GameplayAttributeModifier.Create(EGameplayAttribute.Mana, whole, EModifierType.Additive));
        }

        /// <summary>스킬 쿨다운 경과 여부(클라 예측). 통과 시 마지막 발동시각 갱신. 데이터 미주입(테스트)이면 항상 true.</summary>
        private bool SkillCooldownReady(int skillId)
        {
            var skill = _skills?.Get(SkillName(skillId));
            if (skill == null)
                return true;

            float cd = skill.CooldownMs / 1000f;
            float now = Time.time;
            if (_lastCastTime.TryGetValue(skillId, out var last) && now - last < cd)
                return false;

            _lastCastTime[skillId] = now;
            return true;
        }

        /// <summary>
        /// 회피 = Action 축. 입력 소비 + 쿨다운 통과 시 DodgeDriver.Begin(대시+무적 태그+애니).
        /// 방향: 이동 입력이 있으면 그 방향(카메라 기준 월드), 없으면 정면 구르기(transform.forward).
        /// 던전 무적은 OnDodgePerformed → DodgeSyncSender → C_Dodge 로 서버가 권위 강제(쿨다운 검증).
        /// </summary>
        private bool HandleDodgeInput()
        {
            if (InputSource == null || !InputSource.ConsumeDodgePressed())
                return false;
            if (!_dodge.CanBegin(Time.time))
                return false;
            if (!HasMana(DodgeConfig.ManaCost)) // 마나 부족 → 회피 발동/송신 안 함(서버도 동일 거부)
                return false;

            Vector3 dir = Motor != null ? Motor.ResolveWorldMoveDirection(InputSource.Current.Move) : Vector3.zero;
            if (dir.sqrMagnitude < 0.0001f)
                dir = transform.forward; // 무입력 = 정면 구르기

            SpendMana(DodgeConfig.ManaCost); // 예측 차감 — 서버가 S_PlayerMana 로 정정
            _dodge.Begin(dir, Time.time);
            OnDodgePerformed?.Invoke();
            return true;
        }

        /// <summary>
        /// 상호작용 = Action 축. FSM 상태(구 InteractState)가 아니라 입력→탐지된 대상에게 위임.
        /// 대상(문/아이템/NPC)이 IInteractable.Interact(interactor)로 행동을 소유한다(instigator 전달).
        /// ※ 이동 제약/캐스트 타이밍이 필요해지면 GameplayEffect/태그로(전이 아님), 정식화는 후속.
        /// </summary>
        private void HandleInteractInput()
        {
            if (InputSource == null || !InputSource.ConsumeInteractPressed())
                return;

            var target = _interactionDetector != null ? _interactionDetector.CurrentInteractable : null;
            if (target == null)
                return;

            AgentAnimations?.SetTrigger(AnimationTriggerType.Interact);
            target.Interact(gameObject);
            ApplyRoot(InteractRootSeconds); // 줍기/상호작용 동안 이동 잠금(Action 축 — 전이 아님)
        }
    }
}
