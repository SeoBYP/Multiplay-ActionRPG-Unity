using UnityEngine;

namespace Game.Main.Character
{
    public interface IInteractable
    {
        void Interact(GameObject interactor);
    }
}