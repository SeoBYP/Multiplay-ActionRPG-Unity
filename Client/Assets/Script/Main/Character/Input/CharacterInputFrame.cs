using UnityEngine;

namespace Game.Main.Character.Input
{
    public readonly struct CharacterInputFrame
    {
        public Vector2 Move { get; }
        public Vector2 Look { get; }
        public bool SprintHeld { get; }
        public bool JumpPressed { get; }
        public bool DodgePressed { get; }
        public bool InteractPressed { get; }

        public CharacterInputFrame(Vector2 move, Vector2 look, bool sprintHeld, bool jumpPressed, bool dodgePressed,
            bool interactPressed)
        {
            Move = move;
            Look = look;
            SprintHeld = sprintHeld;
            JumpPressed = jumpPressed;
            DodgePressed = dodgePressed;
            InteractPressed = interactPressed;
        }
        
        public CharacterInputFrame WithMove(Vector2 move) =>
            new(move, Look, SprintHeld, JumpPressed, DodgePressed, InteractPressed);

        public CharacterInputFrame WithLook(Vector2 lockDirection) =>
            new(Move, lockDirection, SprintHeld, JumpPressed, DodgePressed, InteractPressed);
        
        public CharacterInputFrame WithSprint(bool sprintHeld) =>
            new(Move, Look, sprintHeld, JumpPressed, DodgePressed, InteractPressed);
        
        public CharacterInputFrame WithJump(bool jumpPressed) =>
            new(Move, Look, SprintHeld, jumpPressed, DodgePressed, InteractPressed);
        
        public CharacterInputFrame WithDodge(bool dodgePressed) =>
            new(Move, Look, SprintHeld, JumpPressed, dodgePressed, InteractPressed);
        
        public CharacterInputFrame WithInteract(bool interactPressed) =>
            new(Move, Look, SprintHeld, JumpPressed, DodgePressed, interactPressed);
        
        public static CharacterInputFrame Empty => new CharacterInputFrame(Vector2.zero, Vector2.zero, false, false, false, false);
    }
}