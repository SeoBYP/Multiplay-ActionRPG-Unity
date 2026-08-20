namespace Game.Gameplay.Character
{
    public enum StateKind
    {
        Ground = 0,
        Jump = 1,
        Fall = 2,
        Land = 3,
        Climb = 4, // P6 사다리. ⚠️ SO(CharacterStateConfig)에 정수로 직렬화 — 새 값은 항상 끝에.
    }
}