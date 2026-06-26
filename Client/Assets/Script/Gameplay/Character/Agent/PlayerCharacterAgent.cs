using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Abilities;
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

        private InteractionDetector _interactionDetector;
        private DodgeDriver _dodge;
        private KnockbackDriver _knockback;

        // 스킬 데이터(쿨다운) 조회 — 클라 쿨다운 예측용. DI 미주입(테스트) 시 null → 게이트 없음.
        private SkillCatalogProvider _skills;
        private readonly Dictionary<int, float> _lastCastTime = new();

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

            // MotionMatchingDriver가 붙어 있으면 MM 연동, 없으면 기존 Animator 방식
            var motionMatching = this.GetAroundComponent<IMotionMatchingDriver>();

            Context = new CharacterStateContext
            {
                Motor                = Motor,
                GroundDetector       = GroundDetector,
                Animations           = AgentAnimations,
                InputSource          = InputSource,
                AbilitySystem        = AbilitySystem,
                LocomotionSettings   = settings,
                MotionMatching       = motionMatching,
            };

            _dodge = new DodgeDriver(Motor, AbilitySystem, AgentAnimations, settings);
            _knockback = new KnockbackDriver(Motor);
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
            // 사망(다운) 시 두 축 모두 게이트: Action(공격/상호작용) 무시 + base.Update() 미호출로
            // Locomotion(이동) 정지. 던전 내 부활(2.5.2) 또는 씬 복귀 전까지 다운-잠금 유지.
            if (IsDead)
                return;

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
            HandleAttackInput();
            HandleHeavyAttackInput();
            HandleInteractInput();
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

        /// <summary>강공격(우클릭) = skillId 1(heavy_swing).</summary>
        private void HandleHeavyAttackInput()
        {
            if (InputSource == null || !InputSource.ConsumeHeavyAttackPressed())
                return;
            FireSkill(1); // 강공격(heavy_swing)
        }

        /// <summary>스킬 발동 — 클라 쿨다운 예측 게이트 통과 시 애니+OnAttackPerformed. 서버 SkillTimeline 게이트가 최종 권위.</summary>
        private void FireSkill(int skillId)
        {
            if (!SkillCooldownReady(skillId))
                return;
            AgentAnimations?.SetTrigger(AnimationTriggerType.Attack);
            OnAttackPerformed?.Invoke(skillId);
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

            Vector3 dir = Motor != null ? Motor.ResolveWorldMoveDirection(InputSource.Current.Move) : Vector3.zero;
            if (dir.sqrMagnitude < 0.0001f)
                dir = transform.forward; // 무입력 = 정면 구르기

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
        }
    }
}
