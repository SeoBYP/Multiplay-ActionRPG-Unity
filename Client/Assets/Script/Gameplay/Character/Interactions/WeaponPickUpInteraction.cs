using UnityEngine;

namespace Game.Gameplay.Character
{
    public class WeaponPickUpInteraction : MonoBehaviour, IInteractable
    {
        public string InteractionPrompt => "줍기";

        public void Interact(GameObject interactor)
        {
            WeaponHelper weaponHelper;
            if (weaponHelper = interactor.GetComponent<WeaponHelper>())
            {
                weaponHelper.ToggleWeapon(true);
                weaponHelper.HasWeapon = true;
            }

            Destroy(gameObject);
        }
    }
}
