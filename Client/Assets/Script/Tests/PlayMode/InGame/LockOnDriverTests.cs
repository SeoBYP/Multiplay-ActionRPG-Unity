using System.Collections;
using System.Collections.Generic;
using Game.Gameplay.Camera;
using Game.Gameplay.Character;
using Game.Gameplay.Character.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// 2.6.3 락온(타겟팅) 드라이버 — 순수 클라 조준 보조. 화면중앙 타겟 선정·토글·facing/카메라 잠금·
    /// 사거리/소실 시 자동 해제를 격리 검증한다. 패킷/서버 무관(facing → C_Move 로 서버 hitbox 정렬).
    /// </summary>
    public class LockOnDriverTests
    {
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects)
                if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        [UnityTest]
        public IEnumerator FindBest는_화면중앙에_가까운_대상을_고른다()
        {
            MakeMainCamera();                       // 원점에서 +Z 를 바라봄
            var center = MakeTarget(new Vector3(0f, 0f, 8f));  // 화면 중앙
            MakeTarget(new Vector3(2f, 0f, 8f));               // 화면 우측(중앙에서 더 멈)

            var best = LockOnTarget.FindBest(Camera.main, Vector3.zero, 20f);
            Assert.AreSame(center, best, "뷰포트 중심에 가장 가까운 대상이 선택돼야 한다.");
            yield break;
        }

        [UnityTest]
        public IEnumerator 락온_토글은_facing과_카메라를_잠그고_다시_해제한다()
        {
            MakeMainCamera();
            var (motor, follow) = BuildPlayer();
            var target = MakeTarget(new Vector3(0f, 0f, 5f)); // 정면(+Z)
            var driver = new LockOnDriver(motor.transform, motor, follow, 15f);

            Assert.IsFalse(driver.IsLocked);

            driver.Toggle();                                   // 획득
            Assert.IsTrue(driver.IsLocked, "정면 사거리 내 대상이면 락온돼야 한다.");
            Assert.AreSame(target.transform, follow.LockTarget, "카메라 LockTarget 이 대상에 걸려야 한다.");

            driver.Tick();                                     // facing 적용
            Assert.IsTrue(motor.FacingOverride.HasValue);
            Assert.Greater(motor.FacingOverride.Value.z, 0.9f, "facing 이 정면 타겟(+Z)을 향해야 한다.");

            driver.Toggle();                                   // 해제
            Assert.IsFalse(driver.IsLocked);
            Assert.IsNull(follow.LockTarget, "해제 시 카메라 LockTarget 이 풀려야 한다.");
            Assert.IsFalse(motor.FacingOverride.HasValue, "해제 시 facing 오버라이드가 풀려야 한다.");
            yield break;
        }

        [UnityTest]
        public IEnumerator 사거리밖_대상은_락온되지_않는다()
        {
            MakeMainCamera();
            var (motor, follow) = BuildPlayer();
            MakeTarget(new Vector3(0f, 0f, 30f));              // 정면이지만 사거리 밖
            var driver = new LockOnDriver(motor.transform, motor, follow, 15f);

            driver.Toggle();
            Assert.IsFalse(driver.IsLocked, "사거리(15m) 밖 대상은 락온되지 않아야 한다.");
            Assert.IsNull(follow.LockTarget);
            yield break;
        }

        [UnityTest]
        public IEnumerator 대상_소실시_자동_언락된다()
        {
            MakeMainCamera();
            var (motor, follow) = BuildPlayer();
            var target = MakeTarget(new Vector3(0f, 0f, 5f));
            var driver = new LockOnDriver(motor.transform, motor, follow, 15f);

            driver.Toggle();
            Assert.IsTrue(driver.IsLocked);

            Object.DestroyImmediate(target.gameObject);        // 몬스터 사망/디스폰
            driver.Tick();                                     // 유효성 검사 → 자동 해제

            Assert.IsFalse(driver.IsLocked, "대상이 사라지면 다음 Tick 에서 자동 해제돼야 한다.");
            Assert.IsNull(follow.LockTarget);
            Assert.IsFalse(motor.FacingOverride.HasValue);
            yield break;
        }

        // --- 헬퍼 ---

        private Camera MakeMainCamera()
        {
            var go = new GameObject("MainCamera") { tag = "MainCamera" };
            _objects.Add(go);
            var cam = go.AddComponent<Camera>();
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity; // +Z 바라봄
            return cam;
        }

        private LockOnTarget MakeTarget(Vector3 pos)
        {
            var go = new GameObject("LockTarget");
            go.transform.position = pos;
            _objects.Add(go);
            return go.AddComponent<LockOnTarget>(); // OnEnable 로 레지스트리 등록
        }

        private (CharacterMotor motor, CharacterCameraFollow follow) BuildPlayer()
        {
            var go = new GameObject("Player");
            go.SetActive(false);
            _objects.Add(go);

            go.AddComponent<CharacterController>();
            var motor = go.AddComponent<CharacterMotor>();
            go.AddComponent<CharacterInputBuffer>(); // CharacterCameraFollow 가 ICharacterInputSource 로 참조
            var follow = go.AddComponent<CharacterCameraFollow>();

            var pivot = new GameObject("CamPivot");
            pivot.transform.SetParent(go.transform);
            follow.CinemachineCameraTarget = pivot;

            go.SetActive(true); // Awake 실행
            return (motor, follow);
        }
    }
}
