using Game.Gameplay.Character;
using Game.Gameplay.Monster;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 저작 카탈로그(스탯) ↔ 표시 카탈로그(프리팹) 사이가 끊기지 않았는지 지키는 가드.
    ///
    /// <para>실제로 새어나간 적이 있다: AC-G 가 변종을 별개 ID(<c>leviathan_boss</c> 등)로 만들고 던전 스폰을
    /// 그 ID 로 재배치했는데 <see cref="MonsterVisualCatalog"/> 에는 안 넣어서, 스포너가 조용히 기본 캡슐로
    /// 폴백했다. 모델이 안 뜨니 그 프리팹에 달린 AC-D1 전용 애니(보스 슬램)도 통째로 도달 불가였다.
    /// 폴백이 예외가 아니라 정상 경로라 컴파일·기존 테스트로는 잡히지 않는다 → 데이터 대조로 잡는다.</para>
    /// </summary>
    public class MonsterVisualCatalogTests
    {
        private const string StatCatalogPath   = "Assets/GameData/Monster/MonsterCatalogDefinition.asset";
        private const string VisualCatalogPath = "Assets/GameData/Monster/MonsterVisualCatalog.asset";

        /// <summary>서버 테스트 픽스처(test_arena) 전용 — 클라 표시 대상이 아니라 의도적으로 제외한다.</summary>
        private const string FixtureOnlyMonsterId = "test_brute";

        private static T Load<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"{path} 로드 실패");
            return asset;
        }

        [Test]
        public void 저작된_모든_몬스터_ID_는_표시_프리팹이_등록돼_있다()
        {
            var stats   = Load<MonsterCatalogDefinition>(StatCatalogPath);
            var visuals = Load<MonsterVisualCatalog>(VisualCatalogPath);

            foreach (var m in stats.monsters)
            {
                if (m == null || m.monsterId == FixtureOnlyMonsterId) continue;

                Assert.IsNotNull(
                    visuals.GetPrefab(m.monsterId),
                    $"'{m.monsterId}' 가 MonsterVisualCatalog 에 없다 → MonsterSpawner 가 기본 캡슐로 폴백한다. " +
                    "변종을 추가했다면 표시 프리팹도 함께 등록할 것.");
            }
        }

        [Test]
        public void 보스_슬램은_프리팹_트리거명과_컨트롤러_파라미터가_이어져_있다()
        {
            var visuals = Load<MonsterVisualCatalog>(VisualCatalogPath);

            var prefab = visuals.GetPrefab("leviathan_boss");
            Assert.IsNotNull(prefab, "leviathan_boss 표시 프리팹 미등록");

            var anims = prefab.GetComponentInChildren<CharacterAgentAnimations>(true);
            Assert.IsNotNull(anims, "프리팹에 CharacterAgentAnimations 없음");

            // 트리거명은 private [SerializeField] — 프리팹에 저장된 실제 값을 그대로 읽는다.
            var triggerName = new SerializedObject(anims)
                .FindProperty("m_animationAttackSpecialTrigger").stringValue;
            Assert.IsFalse(
                string.IsNullOrEmpty(triggerName),
                "m_animationAttackSpecialTrigger 가 비어 있으면 SetTrigger 가 조용히 스킵된다(슬램이 평타처럼 보임)");

            var animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator, "프리팹에 Animator 없음");
            var controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.IsNotNull(controller, "AnimatorController 미할당");

            var hasTrigger = false;
            foreach (var p in controller.parameters)
                if (p.name == triggerName && p.type == AnimatorControllerParameterType.Trigger)
                    hasTrigger = true;

            Assert.IsTrue(
                hasTrigger,
                $"컨트롤러 '{controller.name}' 에 Trigger 파라미터 '{triggerName}' 가 없다 — 프리팹 트리거명과 불일치");
        }
    }
}
