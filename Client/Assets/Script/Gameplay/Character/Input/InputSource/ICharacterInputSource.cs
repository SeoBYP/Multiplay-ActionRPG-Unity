namespace Game.Gameplay.Character.Input
{
    public interface ICharacterInputSource
    {
        CharacterInputFrame Current { get; }
        
        bool ConsumeJumpPressed();
        bool ConsumeDodgePressed();
        bool ConsumeInteractPressed();
        bool ConsumeAttackPressed();
        bool ConsumeHeavyAttackPressed();
        bool ConsumeLockOnPressed();
    }
}
