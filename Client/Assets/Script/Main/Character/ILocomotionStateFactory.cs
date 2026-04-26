namespace Game.Main.Character
{
    public interface ILocomotionStateFactory
    {
        State Create(System.Type stateType, CharacterLocomotionContext context);
    }

}