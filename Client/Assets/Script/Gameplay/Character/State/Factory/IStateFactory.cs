namespace Game.Gameplay.Character
{
    public interface IStateFactory
    {
        State Create(StateDefinition definition, CharacterStateContext context);
    }
}
