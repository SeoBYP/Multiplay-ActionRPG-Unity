using UnityEngine;

namespace Game.Gameplay.Character
{
    public interface IInteractable
    {
        void Interact(GameObject interactor);
    }
}