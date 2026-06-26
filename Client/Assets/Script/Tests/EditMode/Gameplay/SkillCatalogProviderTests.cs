using System.Collections.Generic;
using Game.Gameplay.Abilities;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// 2.2 스킬 데이터 자산화(클라) — SkillDefinition SO → SkillCatalogProvider → SkillTimeline 조회.
    /// 서버는 같은 데이터를 bake skills.json 으로 읽는다(데이터 진실원=SO, gas-architecture §2.5).
    /// </summary>
    public class SkillCatalogProviderTests
    {
        private readonly List<Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects)
                if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        [Test]
        public void 카탈로그_스킬을_SkillTimeline로_변환_조회한다()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            _objects.Add(skill);
            skill.id = "test_skill";
            skill.startupMs = 100; skill.activeMs = 50; skill.recoveryMs = 50; skill.cooldownMs = 700;
            skill.hitboxShape = EHitboxShape.Sphere;
            skill.hitboxHalfExtents = new Vector3(1.5f, 0f, 0f);
            skill.onHitEffectIds = new List<string> { "basic_attack_dmg" };

            var cat = ScriptableObject.CreateInstance<SkillCatalogDefinition>();
            _objects.Add(cat);
            cat.skills = new List<SkillDefinition> { skill };

            var provider = new SkillCatalogProvider(cat);

            var t = provider.Get("test_skill");
            Assert.IsNotNull(t, "등록된 스킬은 SkillTimeline 으로 조회돼야 한다.");
            Assert.AreEqual(700, t.CooldownMs);
            Assert.AreEqual(EHitboxShape.Sphere, t.Hitbox.Shape);
            Assert.That(t.OnHitEffectIds, Does.Contain("basic_attack_dmg"));

            Assert.IsNull(provider.Get("does_not_exist"), "미등록 id 는 null.");
        }

        [Test]
        public void 빈_카탈로그도_안전하게_동작한다()
        {
            var provider = new SkillCatalogProvider(null);
            Assert.IsNull(provider.Get("anything"));
        }
    }
}
