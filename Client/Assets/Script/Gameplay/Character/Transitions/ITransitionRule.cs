using System;

namespace Game.Gameplay.Character
{
    public interface ITransitionRule
    {
        bool ShouldTransition(float deltaTime);
        StateKind NextState { get; }
    }
}