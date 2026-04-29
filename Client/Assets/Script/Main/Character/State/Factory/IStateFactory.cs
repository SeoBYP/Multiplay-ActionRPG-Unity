namespace Game.Main.Character
{
    public interface IStateFactory
    {
        State Create(StateDefinition definition, CharacterStateContext context);
    }
}
