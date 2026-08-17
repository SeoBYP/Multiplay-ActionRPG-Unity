using System.Collections;
using Game.Gameplay.Abilities;
using Game.Gameplay.Character;
using Game.Gameplay.Monster;
using Game.Network.Socket;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// AC 증분4: 몬스터 공격 발동 연출 검증 — <b>실제 Monster 프리팹</b>을 로드해 서버 발동 신호(S_AbilityActivated)가
    /// ActorRegistry→AbilityCueRouter→MonsterEntity.PlayAbilityCue→Animator("Attack") 로 관통하는지 확인한다.
    /// 프리팹의 MonsterEntity.attackState 배선("Attack")까지 함께 고정한다(비면 SetState 가 조용히 스킵돼 공격 애니가 안 보이는 회귀).
    /// </summary>
    public class MonsterEntityAnimTests
    {
        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.Destroy(_instance);
            _instance = null;
        }

        [UnityTest]
        public IEnumerator 서버_발동신호_수신하면_몬스터_Animator가_Attack상태로_전이한다()
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Character/Monster/Monster_creepy_demon.prefab");
#endif
            Assume.That(prefab, Is.Not.Null, "Monster_creepy_demon 프리팹 로드 실패(에디터 외 실행)");

            const int instanceId = 1;
            _instance = Object.Instantiate(prefab);
            var entity = _instance.GetComponent<MonsterEntity>();
            Assert.IsNotNull(entity, "Monster 프리팹에 MonsterEntity 가 있어야 한다");

            var animator = _instance.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "Monster 프리팹에 Animator 가 있어야 한다");
            Assume.That(animator.runtimeAnimatorController, Is.Not.Null, "몬스터 Animator Controller 미배선");

            // 배선: registry 에 등록 + router 가 발동 신호를 라우팅 (던전 런타임과 동일 구성).
            var state = new SocketPacketState();
            var registry = new ActorRegistry();
            long actorId = ActorIds.FromMonster(instanceId); // -1
            registry.Register(actorId, entity);
            var router = new AbilityCueRouter(state, registry, abilities: null);
            router.Initialize();

            entity.Initialize(instanceId, state); // idle 로 시작
            for (int i = 0; i < 2; i++) yield return null; // 초기 프레임 안정화

            // 서버 S_AbilityActivated → OnAbilityActivated → 라우터 → entity.PlayAbilityCue → CrossFade("Attack")
            state.NotifyAbilityActivated(actorId, skillId: 0);

            bool enteredAttack = false;
            for (int i = 0; i < 30 && !enteredAttack; i++)
            {
                yield return null;
                var st = animator.GetCurrentAnimatorStateInfo(0);
                var next = animator.GetNextAnimatorStateInfo(0);
                if (st.IsName("Attack") || (animator.IsInTransition(0) && next.IsName("Attack")))
                    enteredAttack = true;
            }

            router.Dispose();
            Assert.IsTrue(enteredAttack,
                "서버 발동 신호 수신 시 몬스터 Animator 가 Attack 상태로 전이해야 한다 — 프리팹 CharacterAgentAnimations 의 Attack 트리거명 배선 확인.");
        }

        [UnityTest]
        public IEnumerator 서버_이동_수신하면_몬스터_Animator가_Walk상태로_전이한다()
        {
            // 회귀 고정: 컨트롤러는 Speed 파라미터로 Idle↔Walk 를 전이하는데 코드가 상태이름 CrossFade 로 구동하던 시절,
            // Speed 를 아무도 세팅하지 않아(항상 0) Walk 진입 즉시 Walk→Idle[Speed<0.1] 이 발동 → 걷기 애니가 안 보였다.
            // → 파라미터 구동(SetFloat(Speed))으로 전환. 실제로 Walk 로 전이하는지 고정한다.
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Character/Monster/Monster_creepy_demon.prefab");
#endif
            Assume.That(prefab, Is.Not.Null, "Monster_creepy_demon 프리팹 로드 실패(에디터 외 실행)");

            const int instanceId = 2;
            _instance = Object.Instantiate(prefab);
            var entity = _instance.GetComponent<MonsterEntity>();
            var animator = _instance.GetComponentInChildren<Animator>();
            Assume.That(animator.runtimeAnimatorController, Is.Not.Null, "몬스터 Animator Controller 미배선");

            var state = new SocketPacketState();
            state.AddMonster(new SocketMonsterSnapshot(instanceId, "creepy_demon", 0f, 0f, 0f, 0f, 30, 30, 0));
            entity.Initialize(instanceId, state);
            for (int i = 0; i < 2; i++) yield return null; // 초기(Idle) 안정화

            // 서버가 먼 위치를 통지 → MonsterEntity 가 보간 이동 → Speed 상승 → 컨트롤러 Idle→Walk
            state.UpdateMonster(instanceId, 50f, 0f, 0f, 0f, 30, phase: 2, seq: 1); // seq: 스폰 baseline 0 보다 커야 반영(AC-C3)

            bool enteredWalk = false;
            for (int i = 0; i < 30 && !enteredWalk; i++)
            {
                yield return null;
                var st = animator.GetCurrentAnimatorStateInfo(0);
                var next = animator.GetNextAnimatorStateInfo(0);
                if (st.IsName("Walk") || (animator.IsInTransition(0) && next.IsName("Walk")))
                    enteredWalk = true;
            }

            Assert.IsTrue(enteredWalk,
                "서버 이동 수신 시 Speed 파라미터가 올라 Animator 가 Walk 로 전이해야 한다 — CharacterAgentAnimations 의 Speed 파라미터명 배선 확인.");
        }

        [UnityTest]
        public IEnumerator 보스_슬램_발동신호는_평타가_아닌_AttackSpecial_상태로_전이한다()
        {
            // AC-D1 회귀 고정. 체인 전체를 실물로 관통시킨다:
            //   MonsterVisualCatalog("leviathan_boss") → 프리팹 → AbilityCatalog(networkId 109 → cueTrigger AttackSpecial)
            //   → AbilityCueRouter → MonsterEntity.PlayAbilityCue → CAA 트리거명 → 컨트롤러 AttackSpecial 상태
            // 어느 고리든 끊기면 조용히 평타(Attack)로 폴백하거나 아무것도 안 나온다 — 그게 "슬램이 평타처럼 보이던" 증상.
            MonsterVisualCatalog visuals = null;
            AbilityCatalogDefinition abilityCatalog = null;
#if UNITY_EDITOR
            visuals = UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterVisualCatalog>(
                "Assets/GameData/Monster/MonsterVisualCatalog.asset");
            abilityCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityCatalogDefinition>(
                "Assets/GameData/Ability/AbilityCatalogDefinition.asset");
#endif
            Assume.That(visuals, Is.Not.Null, "MonsterVisualCatalog 로드 실패(에디터 외 실행)");
            Assume.That(abilityCatalog, Is.Not.Null, "AbilityCatalogDefinition 로드 실패(에디터 외 실행)");

            // 스포너와 동일한 해석 경로. 미등록이면 여기서 null → 런타임엔 기본 캡슐 폴백(=슬램 애니 도달 불가).
            var prefab = visuals.GetPrefab("leviathan_boss");
            Assert.IsNotNull(prefab, "leviathan_boss 표시 프리팹 미등록 — MonsterSpawner 가 캡슐로 폴백한다");

            var abilities = new AbilityCatalogProvider(abilityCatalog);
            var slam = abilities.Get("leviathan_slam");
            Assert.IsNotNull(slam, "leviathan_slam 어빌리티가 카탈로그에 없다");
            Assert.AreEqual(AnimationTriggerType.AttackSpecial, slam.cueTrigger,
                "슬램의 cueTrigger 가 AttackSpecial 이 아니면 평타와 같은 애니가 나온다");

            const int instanceId = 4;
            _instance = Object.Instantiate(prefab);
            var entity = _instance.GetComponent<MonsterEntity>();
            Assert.IsNotNull(entity, "leviathan 프리팹에 MonsterEntity 가 있어야 한다");

            var animator = _instance.GetComponentInChildren<Animator>();
            Assume.That(animator?.runtimeAnimatorController, Is.Not.Null, "leviathan Animator Controller 미배선");

            var state = new SocketPacketState();
            var registry = new ActorRegistry();
            long actorId = ActorIds.FromMonster(instanceId);
            registry.Register(actorId, entity);
            var router = new AbilityCueRouter(state, registry, abilities);
            router.Initialize();

            entity.Initialize(instanceId, state);
            for (int i = 0; i < 2; i++) yield return null;

            state.NotifyAbilityActivated(actorId, slam.networkId);

            bool enteredSpecial = false;
            for (int i = 0; i < 30 && !enteredSpecial; i++)
            {
                yield return null;
                var st = animator.GetCurrentAnimatorStateInfo(0);
                var next = animator.GetNextAnimatorStateInfo(0);
                if (st.IsName("AttackSpecial") || (animator.IsInTransition(0) && next.IsName("AttackSpecial")))
                    enteredSpecial = true;
            }

            router.Dispose();
            Assert.IsTrue(enteredSpecial,
                "슬램 발동 시 Animator 가 AttackSpecial 로 전이해야 한다 — 프리팹 m_animationAttackSpecialTrigger 와 컨트롤러 파라미터 배선 확인.");
        }

        [UnityTest]
        public IEnumerator 사망시_체력바가_0으로_내려간_뒤_죽는모션이_나온다()
        {
            // 회귀: 서버는 죽는 순간의 S_MonsterState 를 보내지 않고 S_MonsterDead 만 보낸다.
            // 클라가 HP 를 0 으로 확정하지 않으면 **체력바가 치명타 직전 값에 멈춘 채** die 애니가 재생된다
            // (사용자 관측: "체력바가 남아 있는데 죽는 모션이 나와").
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Character/Monster/Monster_creepy_demon.prefab");
#endif
            Assume.That(prefab, Is.Not.Null, "Monster_creepy_demon 프리팹 로드 실패(에디터 외 실행)");

            const int instanceId = 3;
            _instance = Object.Instantiate(prefab);
            var entity = _instance.GetComponent<MonsterEntity>();

            var state = new SocketPacketState();
            state.AddMonster(new SocketMonsterSnapshot(instanceId, "creepy_demon", 0f, 0f, 0f, 0f, 30, 30, 0));
            entity.Initialize(instanceId, state);
            yield return null;

            // 치명타 직전: HP 가 12 로 깎인 상태(체력바도 12/30 을 그리고 있다).
            state.UpdateMonster(instanceId, 0f, 0f, 0f, 0f, hp: 12, phase: 3, seq: 1);
            yield return null;
            Assume.That(entity.Hp, Is.EqualTo(12), "선행 조건: 사망 직전 HP 가 남아 있어야 이 회귀가 성립한다");

            int hpChangedCalls = 0;
            entity.HpChanged += _ => hpChangedCalls++;

            // 서버가 죽였다 — HP 통지 없이 사망만 온다.
            state.RemoveMonster(instanceId);
            yield return null;

            Assert.AreEqual(0, entity.Hp, "사망 통지를 받으면 HP 가 0 이어야 한다(체력바가 남아 있으면 안 된다)");
            Assert.AreEqual(1, hpChangedCalls, "체력바가 다시 그리도록 HpChanged 가 발화해야 한다");
        }
    }
}
