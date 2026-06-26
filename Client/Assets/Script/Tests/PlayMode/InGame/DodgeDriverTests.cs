using System.Collections;
using System.Collections.Generic;
using Game.Gameplay.Character;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// 2.6.1 회피(Dodge) 드라이버 — 무적 태그 수명·쿨다운·종료를 결정론 dt 로 격리 검증.
    /// 무적창/쿨다운 수치 = Shared <see cref="DodgeConfig"/>(서버와 동일 단일 소스).
    /// </summary>
    public class DodgeDriverTests
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
        public IEnumerator 회피는_무적태그를_세우고_iframe_만료시_해제하고_쿨다운을_건다()
        {
            var (motor, asc, anims) = BuildRig();
            var settings = new LocomotionSettings(); // DodgeDuration=0.5, DodgeConfig.IframeMs=500, CooldownMs=1000
            var driver = new DodgeDriver(motor, asc, anims, settings);

            Assert.IsTrue(driver.CanBegin(0f));

            driver.Begin(Vector3.forward, 0f);
            Assert.IsTrue(asc.HasTag(GameplayTags.Invulnerable), "회피 시작 시 무적 태그가 세워져야 한다.");
            Assert.IsTrue(driver.IsActive);
            Assert.IsFalse(driver.CanBegin(0.5f), "쿨다운(1s) 내에는 재발동 불가.");

            // i-frame(0.5s) 만료 전 — 태그 유지
            driver.Tick(0.3f);
            Assert.IsTrue(asc.HasTag(GameplayTags.Invulnerable));
            Assert.IsTrue(driver.IsActive);

            // i-frame·대시(0.5s) 경과 — 태그 해제 + 제어 반환
            driver.Tick(0.3f); // 누적 0.6 ≥ 0.5
            Assert.IsFalse(asc.HasTag(GameplayTags.Invulnerable), "i-frame 만료 후 무적이 해제돼야 한다.");
            Assert.IsFalse(driver.IsActive, "대시·무적 종료 후 제어를 반환해야 한다.");

            Assert.IsTrue(driver.CanBegin(1.0f), "쿨다운(1s) 경과 후 재발동 가능.");
            yield break;
        }

        [UnityTest]
        public IEnumerator Cancel은_진행중_무적을_즉시_해제한다()
        {
            var (motor, asc, anims) = BuildRig();
            var driver = new DodgeDriver(motor, asc, anims, new LocomotionSettings());

            driver.Begin(Vector3.forward, 0f);
            Assert.IsTrue(asc.HasTag(GameplayTags.Invulnerable));

            driver.Cancel();
            Assert.IsFalse(asc.HasTag(GameplayTags.Invulnerable), "Cancel 시 무적 태그가 즉시 해제돼야 한다.");
            Assert.IsFalse(driver.IsActive);
            yield break;
        }

        [UnityTest]
        public IEnumerator 넉백은_방향을_적용하고_지속후_종료된다()
        {
            var (motor, _, _) = BuildRig();
            var kb = new KnockbackDriver(motor);

            Assert.IsFalse(kb.IsActive);
            kb.Begin(Vector3.forward, 2f, 0.4f);
            Assert.IsTrue(kb.IsActive);

            kb.Tick(0.1f);
            // 밀림 방향(+z)이 Motor 에 적용됐는지 — 회전 없이 변위만(faceDirection=false).
            Assert.Greater(motor.DesiredMoveDirection.z, 0.9f);
            Assert.IsTrue(kb.IsActive);

            kb.Tick(0.4f); // 누적 0.5 ≥ 0.4 → 종료
            Assert.IsFalse(kb.IsActive);
            yield break;
        }

        [UnityTest]
        public IEnumerator 무효_넉백입력은_무시된다()
        {
            var (motor, _, _) = BuildRig();
            var kb = new KnockbackDriver(motor);

            kb.Begin(Vector3.zero, 2f, 0.4f); // 방향 0 → 무시
            Assert.IsFalse(kb.IsActive);
            kb.Begin(Vector3.forward, 2f, 0f); // 시간 0 → 무시
            Assert.IsFalse(kb.IsActive);
            yield break;
        }

        private (CharacterMotor motor, AbilitySystemComponent asc, CharacterAgentAnimations anims) BuildRig()
        {
            var go = new GameObject("DodgeRig");
            go.SetActive(false);
            _objects.Add(go);

            go.AddComponent<CharacterController>();
            var motor = go.AddComponent<CharacterMotor>();
            var asc = go.AddComponent<AbilitySystemComponent>();
            var anims = go.AddComponent<CharacterAgentAnimations>();

            go.SetActive(true); // Awake 실행(motor 가 CharacterController 캐시)
            return (motor, asc, anims);
        }
    }
}
