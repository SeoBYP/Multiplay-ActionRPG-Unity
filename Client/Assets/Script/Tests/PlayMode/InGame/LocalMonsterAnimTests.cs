using System.Collections;
using Game.Gameplay.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// AC 증분6: Main(솔로·클라 권위) 몬스터 공격 발동 연출 검증 — <b>실제 CreepyDemonLocal 프리팹</b>을 로드해
    /// LocalMonster.PlayAbilityCue(발동)가 Animator 를 "Attack" 상태로 전이하는지 확인한다.
    /// 프리팹의 LocalMonster.attackState="Attack" 배선을 고정한다(비면 조용히 스킵돼 Main 몬스터 공격 애니가 안 보이는 회귀).
    /// 던전 MonsterEntity 와 동일한 재생 로직 — Main 은 로컬 AI 가 직접 호출(네트워크·라우터 없음).
    /// </summary>
    public class LocalMonsterAnimTests
    {
        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.Destroy(_instance);
            _instance = null;
        }

        [UnityTest]
        public IEnumerator 발동_Cue_재생하면_LocalMonster_Animator가_Attack상태로_전이한다()
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Character/Monster/CreepyDemonLocal.prefab");
#endif
            Assume.That(prefab, Is.Not.Null, "CreepyDemonLocal 프리팹 로드 실패(에디터 외 실행)");

            _instance = Object.Instantiate(prefab);
            var monster = _instance.GetComponent<LocalMonster>();
            Assert.IsNotNull(monster, "CreepyDemonLocal 에 LocalMonster 가 있어야 한다");

            var animator = _instance.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "CreepyDemonLocal 에 Animator 가 있어야 한다");
            Assume.That(animator.runtimeAnimatorController, Is.Not.Null, "몬스터 Animator Controller 미배선");

            for (int i = 0; i < 2; i++) yield return null; // Awake/초기 idle 안정화

            // 로컬 AI 의 TryAttack 이 발동 시 호출하는 것과 동일한 종단 재생.
            monster.PlayAbilityCue(AnimationTriggerType.Attack, comboStep: 0);

            bool enteredAttack = false;
            for (int i = 0; i < 30 && !enteredAttack; i++)
            {
                yield return null;
                var st = animator.GetCurrentAnimatorStateInfo(0);
                var next = animator.GetNextAnimatorStateInfo(0);
                if (st.IsName("Attack") || (animator.IsInTransition(0) && next.IsName("Attack")))
                    enteredAttack = true;
            }

            Assert.IsTrue(enteredAttack,
                "Main 몬스터 발동 시 Animator 가 Attack 상태로 전이해야 한다 — 프리팹 LocalMonster.attackState=\"Attack\" 배선 확인.");
        }
    }
}
