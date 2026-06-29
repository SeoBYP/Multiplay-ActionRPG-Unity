using UnityEngine;

namespace Game.Gameplay.Character.Input
{
    public class CharacterInputBuffer :MonoBehaviour, ICharacterInputSource, ICharacterInputWriter
    {
        public CharacterInputFrame Current { get; private set; } = CharacterInputFrame.Empty;
        
        public bool ConsumeJumpPressed()
        {
            if (!Current.JumpPressed)
                return false;

            Current = Current.WithJump(false);
            return true;
        }

        public bool ConsumeDodgePressed()
        {
            if (!Current.DodgePressed)
                return false;

            Current = Current.WithDodge(false);
            return true;
        }

        public bool ConsumeInteractPressed()
        {
            if (!Current.InteractPressed)
                return false;

            Current = Current.WithInteract(false);
            return true;
        }

        public bool ConsumeAttackPressed()
        {
            if (!Current.AttackPressed)
                return false;

            Current = Current.WithAttack(false);
            return true;
        }

        public bool ConsumeHeavyAttackPressed()
        {
            if (!Current.HeavyAttackPressed)
                return false;

            Current = Current.WithHeavyAttack(false);
            return true;
        }

        public void SetMove(Vector2 move)
        {
            Current = Current.WithMove(move);
        }

        public void SetLook(Vector2 look)
        {
            Current = Current.WithLook(look);
        }

        public void SetSprintHeld(bool value)
        {
            Current = Current.WithSprint(value);
        }

        public void PressJump()
        {
            Current = Current.WithJump(true);
        }

        public void PressDodge()
        {
            Current = Current.WithDodge(true);
        }

        public void PressInteract()
        {
            Current = Current.WithInteract(true);
        }

        public void PressAttack()
        {
            Current = Current.WithAttack(true);
        }

        public void PressHeavyAttack()
        {
            Current = Current.WithHeavyAttack(true);
        }

        public bool ConsumeLockOnPressed()
        {
            if (!Current.LockOnPressed)
                return false;

            Current = Current.WithLockOn(false);
            return true;
        }

        public void PressLockOn()
        {
            Current = Current.WithLockOn(true);
        }
    }
}
