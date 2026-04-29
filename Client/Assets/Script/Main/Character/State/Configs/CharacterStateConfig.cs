using System.Collections.Generic;
using UnityEngine;

namespace Game.Main.Character
{
    [CreateAssetMenu(fileName = "CharacterStateConfig", menuName = "ScriptableObjects/CharacterStateConfig")]
    public class CharacterStateConfig : ScriptableObject
    {
        public StateKind InitialState = StateKind.Ground;
        public List<StateDefinition> States;
    }
}
