namespace Game.Gameplay.Character
{
    public interface IActiveInteractable: IInteractable
    {
        bool IsInteractionActive { get; }
    }
}