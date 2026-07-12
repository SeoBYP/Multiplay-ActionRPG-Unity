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
    }
}
