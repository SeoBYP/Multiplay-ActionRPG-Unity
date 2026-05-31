using UnityEngine;

namespace Game.Gameplay.Character
{
    public class WeaponHelper : MonoBehaviour
    {
        [SerializeField]
        private GameObject m_weapon;

        public bool HasWeapon { get; set; }

        public void ToggleWeapon(bool val)
        {
            m_weapon.SetActive(val);
        }
    }
}
