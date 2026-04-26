namespace Game.Main.Character
{
    public interface IActiveInteractable: IInteractable
    {
        bool IsInteractionActive { get; }
    }
}