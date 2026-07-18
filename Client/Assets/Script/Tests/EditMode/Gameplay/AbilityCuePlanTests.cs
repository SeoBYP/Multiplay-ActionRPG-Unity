using System.Collections.Generic;
using Game.Gameplay.Abilities;
using NUnit.Framework;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// CA-5 Phase 1: 어빌리티 연출 타임라인 플래너(<see cref="AbilityCuePlan"/>) 순수 로직 검증.
    /// 재생기(<see cref="Game.Gameplay.Character.AbilityCuePlayer"/>)가 "정렬된 유효 리스트"만 신뢰하도록,
    /// 정규화 규칙(빈 id 제거·음수 클램프·시간 정렬·동시각 저작순 보존)을 여기서 못 박는다.
    /// </summary>
    public class AbilityCuePlanTests
    {
        private static AbilityCueEvent Ev(float t, ECueKind kind, string id, string socket = "")
            => new AbilityCueEvent { timeMs = t, kind = kind, id = id, socket = socket };

        [Test]
        public void 이벤트를_시간_오름차순으로_정렬한다()
        {
            var plan = AbilityCuePlan.Build(new List<AbilityCueEvent>
            {
                Ev(250, ECueKind.Vfx, "spark"),
                Ev(0, ECueKind.Anim, "swing"),
                Ev(120, ECueKind.Sfx, "whoosh"),
            });

            Assert.AreEqual(3, plan.Length);
            Assert.AreEqual(0f, plan[0].timeMs);
            Assert.AreEqual(120f, plan[1].timeMs);
            Assert.AreEqual(250f, plan[2].timeMs);
        }

        [Test]
        public void 빈_id_이벤트는_제거된다()
        {
            // 카탈로그 조회 불가한 이벤트는 재생 의미가 없다 → 재생기까지 흘리지 않는다.
            var plan = AbilityCuePlan.Build(new List<AbilityCueEvent>
            {
                Ev(0, ECueKind.Sfx, ""),
                Ev(100, ECueKind.Sfx, null),
                Ev(200, ECueKind.Vfx, "hit"),
            });

            Assert.AreEqual(1, plan.Length);
            Assert.AreEqual("hit", plan[0].id);
        }

        [Test]
        public void 음수_시간은_0으로_클램프된다()
        {
            // 발동(t=0) 이전 재생은 불가능 — 인스펙터 실수를 재생기가 아니라 플래너가 흡수.
            var plan = AbilityCuePlan.Build(new List<AbilityCueEvent> { Ev(-50, ECueKind.Sfx, "early") });

            Assert.AreEqual(1, plan.Length);
            Assert.AreEqual(0f, plan[0].timeMs);
        }

        [Test]
        public void 같은_시각이면_저작_순서를_보존한다()
        {
            // 동시각 VFX+SFX 를 겹칠 때 의도한 재생 순서(저작 순서)가 유지돼야 한다(안정 정렬).
            var plan = AbilityCuePlan.Build(new List<AbilityCueEvent>
            {
                Ev(100, ECueKind.Vfx, "first"),
                Ev(100, ECueKind.Sfx, "second"),
                Ev(100, ECueKind.Vfx, "third"),
            });

            Assert.AreEqual(3, plan.Length);
            Assert.AreEqual("first", plan[0].id);
            Assert.AreEqual("second", plan[1].id);
            Assert.AreEqual("third", plan[2].id);
        }

        [Test]
        public void 길이는_보존되고_음수는_0으로_클램프된다()
        {
            // durationMs = 윈도우 이벤트(P6). 재생기(VFX 수명)가 신뢰하도록 플래너가 정규화.
            var plan = AbilityCuePlan.Build(new List<AbilityCueEvent>
            {
                new AbilityCueEvent { timeMs = 100, durationMs = 250, kind = ECueKind.Vfx, id = "beam" },
                new AbilityCueEvent { timeMs = 200, durationMs = -30, kind = ECueKind.Sfx, id = "hum" },
            });

            Assert.AreEqual(250f, plan[0].durationMs, "길이는 그대로 보존");
            Assert.AreEqual(0f, plan[1].durationMs, "음수 길이는 0(즉발)로 클램프");
        }

        [Test]
        public void null_이거나_빈_리스트는_빈_배열이다()
        {
            Assert.AreEqual(0, AbilityCuePlan.Build(null).Length);
            Assert.AreEqual(0, AbilityCuePlan.Build(new List<AbilityCueEvent>()).Length);
        }

        [Test]
        public void 원본_리스트를_변경하지_않는다()
        {
            // 플래너는 새 배열을 반환한다 — 저작 SO 를 훼손하면 안 됨(음수 클램프가 원본에 새면 다음 조회가 오염).
            var src = new List<AbilityCueEvent> { Ev(-10, ECueKind.Sfx, "a") };
            AbilityCuePlan.Build(src);
            Assert.AreEqual(-10f, src[0].timeMs, "원본은 그대로여야 한다");
        }
    }
}
