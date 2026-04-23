namespace Game.Main.Character.Input
{
    public interface ICharacterInputSource
    {
        CharacterInputFrame Current { get; }
        
        bool ConsumeJumpPressed();
        bool ConsumeDodgePressed();
        bool ConsumeInteractPressed();
    }
}