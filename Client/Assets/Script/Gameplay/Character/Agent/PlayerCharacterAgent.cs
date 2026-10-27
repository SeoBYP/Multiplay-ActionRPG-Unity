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
        // 사망 태그(GAS). HP 0 시 세우고 입력/이동 게이트가 폴링한다. 던전=다운-잠금(씬 복귀 전까지 유지).
        private static readonly GameplayTag DeadTag = GameplayTags.Dead;
        // 스턴(CC) 태그. Duration 효과가 부여→자동 만료. 켜진 동안 입력/이동 정지.
        private static readonly GameplayTag StunTag = GameplayTags.Stun;

        [Header("락온(2.6.3)")]
        [Tooltip("락온 가능 최대 평면 사거리(m). 화면 안 + 이 거리 내 대상만 잡힌다.")]
        [SerializeField] private float lockOnRange = 15f;

        // 콤보 단계별 skillId — 서버 CombatHandler.ResolveSkill 과 동일 규약(2=combo_a/3=combo_b/4=combo_c/5=combo_d).
        private static readonly int[] ComboSkillIds = { 2, 3, 4, 5 };

        // 콤보 타이밍 폴백(스킬 데이터 미로드 시에만). 진실원은 SkillTimeline.ComboChainMs/ComboWindowMs(skills.json).
        private const float FallbackComboChainSec = 0.8f;
        private const float FallbackComboWindowSec = 0.9f;

        private InteractionDetector _interactionDetector;
        private DodgeDriver _dodge;
        private KnockbackDriver _knockback;
        private LockOnDriver _lockOn;
        private ComboDriver _combo;

        // 쿨다운·마나 예측용 스킬 데이터. DI 미주입(테스트) 시 null → 게이트 없음.
        private AbilityCatalogProvider _skills;
        private AbilityCuePlayer _cuePlayer; // 로컬 플레이어 발동 시 SFX/VFX 즉발(RTT 없이). 라우터는 원격만 담당.
        private readonly Dictionary<int, float> _lastCastTime = new();

        // 마나 리젠 소수부 누적(dt 비례 회복을 정수 Mana 로 환산). 서버 _manaRegenAccum 과 동일 방식.
        private float _manaRegenAccum;

        [Inject]
        public void ConstructAbilities(AbilityCatalogProvider skills) => _skills = skills;

        // 상호작용 안내(HUD) 채널 — 탐지 결과를 밀어 넣기만 하고 표시는 GUI 가 소유.
        private Game.System.Player.InteractionPromptNotifier _interactionPrompt;

        [Inject]
        public void ConstructInteractionPrompt(Game.System.Player.InteractionPromptNotifier prompt)
            => _interactionPrompt = prompt;

        /// <summary>공격 입력으로 스윙이 발동될 때 발행(인자=skillId: 0=기본/좌클릭, 1=강공격/우클릭).
        /// 던전 `CombatSyncSender`가 C_Attack{SkillId} 송신, Main `LocalCombat`가 그 스킬 hitbox 로 판정.</summary>
        public event Action<int> OnAttackPerformed;

        /// <summary>회피 발동(캐릭터 기준 방향 우+/전+). 던전 DodgeSyncSender 가 C_Dodge 로 실어 원격 연출을 맞춘다.</summary>
        public event Action<Vector2> OnDodgePerformed;

        /// <summary>로컬 플레이어가 사망(State.Dead)했는지. 입력·이동 게이트의 단일 판정.</summary>
        private bool IsDead => AbilitySystem != null && AbilitySystem.HasTag(DeadTag);

        /// <summary>스턴(State.Stun) 상태인지. 입력·이동 게이트가 폴링(자동 만료되면 해제).</summary>
        private bool IsStunned => AbilitySystem != null && AbilitySystem.HasTag(StunTag);

        protected override void Awake()
        {
            base.Awake();
            _interactionDetector = this.GetAroundComponent<InteractionDetector>();
            _cuePlayer = GetComponent<AbilityCuePlayer>(); // 미부착이면 null → 무시

            // 자기 HP 를 관찰해 0 이하가 되면 State.Dead 를 세운다.
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
                ClimbSensor          = this.GetAroundComponent<ClimbSensor>(), // 없으면 Climb 전이 미생성
            };

            _dodge = new DodgeDriver(Motor, AbilitySystem, AgentAnimations, settings);
            _knockback = new KnockbackDriver(Motor);
            // 콤보 타이밍 진실원 = SkillTimeline — 서버 cadence 게이트와 같은 값
            _combo = new ComboDriver(ComboSkillIds, ResolveComboTiming);

            // 락온 = 순수 클라 조준 보조. 카메라 Follow(피벗 회전 소유)와 Motor(facing) 에 매 프레임 push
            var cameraFollow = this.GetAroundComponent<CharacterCameraFollow>();
            _lockOn = new LockOnDriver(transform, Motor, cameraFollow, lockOnRange);
        }

        /// <summary>
        /// 외부(몬스터 공격·스킬)가 호출하는 넉백 임펄스 진입점. 방향 = 공격자→피격자, 거리/시간 동안 Motor 로 강제 변위(회전 없음).
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
            // Rooted 만료 — 사망/스턴 게이트보다 먼저 처리해 확실히 해제
            if (_rootedUntil > 0f && Time.time >= _rootedUntil)
            {
                AbilitySystem?.RemoveTag(ActionTags.Rooted);
                _rootedUntil = 0f;
            }

            // 사망(다운) 시 두 축 모두 게이트: Action 무시 + base.Update() 미호출로 Locomotion 정지. 부활 또는 씬 복귀 전까지 유지.
            if (IsDead)
            {
                _lockOn?.ForceUnlock(); // 다운되면 락 해제(facing/카메라 원복)
                _interactionPrompt?.Clear(); // 다운 중엔 상호작용 안내를 남기지 않는다
                return;
            }

            // 마나 자연 회복(클라 예측). 서버 RegenMana 와 동일 rate. 스턴/회피 중에도 진행.
            RegenMana(Time.deltaTime);

            // 넉백은 스턴보다 우선 — 밀려나는 중엔 임펄스가 이동을 전담
            if (_knockback.IsActive)
            {
                _knockback.Tick(Time.deltaTime);
                return;
            }

            // 스턴: 입력/이동 정지. 진행 중이던 회피는 끊는다(무적 태그도 정리).
            if (IsStunned)
            {
                if (_dodge.IsActive) _dodge.Cancel();
                // base.Update() 미호출로 Motor 는 이미 멈춤 — 걷기 클립/루트모션도 정지
                AgentAnimations?.SetFloat(AnimationFloatType.Speed, 0f);
                return;
            }

            // 회피 중엔 두 축 게이트 — 대시가 이동을 전담. 회피는 FSM 상태가 아니라 Action 축 임펄스.
            if (_dodge.IsActive)
            {
                _dodge.Tick(Time.deltaTime);
                return;
            }

            _interactionDetector?.DetectInteractable();
            // 대상 없으면 null → HUD 숨김. 변할 때만 발행된다.
            _interactionPrompt?.Set(_interactionDetector?.CurrentInteractable?.InteractionPrompt);
            if (HandleDodgeInput()) // 회피 시작 프레임엔 다른 Action/Locomotion 스킵
                return;
            HandleLockOnInput();
            HandleAttackInput();
            HandleHeavyAttackInput();
            HandleInteractInput();
            // facing 강제는 base.Update()→Motor.Move 전에 적용
            _lockOn?.Tick();
            base.Update();
        }

        // 피격(HP 감소) 판정용 직전 HP. -1 = 관측 전(첫 통지는 피격 아님).
        private int _lastObservedHealth = -1;

        /// <summary>
        /// HP 변화 반응. ① HP 가 <b>줄었고 살아 있으면</b> 피격 리액션 1회(연출 전용, 경직은 CC 태그 담당)
        /// ② HP 0 이하면 State.Dead 를 1회 세우고 다운 포즈 재생. 회복·첫 통지·사망 순간은 피격으로 보지 않는다.
        /// </summary>
        private void OnAttributeChanged(EGameplayAttribute type, int current, int max)
        {
            if (type == EGameplayAttribute.Health)
            {
                bool decreased = _lastObservedHealth >= 0 && current < _lastObservedHealth;
                _lastObservedHealth = current;

                if (decreased && current > 0 && !IsDead)
                    AgentAnimations?.SetTrigger(AnimationTriggerType.Hit);
            }

            if (type == EGameplayAttribute.Health && current <= 0 && AbilitySystem != null && !IsDead)
            {
                AbilitySystem.AddTag(DeadTag);
                _combo?.Reset(); // 부활 후 stale 단계 방지
                AgentAnimations?.ResetTrigger(AnimationTriggerType.Revive); // 잔재 트리거로 즉시 Dead 탈출 방지
                AgentAnimations?.ResetTrigger(AnimationTriggerType.Hit);     // 남은 피격 트리거가 Dead 를 빠져나가지 못하게
                AgentAnimations?.SetTrigger(AnimationTriggerType.Dead);
                Debug.Log("[PlayerCharacterAgent] 로컬 다운 — HP≤0 → State.Dead (입력 게이트). ※다운 애니는 미배선(로그 대체)");
            }
        }

        /// <summary>
        /// 로컬 부활(Main 타이머 리스폰 전용 — 던전은 다운잠금이라 호출되지 않는다).
        /// State.Dead 해제 + HP 만피 + 다운 트리거 리셋 + 스폰 지점 텔레포트.
        /// </summary>
        public void Revive(Vector3 spawnPos)
        {
            if (AbilitySystem == null) return;

            _dodge?.Cancel();
            _knockback?.Cancel();
            AbilitySystem.RemoveTag(DeadTag);
            AbilitySystem.SetCurrent(EGameplayAttribute.Health, AbilitySystem.Max(EGameplayAttribute.Health));

            AgentAnimations?.ResetTrigger(AnimationTriggerType.Dead);
            AgentAnimations?.SetTrigger(AnimationTriggerType.Revive); // Dead 포즈 → 로코모션 복귀

            // CharacterController 는 비활성화 후 위치를 설정해야 내부 위치와 어긋나지 않는다.
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
        /// Co-op 부활(던전) — 서버 S_PlayerRevived 로 호출. State.Dead 해제 + 서버 권위 HP 적용 + 다운 트리거 리셋. <b>텔레포트 없음</b>(제자리).
        /// </summary>
        public void ReviveInPlace(int hp)
        {
            if (AbilitySystem == null) return;

            _dodge?.Cancel();
            _knockback?.Cancel();
            AbilitySystem.RemoveTag(DeadTag);
            AbilitySystem.SetCurrent(EGameplayAttribute.Health, hp);
            AgentAnimations?.ResetTrigger(AnimationTriggerType.Dead);
            AgentAnimations?.SetTrigger(AnimationTriggerType.Revive);

            Debug.Log($"[PlayerCharacterAgent] Co-op 부활 — State.Dead 해제, HP={hp} (제자리). ※부활 애니 미배선(로그)");
        }

        private void OnDestroy()
        {
            if (AbilitySystem != null)
                AbilitySystem.OnAttributeChanged -= OnAttributeChanged;
        }

        /// <summary>
        /// 콤보 단계 skillId → (체인 지점 초, 콤보 창 초). <b>진실원 = 스킬 데이터</b>(skills.json) —
        /// 서버 `CombatHandler` cadence 게이트가 같은 `ComboChainMs` 를 쓰므로 애니와 서버 권위가 어긋나지 않는다. 데이터 미로드면 폴백 상수.
        /// </summary>
        private (float chainSec, float windowSec) ResolveComboTiming(int skillId)
        {
            var skill = _skills?.GetTimeline(skillId);
            if (skill == null)
                return (FallbackComboChainSec, FallbackComboWindowSec);

            float chain = skill.ComboChainMs > 0 ? skill.ComboChainMs / 1000f : FallbackComboChainSec;
            float window = skill.ComboWindowMs > 0 ? skill.ComboWindowMs / 1000f : FallbackComboWindowSec;
            return (chain, window);
        }

        /// <summary>
        /// 공격 = Action 축. 기본공격 반복 = 콤보 A→B→C(선입력 버퍼링). 입력은 콤보기에 접수만 하고,
        /// 실제 발동은 <see cref="ComboDriver.TryFire"/> 가 "지금 나갈 시점"을 알릴 때 한다(스윙 중 선입력이 체인 지점에 이어진다).
        /// 적중 판정·데미지는 <b>서버 권위</b>(CombatSyncSender → C_Attack), 로컬은 연출만.
        /// </summary>
        private void HandleAttackInput()
        {
            if (InputSource != null && InputSource.ConsumeAttackPressed())
                _combo.OnAttackPressed(Time.time);

            if (_combo.TryFire(Time.time, out int skillId, out int step))
                FireSkill(skillId, step);
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
            FireSkill(1);
        }

        /// <summary>스킬 발동 — 클라 마나+쿨다운 예측 게이트 통과 시 애니+OnAttackPerformed. 서버 게이트가 최종 권위.
        /// comboStep: 콤보 단계(0=A/1=B/2=C). 음수 = 비콤보(강공격 등)로 A 애니 재생.</summary>
        private void FireSkill(int skillId, int comboStep = -1)
        {
            var skill = _skills?.GetTimeline(skillId);
            int manaCost = skill?.ManaCost ?? 0;
            if (!HasMana(manaCost))            // 마나는 쿨다운보다 먼저 — 거부될 발동이 쿨다운을 커밋하지 않게
                return;
            if (!SkillCooldownReady(skillId))  // 통과 시 발동시각 커밋
                return;
            SpendMana(manaCost);               // 예측 차감 — 서버가 S_PlayerMana 로 정정

            string abilityId = skill?.Id ?? $"(unknown networkId {skillId})";
            Debug.Log($"[GameplayAbility] 발동: '{abilityId}' (mana {manaCost}, cd {skill?.CooldownMs ?? 0}ms)");

            // 콤보 단계 파라미터로 컨트롤러가 A/B/C 선택
            AgentAnimations?.SetInt(AnimationIntType.ComboStep, comboStep < 0 ? 0 : comboStep);
            AgentAnimations?.SetTrigger(AnimationTriggerType.Attack);
            OnAttackPerformed?.Invoke(skillId);
            _cuePlayer?.Play(_skills?.Get(skillId)); // 로컬 즉발(RTT 없이). 라우터는 원격만 담당

            // 스킬 타임라인(startup+active+recovery) 동안 수평 이동 금지(GroundState 폴링)
            float attackRootSec = skill != null ? (skill.StartupMs + skill.ActiveMs + skill.RecoveryMs) / 1000f : 0.4f;
            ApplyRoot(attackRootSec);
        }

        // Rooted 만료 시각(Time.time 기준). 0 = 잠금 없음.
        private float _rootedUntil;
        // 상호작용 이동잠금 지속(초). 스킬과 달리 데이터 타임라인이 없어 고정값.
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
            if (AbilitySystem == null || !AbilitySystem.Has(EGameplayAttribute.Mana))
                return true;
            return AbilitySystem.Current(EGameplayAttribute.Mana) >= cost;
        }

        /// <summary>마나 예측 차감. 서버가 권위로 검증·차감하고 S_PlayerMana 로 정정한다(되돌림 가능).</summary>
        private void SpendMana(int cost)
        {
            if (cost <= 0) return;
            AbilitySystem?.ApplyModifiers(new[]
            {
                GameplayAttributeModifier.Create(EGameplayAttribute.Mana, -cost, EModifierType.Additive),
            });
        }

        /// <summary>시간 비례 마나 자연 회복(예측). <see cref="ManaConfig.RegenPerSecond"/> 누적 → 정수 단위 가산, Max 클램프.</summary>
        private void RegenMana(float dt)
        {
            if (AbilitySystem == null || !AbilitySystem.Has(EGameplayAttribute.Mana)
                || AbilitySystem.Current(EGameplayAttribute.Mana) >= AbilitySystem.Max(EGameplayAttribute.Mana))
            {
                _manaRegenAccum = 0f;
                return;
            }

            _manaRegenAccum += ManaConfig.RegenPerSecond * dt;
            int whole = (int)_manaRegenAccum;
            if (whole <= 0)
                return;

            _manaRegenAccum -= whole;
            AbilitySystem.ApplyModifiers(new[]
            {
                GameplayAttributeModifier.Create(EGameplayAttribute.Mana, whole, EModifierType.Additive),
            });
        }

        /// <summary>스킬 쿨다운 경과 여부(클라 예측). 통과 시 마지막 발동시각 갱신. 데이터 미주입(테스트)이면 항상 true.</summary>
        private bool SkillCooldownReady(int skillId)
        {
            var skill = _skills?.GetTimeline(skillId);
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
        /// 회피 = Action 축. 방향: 이동 입력이 있으면 그 방향(카메라 기준 월드), 없으면 정면 구르기.
        /// 던전 무적은 OnDodgePerformed → DodgeSyncSender → C_Dodge 로 서버가 권위 강제(쿨다운 검증).
        /// </summary>
        private bool HandleDodgeInput()
        {
            if (InputSource == null || !InputSource.ConsumeDodgePressed())
                return false;
            if (!_dodge.CanBegin(Time.time))
                return false;
            if (!HasMana(DodgeConfig.ManaCost)) // 서버도 동일 거부
                return false;

            Vector3 dir = Motor != null ? Motor.ResolveWorldMoveDirection(InputSource.Current.Move) : Vector3.zero;
            if (dir.sqrMagnitude < 0.0001f)
                dir = transform.forward; // 무입력 = 정면 구르기

            SpendMana(DodgeConfig.ManaCost); // 예측 차감 — 서버가 S_PlayerMana 로 정정
            _dodge.Begin(dir, Time.time);
            OnDodgePerformed?.Invoke(_dodge.LastLocalDirection); // Begin 이 계산한 방향 재사용
            return true;
        }

        /// <summary>
        /// 상호작용 = Action 축. 입력→탐지된 대상에게 위임, 대상이 IInteractable.Interact(interactor)로 행동을 소유한다.
        /// </summary>
        private void HandleInteractInput()
        {
            if (InputSource == null || !InputSource.ConsumeInteractPressed())
                return;

            var target = _interactionDetector != null ? _interactionDetector.CurrentInteractable : null;
            if (target == null)
                return;

            // 상호작용 애니는 재생하지 않는다 — 줍기 피드백은 GameHud 획득 토스트(InGameModel.OnItemPickup).
            // 이동잠금은 위임 **전에** 건다 — 대상의 Interact 가 조기반환/예외여도 잠금은 걸려야 한다.
            ApplyRoot(InteractRootSeconds);
            target.Interact(gameObject);
        }
    }
}
