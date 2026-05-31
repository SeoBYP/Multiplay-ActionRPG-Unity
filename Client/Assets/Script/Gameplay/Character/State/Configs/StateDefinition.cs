using System;

namespace Game.Gameplay.Character
{
    [Serializable]
    public class StateDefinition
    {
        public StateKind Kind;
        public float Duration;
        public float InvokeDelay;
    }
}