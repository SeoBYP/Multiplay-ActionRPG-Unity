namespace Game.Main.Character
{
    public interface IStateMachineBuilder
    {
        StateKind GetInitialState(CharacterStateConfig config);
        bool TryCreateState(
            StateKind stateKind,
            CharacterStateConfig config,
            CharacterStateContext context,
            out State state);
    }
}
