using System.Collections;
using Game.Gameplay.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// Main(B-lite) 몬스터 체력바 — 실제 프리팹으로 검증한다.
    ///
    /// 던전(MonsterEntity)과 Main(LocalMonster)은 **HP 권위가 다르다**(서버 vs 클라). 체력바는 그 차이를
    /// 몰라도 되게 <see cref="IMonsterHealth"/> 계약만 본다 — 이 테스트가 그 계약이 Main 에서 실제로
    /// 관통하는지(프리팹 배선 포함) 고정한다.
    /// </summary>
    public class LocalMonsterHealthBarTests
    {
        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.Destroy(_instance);
            _instance = null;
        }

        private static GameObject LoadPrefab()
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Character/Monster/CreepyDemonLocal.prefab");
#endif
            Assume.That(prefab, Is.Not.Null, "CreepyDemonLocal 프리팹 로드 실패(에디터 외 실행)");
            return prefab;
        }

        [UnityTest]
        public IEnumerator Main_몬스터_프리팹에_체력바가_배선되어_있다()
        {
            // 프리팹에 체력바가 없으면 코드가 아무리 맞아도 화면엔 안 보인다.
            _instance = Object.Instantiate(LoadPrefab());
            yield return null;

            var bar = _instance.GetComponentInChildren<MonsterHealthBar>(true);
            Assert.IsNotNull(bar, "Main 몬스터 프리팹에 MonsterHealthBar 가 없다");

            var health = _instance.GetComponent<IMonsterHealth>();
            Assert.IsNotNull(health, "LocalMonster 가 IMonsterHealth 를 구현해야 체력바가 붙는다");
        }

        [UnityTest]
        public IEnumerator 피격하면_체력바_fill_이_줄어든다()
        {
            _instance = Object.Instantiate(LoadPrefab());
            yield return null; // Awake → _hp = maxHp

            var monster = _instance.GetComponent<LocalMonster>();
            var fill = FindFill(_instance);
            Assume.That(fill, Is.Not.Null, "체력바 fill(Image) 미배선");
            Assume.That(monster.Hp, Is.GreaterThan(0), "선행 조건: 초기 HP");

            float before = fill.fillAmount;
            monster.TakeDamage(monster.MaxHp / 4);
            yield return null;

            Assert.Less(fill.fillAmount, before, "피격 시 fill 이 줄어야 한다");
            Assert.AreEqual(monster.Hp / (float)monster.MaxHp, fill.fillAmount, 0.01f);
        }

        [UnityTest]
        public IEnumerator 사망하면_체력바가_0이_된다()
        {
            // 던전에서 고쳤던 그 버그(체력바가 남은 채 죽는 모션)가 Main 에서 재발하지 않게 고정한다.
            _instance = Object.Instantiate(LoadPrefab());
            yield return null;

            var monster = _instance.GetComponent<LocalMonster>();
            var fill = FindFill(_instance);
            Assume.That(fill, Is.Not.Null);

            monster.TakeDamage(monster.MaxHp * 10); // 확실히 죽인다
            yield return null;

            Assert.IsTrue(monster.IsDead);
            Assert.AreEqual(0, monster.Hp, "사망 시 HP 는 0 으로 확정돼야 한다(음수 노출 금지)");
            Assert.AreEqual(0f, fill.fillAmount, 0.001f, "체력바가 남은 채 죽는 모션이 나오면 안 된다");
        }

        private static Image FindFill(GameObject root)
        {
            var bar = root.GetComponentInChildren<MonsterHealthBar>(true);
            if (bar == null) return null;
            foreach (var img in bar.GetComponentsInChildren<Image>(true))
                if (img.gameObject.name == "Fill") return img;
            return null;
        }
    }
}
