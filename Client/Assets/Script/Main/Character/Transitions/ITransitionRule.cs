using System;

namespace Game.Main.Character
{
    public interface ITransitionRule
    {
        bool ShouldTransition(float deltaTime);
        Type NextState { get; }
    }
}