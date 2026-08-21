using System.Collections;
using Game.Gameplay.Character;
using Game.Network.Socket;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// 던전 원격 플레이어 로코모션 동기화 — <b>실제 RemotePlayerCharacter 프리팹</b>으로 검증한다.
    ///
    /// 회귀 배경: 원격은 <c>Strafe</c> 를 켜지 않아 1D 블렌드(전진 클립)만 재생했다. 옆으로 게걸음을 치든
    /// 뒤로 걷든 다른 사람 화면에는 <b>전부 전진</b>으로 보였다 — 8방향 블렌드를 만들어 놓고 원격만 못 쓰던 상태.
    ///
    /// 검증 축 셋:
    ///   ① 방향 — 보간 속도 + RotY 로 MoveX/MoveY 복원(패킷 추가 없음)
    ///   ② 모드 — S_Move.AnimState(점프/낙하/사다리)로 애니 상태 전이
    ///   ③ 회피 방향 — S_Dodge.DirX/DirY
    /// </summary>
    public class RemoteLocomotionSyncTests
    {
        private const long RemoteId = 777;

        private GameObject _instance;
        private RemoteDriver _driver;
        private SocketPacketState _state;
        private CharacterAgentAnimations _animations;
        private Animator _animator;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.Destroy(_instance);
            _instance = null;
            _driver = null;
            _state = null;
        }

        [UnityTest]
        public IEnumerator 원격이_옆으로_이동하면_Strafe_블렌드의_MoveX가_선다()
        {
            yield return Spawn();

            // 정면(rotY=0)을 본 채 월드 +X 로 이동 = 오른쪽 게걸음.
            yield return MoveRemote(from: Vector3.zero, to: new Vector3(3f, 0f, 0f), rotY: 0f);

            Assert.IsTrue(_animator.GetBool("Strafe"),
                "원격도 8방향 블렌드를 써야 한다(끄면 옆걸음이 전진 클립으로 보인다).");
            Assert.Greater(_animator.GetFloat("MoveX"), 0.5f,
                $"오른쪽 이동이면 MoveX 가 양수여야 한다(실측 {_animator.GetFloat("MoveX"):F2}).");
            Assert.Less(Mathf.Abs(_animator.GetFloat("MoveY")), 0.5f,
                $"전후 성분은 거의 0 이어야 한다(실측 {_animator.GetFloat("MoveY"):F2}).");
        }

        [UnityTest]
        public IEnumerator 원격이_뒤로_이동하면_MoveY가_음수다()
        {
            yield return Spawn();

            yield return MoveRemote(from: Vector3.zero, to: new Vector3(0f, 0f, -3f), rotY: 0f);

            Assert.Less(_animator.GetFloat("MoveY"), -0.5f,
                $"후진이면 MoveY 음수여야 뒷걸음 클립이 나온다(실측 {_animator.GetFloat("MoveY"):F2}).");
        }

        [UnityTest]
        public IEnumerator 원격_점프_상태를_받으면_Jump_애니로_전이한다()
        {
            yield return Spawn();

            _state.UpdatePlayerTransform(RemoteId, 0f, 1.2f, 0f, 0f, 1L, (byte)StateKind.Jump);

            bool entered = false;
            for (int i = 0; i < 40 && !entered; i++)
            {
                yield return null;
                entered = InState("Jump");
            }

            Assert.IsTrue(entered, "AnimState=Jump 면 원격도 점프 애니를 재생해야 한다(지금은 지상 가정이라 미끄러져 올라간다).");
        }

        [UnityTest]
        public IEnumerator 원격_사다리_상태를_받으면_Climbing이_켜지고_오르는_배속이_양수다()
        {
            yield return Spawn();

            // 사다리를 오르는 중 — y 가 계속 증가한다.
            _state.UpdatePlayerTransform(RemoteId, 0f, 0f, 0f, 0f, 1L, (byte)StateKind.Climb);
            yield return null;
            for (int i = 1; i <= 10; i++)
            {
                _state.UpdatePlayerTransform(RemoteId, 0f, i * 0.15f, 0f, 0f, i + 1L, (byte)StateKind.Climb);
                yield return null;
            }

            Assert.IsTrue(_animator.GetBool("Climbing"),
                "사다리 상태면 Climbing 이 켜져야 한다(안 켜면 걷기 클립으로 공중에 뜬 채 올라간다).");
            Assert.Greater(_animator.GetFloat("ClimbSpeed"), 0f,
                $"오를 때는 클립 배속이 양수여야 한다(실측 {_animator.GetFloat("ClimbSpeed"):F2}).");
        }

        [UnityTest]
        public IEnumerator 원격_사다리를_내려가면_배속이_음수다()
        {
            yield return Spawn();

            _state.UpdatePlayerTransform(RemoteId, 0f, 2f, 0f, 0f, 1L, (byte)StateKind.Climb);
            yield return null;
            for (int i = 1; i <= 10; i++)
            {
                _state.UpdatePlayerTransform(RemoteId, 0f, 2f - i * 0.15f, 0f, 0f, i + 1L, (byte)StateKind.Climb);
                yield return null;
            }

            Assert.Less(_animator.GetFloat("ClimbSpeed"), 0f,
                $"내려갈 때는 역재생(음수 배속)이어야 한다(실측 {_animator.GetFloat("ClimbSpeed"):F2}).");
        }

        [UnityTest]
        public IEnumerator 원격_회피는_수신한_방향으로_구른다()
        {
            yield return Spawn();

            _state.NotifyPlayerDodged(RemoteId, -1f, 0f); // 왼쪽 구르기
            yield return null;

            Assert.Less(_animator.GetFloat("DodgeX"), -0.5f,
                $"왼쪽 회피면 DodgeX 가 음수여야 한다(실측 {_animator.GetFloat("DodgeX"):F2}). 예전엔 항상 정면으로 근사했다.");
            Assert.Less(Mathf.Abs(_animator.GetFloat("DodgeY")), 0.5f);
        }

        // ── 리그 ────────────────────────────────────────────────────────────

        private IEnumerator Spawn()
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Character/RemotePlayerCharacter.prefab");
#endif
            Assume.That(prefab, Is.Not.Null, "RemotePlayerCharacter 프리팹 로드 실패(에디터 외 실행)");

            _instance = Object.Instantiate(prefab);
            _driver = _instance.GetComponent<RemoteDriver>();
            _animations = _instance.GetComponent<CharacterAgentAnimations>();
            _animator = _instance.GetComponentInChildren<Animator>();

            Assert.IsNotNull(_driver, "RemotePlayerCharacter 에 RemoteDriver 가 있어야 한다");
            Assume.That(_animator?.runtimeAnimatorController, Is.Not.Null, "PlayerController 미배선");

            _state = new SocketPacketState();
            _state.UpsertPlayer(RemoteId, "Remote", 0, "dungeon_01", 0f, 0f, 0f, 0f);
            _driver.Initialize(RemoteId, _state);

            for (int i = 0; i < 2; i++) yield return null;
        }

        /// <summary>스냅샷을 여러 번 흘려 보간이 실제로 움직이게 한다(원격 속도는 보간 결과에서 역산된다).</summary>
        private IEnumerator MoveRemote(Vector3 from, Vector3 to, float rotY)
        {
            _instance.transform.position = from;
            _state.UpdatePlayerTransform(RemoteId, to.x, to.y, to.z, rotY, 1L, (byte)StateKind.Ground);

            for (int i = 0; i < 20; i++) yield return null;
        }

        private bool InState(string name)
        {
            var info = _animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(name)) return true;
            return _animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsName(name);
        }
    }
}
