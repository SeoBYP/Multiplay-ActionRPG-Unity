using System.Collections;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Character;
using Game.Network.Socket;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// 원격 플레이어 연출 구동 검증 — <b>실제 RemotePlayerCharacter 프리팹</b>을 로드해 RemoteDriver 가
    /// 서버 이벤트로 Animator 를 전이시키는지 확인한다. 프리팹의 CharacterAgentAnimations 트리거명 배선까지
    /// 함께 검증한다(예: Dodge 트리거명이 비면 SetTrigger 가 조용히 스킵돼 원격 회피 애니가 안 보이는 회귀).
    /// </summary>
    public class RemoteDriverAnimTests
    {
        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.Destroy(_instance);
            _instance = null;
        }

        [UnityTest]
        public IEnumerator 원격_회피_수신하면_RemotePlayer_Animator가_Dodge상태로_전이한다()
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Character/RemotePlayerCharacter.prefab");
#endif
            Assume.That(prefab, Is.Not.Null, "RemotePlayerCharacter 프리팹 로드 실패(에디터 외 실행)");

            const long remoteId = 555;
            _instance = Object.Instantiate(prefab);
            var driver = _instance.GetComponent<RemoteDriver>();
            Assert.IsNotNull(driver, "RemotePlayerCharacter 에 RemoteDriver 가 있어야 한다");

            var animator = _instance.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "RemotePlayerCharacter 에 Animator 가 있어야 한다");
            Assume.That(animator.runtimeAnimatorController, Is.Not.Null, "PlayerController 미배선");

            var state = new SocketPacketState();
            driver.Initialize(remoteId, state); // OnPlayerDodged 구독

            for (int i = 0; i < 2; i++) yield return null; // 초기 프레임 안정화

            // 서버 S_Dodge → OnPlayerDodged → RemoteDriver.HandlePlayerDodged → CAA.SetTrigger(Dodge) → AnyState→Dodge
            state.NotifyPlayerDodged(remoteId);

            bool enteredDodge = false;
            for (int i = 0; i < 30 && !enteredDodge; i++)
            {
                yield return null;
                if (animator.GetCurrentAnimatorStateInfo(0).IsName("Dodge")) enteredDodge = true;
            }

            Assert.IsTrue(enteredDodge,
                "원격 회피(S_Dodge) 수신 시 Animator 가 Dodge 상태로 전이해야 한다 — 프리팹 CAA 의 Dodge 트리거명 배선 확인.");
        }

        [UnityTest]
        public IEnumerator 원격_콤보_A에서_B_C로_체인_전이한다()
        {
            // #7 원격 콤보: S_Attack{SkillId 2→3→4} 순서대로 수신 → RemoteDriver 가 ComboStep 0→1→2 + Attack.
            // 컨트롤러 [Attack] 서브SM 은 **이전 공격에서 이어서** 체인한다(AnyState 진입은 ComboA 뿐,
            // ComboA→ComboB→ComboC 는 상태→상태 전이 exitTime 0.35). 로컬·원격 동일 컨트롤러라 이 테스트가 체인 자체를 고정한다.
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Character/RemotePlayerCharacter.prefab");
#endif
            Assume.That(prefab, Is.Not.Null, "RemotePlayerCharacter 프리팹 로드 실패(에디터 외 실행)");

            const long remoteId = 556;
            _instance = Object.Instantiate(prefab);
            var driver = _instance.GetComponent<RemoteDriver>();
            var animator = _instance.GetComponentInChildren<Animator>();
            Assume.That(animator.runtimeAnimatorController, Is.Not.Null, "PlayerController 미배선");

            var state = new SocketPacketState();
            driver.Initialize(remoteId, state);
            for (int i = 0; i < 2; i++) yield return null;

            // A: AnyState(Attack && ComboStep==0) → ComboA. AC 통합: 발동 Cue 는 IActorView.PlayAbilityCue 로 들어온다
            // (런타임엔 S_AbilityActivated → AbilityCueRouter → ActorRegistry → 이 driver. 여기선 그 종단 호출을 직접 검증).
            driver.PlayAbilityCue(AnimationTriggerType.Attack, 0); // combo_a (cueComboStep=0)
            bool inA = false;
            float deadline = Time.time + 1f;
            while (Time.time < deadline && !inA)
            {
                yield return null;
                inA = IsEnteringOrIn(animator, "ComboA");
            }
            Assert.IsTrue(inA, "콤보A(skillId 2) 수신 시 ComboA 로 진입해야 한다.");

            // 스윙 도중 B 입력 → ComboA→ComboB 체인(상태→상태). 체인 전이는 hasExitTime=false 라 즉시 시작된다.
            yield return new WaitForSeconds(0.45f);
            driver.PlayAbilityCue(AnimationTriggerType.Attack, 1); // combo_b (cueComboStep=1)
            bool inB = false;
            deadline = Time.time + 1f;
            while (Time.time < deadline && !inB)
            {
                yield return null;
                inB = IsEnteringOrIn(animator, "ComboB");
            }
            Assert.IsTrue(inB, "콤보B(skillId 3) 수신 시 ComboA 에서 ComboB 로 체인해야 한다(서브SM 상태전이).");

            // 이어서 C 입력 → ComboB→ComboC
            yield return new WaitForSeconds(0.45f);
            driver.PlayAbilityCue(AnimationTriggerType.Attack, 2); // combo_c (cueComboStep=2)
            bool inC = false;
            deadline = Time.time + 1f;
            while (Time.time < deadline && !inC)
            {
                yield return null;
                inC = IsEnteringOrIn(animator, "ComboC");
            }
            Assert.IsTrue(inC, "콤보C(skillId 4) 수신 시 ComboB 에서 ComboC 로 체인해야 한다(서브SM 상태전이).");
        }

        [UnityTest]
        public IEnumerator 원격_콤보_패킷이_늦게_와도_해당_단계_애니가_재생된다()
        {
            // 던전 동기화 견고성. 로컬 체인 간격(ComboChainMs 0.8s) + 네트워크 지연이 원격의 ComboA 유지시간(1.0s)을
            // 넘으면 원격은 이미 Locomotion 이다. 이때 서브SM 체인(ComboA→ComboB)은 성립하지 않으므로,
            // **AnyState→ComboB 안전망**이 없으면 애니가 아예 안 나온다(원격만 콤보가 안 보이는 버그).
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Character/RemotePlayerCharacter.prefab");
#endif
            Assume.That(prefab, Is.Not.Null, "RemotePlayerCharacter 프리팹 로드 실패(에디터 외 실행)");

            const long remoteId = 557;
            _instance = Object.Instantiate(prefab);
            var driver = _instance.GetComponent<RemoteDriver>();
            var animator = _instance.GetComponentInChildren<Animator>();
            Assume.That(animator.runtimeAnimatorController, Is.Not.Null, "PlayerController 미배선");

            var state = new SocketPacketState();
            driver.Initialize(remoteId, state);
            for (int i = 0; i < 2; i++) yield return null;

            driver.PlayAbilityCue(AnimationTriggerType.Attack, 0); // A
            bool inA = false;
            float deadline = Time.time + 1f;
            while (Time.time < deadline && !inA) { yield return null; inA = IsEnteringOrIn(animator, "ComboA"); }
            Assert.IsTrue(inA, "콤보A 진입");

            // ComboA(클립 1.0s, 복귀 exitTime 1.0)를 완전히 지나 보낸다 → 원격은 Locomotion 으로 복귀한 상태.
            yield return new WaitForSeconds(1.3f);
            Assert.IsFalse(animator.GetCurrentAnimatorStateInfo(0).IsName("ComboA"),
                "전제: ComboA 는 이미 끝나 Locomotion 이어야 한다");

            // 늦게 도착한 B — 서브SM 체인은 못 타지만 AnyState 안전망으로 ComboB 가 재생돼야 한다.
            driver.PlayAbilityCue(AnimationTriggerType.Attack, 1);
            bool inB = false;
            deadline = Time.time + 1f;
            while (Time.time < deadline && !inB) { yield return null; inB = IsEnteringOrIn(animator, "ComboB"); }
            Assert.IsTrue(inB,
                "늦게 도착한 콤보B 도 ComboB 로 재생돼야 한다 — AnyState→ComboB 안전망 확인(원격 콤보 동기화).");
        }

        /// <summary>
        /// 해당 상태에 있거나 그 상태로 <b>전이 중</b>인가.
        /// 블렌드(체인 dur 0.20s) 동안 GetCurrentAnimatorStateInfo 는 여전히 <b>이전</b> 상태를 반환하므로,
        /// 전이 대상(GetNextAnimatorStateInfo)까지 봐야 "체인이 일어났다"를 프레임레이트와 무관하게 판정할 수 있다.
        /// </summary>
        private static bool IsEnteringOrIn(Animator animator, string stateName)
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
                return true;
            return animator.IsInTransition(0)
                   && animator.GetNextAnimatorStateInfo(0).IsName(stateName);
        }
    }
}
