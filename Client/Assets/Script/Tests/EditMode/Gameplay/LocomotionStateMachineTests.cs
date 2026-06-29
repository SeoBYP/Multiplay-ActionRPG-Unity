using System.Collections.Generic;
using Game.Gameplay;
using Game.Gameplay.Character;
using Game.Gameplay.Character.Input;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Gameplay
{
    /// <summary>
    /// CA-1 회귀: Action 축(Attack/Interact)을 들어낸 뒤에도 Locomotion FSM이
    /// 정상적으로 상태를 생성하는지. (StateKind.Attack 제거가 컴파일·생성 경로를 깨지 않음)
    /// </summary>
    public class LocomotionStateMachineTests
    {
        [Test]
        public void Locomotion_상태들이_빌더로_생성된다()
        {
            var config = ScriptableObject.CreateInstance<CharacterStateConfig>();
            config.InitialState = StateKind.Ground;
            config.States = new List<StateDefinition>
            {
                new() { Kind = StateKind.Ground },
                new() { Kind = StateKind.Jump },
                new() { Kind = StateKind.Fall },
                new() { Kind = StateKind.Land },
            };

            var context = new CharacterStateContext
            {
                InputSource = new FakeInputSource(),
                LocomotionSettings = new LocomotionSettings(),
            };

            var builder = new StateMachineBuilder(new StateFactory());

            Assert.AreEqual(StateKind.Ground, builder.GetInitialState(config));

            foreach (var kind in new[] { StateKind.Ground, StateKind.Jump, StateKind.Fall, StateKind.Land })
            {
                bool created = builder.TryCreateState(kind, config, context, out var state);
                Assert.IsTrue(created && state != null, $"{kind} 상태 생성 실패");
            }

            Object.DestroyImmediate(config);
        }

        private sealed class FakeInputSource : ICharacterInputSource
        {
            public CharacterInputFrame Current => default;
            public bool ConsumeJumpPressed() => false;
            public bool ConsumeDodgePressed() => false;
            public bool ConsumeInteractPressed() => false;
            public bool ConsumeAttackPressed() => false;
            public bool ConsumeHeavyAttackPressed() => false;
            public bool ConsumeLockOnPressed() => false;
        }
    }
}
