using System;

namespace Game.Main.Character
{
    [Serializable]
    public class StateDefinition
    {
        public StateKind Kind;
        public float Duration;
        public float InvokeDelay;
    }
}