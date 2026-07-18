using Game.Gameplay.Character;
using Game.Network.Socket;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// AC 증분4: Actor 통합 발동 라우팅(actor-combat-architecture §5.3) 순수 로직 검증.
    /// ActorRegistry(ActorId→IActorView 조회) + AbilityCueRouter(S_AbilityActivated → 해당 뷰 Cue) 배선을 못 박는다.
    /// </summary>
    public class ActorCombatRoutingTests
    {
        private sealed class FakeActorView : IActorView
        {
            public int CueCount;
            public int CuesCount; // PlayAbilityCues(타임라인) 호출 수
            public AnimationTriggerType LastTrigger = AnimationTriggerType.None;
            public int LastComboStep = -999;
            public void PlayAbilityCue(AnimationTriggerType trigger, int comboStep)
            {
                CueCount++; LastTrigger = trigger; LastComboStep = comboStep;
            }
            public void PlayAbilityCues(Game.Gameplay.Abilities.AbilityDefinition ability) => CuesCount++;
        }

        // ── ActorRegistry ──

        [Test]
        public void 등록한_ActorId는_TryGet으로_조회된다()
        {
            var reg = new ActorRegistry();
            var view = new FakeActorView();
            long actorId = ActorIds.FromMonster(7); // -7

            reg.Register(actorId, view);

            Assert.IsTrue(reg.TryGet(actorId, out var found));
            Assert.AreSame(view, found);
            Assert.AreEqual(1, reg.Count);
        }

        [Test]
        public void Unregister하면_더이상_조회되지_않는다()
        {
            var reg = new ActorRegistry();
            long actorId = ActorIds.FromMonster(3);
            reg.Register(actorId, new FakeActorView());

            reg.Unregister(actorId);

            Assert.IsFalse(reg.TryGet(actorId, out _));
            Assert.AreEqual(0, reg.Count);
        }

        [Test]
        public void 같은_ActorId_재등록은_덮어쓴다()
        {
            var reg = new ActorRegistry();
            long actorId = ActorIds.FromMonster(1);
            var first = new FakeActorView();
            var second = new FakeActorView();

            reg.Register(actorId, first);
            reg.Register(actorId, second); // 재스폰 시나리오

            reg.TryGet(actorId, out var found);
            Assert.AreSame(second, found);
            Assert.AreEqual(1, reg.Count);
        }

        // ── AbilityCueRouter ──

        [Test]
        public void 발동신호는_해당_ActorId의_뷰에만_Cue를_재생한다()
        {
            var state = new SocketPacketState();
            var reg = new ActorRegistry();
            var monster = new FakeActorView();
            var other = new FakeActorView();
            long monsterActor = ActorIds.FromMonster(5); // -5
            reg.Register(monsterActor, monster);
            reg.Register(ActorIds.FromMonster(6), other);

            var router = new AbilityCueRouter(state, reg, abilities: null);
            router.Initialize(); // OnAbilityActivated 구독

            state.NotifyAbilityActivated(monsterActor, skillId: 0); // 서버 브로드캐스트 시뮬

            Assert.AreEqual(1, monster.CueCount, "대상 몬스터만 Cue 재생");
            Assert.AreEqual(1, monster.CuesCount, "SFX/VFX 타임라인 경로(PlayAbilityCues)도 대상 뷰에 위임");
            Assert.AreEqual(AnimationTriggerType.Attack, monster.LastTrigger, "카탈로그 미제공 → 기본 공격 Cue 폴백");
            Assert.AreEqual(0, monster.LastComboStep);
            Assert.AreEqual(0, other.CueCount, "다른 액터는 재생 안 됨");
            Assert.AreEqual(0, other.CuesCount, "다른 액터는 타임라인도 재생 안 됨");

            router.Dispose();
        }

        [Test]
        public void 미등록_ActorId_발동신호는_예외없이_무시된다()
        {
            var state = new SocketPacketState();
            var reg = new ActorRegistry();
            var router = new AbilityCueRouter(state, reg, abilities: null);
            router.Initialize();

            // 아직 스폰 전/이미 디스폰 = 미등록. 예외 없이 no-op 이어야 한다.
            Assert.DoesNotThrow(() => state.NotifyAbilityActivated(ActorIds.FromMonster(99), 0));

            router.Dispose();
        }

        [Test]
        public void Dispose후에는_구독해제되어_Cue가_재생되지_않는다()
        {
            var state = new SocketPacketState();
            var reg = new ActorRegistry();
            var view = new FakeActorView();
            long actorId = ActorIds.FromMonster(2);
            reg.Register(actorId, view);

            var router = new AbilityCueRouter(state, reg, abilities: null);
            router.Initialize();
            router.Dispose(); // 구독 해제

            state.NotifyAbilityActivated(actorId, 0);

            Assert.AreEqual(0, view.CueCount, "Dispose 후 신호는 라우팅되지 않아야 한다");
        }
    }
}
