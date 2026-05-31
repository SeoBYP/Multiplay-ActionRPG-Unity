using UnityEngine;

namespace Game.Gameplay.Character
{
    public class WeaponPickUpInteraction : MonoBehaviour, IInteractable
    {
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
