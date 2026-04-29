using System;

namespace Game.Main.Character
{
    public interface ITransitionRule
    {
        bool ShouldTransition(float deltaTime);
        StateKind NextState { get; }
    }
}