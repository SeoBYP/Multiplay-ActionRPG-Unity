using UnityEngine;

namespace Game.Gameplay.Character.Input
{
    public interface ICharacterInputWriter
    {
        void SetMove(Vector2 move);
        void SetLook(Vector2 look);
        void SetSprintHeld(bool value);
        void PressJump();
        void PressDodge();
        void PressInteract();
        void PressAttack();
    }
}
