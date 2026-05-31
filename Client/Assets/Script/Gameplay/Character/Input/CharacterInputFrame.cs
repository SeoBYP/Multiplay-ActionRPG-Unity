using UnityEngine;

namespace Game.Gameplay.Character.Input
{
    public readonly struct CharacterInputFrame
    {
        public Vector2 Move { get; }
        public Vector2 Look { get; }
        public bool SprintHeld { get; }
        public bool JumpPressed { get; }
        public bool DodgePressed { get; }
        public bool InteractPressed { get; }
        public bool AttackPressed { get; }

        public CharacterInputFrame(Vector2 move, Vector2 look, bool sprintHeld, bool jumpPressed, bool dodgePressed,
            bool interactPressed, bool attackPressed)
        {
            Move = move;
            Look = look;
            SprintHeld = sprintHeld;
            JumpPressed = jumpPressed;
            DodgePressed = dodgePressed;
            InteractPressed = interactPressed;
            AttackPressed = attackPressed;
        }
        
        public CharacterInputFrame WithMove(Vector2 move) =>
            new(move, Look, SprintHeld, JumpPressed, DodgePressed, InteractPressed, AttackPressed);

        public CharacterInputFrame WithLook(Vector2 lockDirection) =>
            new(Move, lockDirection, SprintHeld, JumpPressed, DodgePressed, InteractPressed, AttackPressed);
        
        public CharacterInputFrame WithSprint(bool sprintHeld) =>
            new(Move, Look, sprintHeld, JumpPressed, DodgePressed, InteractPressed, AttackPressed);
        
        public CharacterInputFrame WithJump(bool jumpPressed) =>
            new(Move, Look, SprintHeld, jumpPressed, DodgePressed, InteractPressed, AttackPressed);
        
        public CharacterInputFrame WithDodge(bool dodgePressed) =>
            new(Move, Look, SprintHeld, JumpPressed, dodgePressed, InteractPressed, AttackPressed);
        
        public CharacterInputFrame WithInteract(bool interactPressed) =>
            new(Move, Look, SprintHeld, JumpPressed, DodgePressed, interactPressed, AttackPressed);

        public CharacterInputFrame WithAttack(bool attackPressed) =>
            new(Move, Look, SprintHeld, JumpPressed, DodgePressed, InteractPressed, attackPressed);
        
        public static CharacterInputFrame Empty => new CharacterInputFrame(Vector2.zero, Vector2.zero, false, false, false, false, false);
    }
}
