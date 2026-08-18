using UnityEngine;

namespace MagicPigGames.Shared
{
    /*
     * Common base class for all Magic Pig Games MonoBehaviours.
     *
     * Custom inspectors (such as the Documentation-header editor in Projectile Factory) target this
     * type with editForChildClasses = true, INSTEAD of registering a fallback editor for all of
     * MonoBehaviour -- which hijacked the inspector for every component in the user's project.
     *
     * The class is intentionally empty: it adds no serialized fields, so reparenting an existing
     * MonoBehaviour to it does not change scene/prefab serialization in any way.
     */
    public abstract class MagicPigMonoBehaviour : MonoBehaviour
    {
    }
}
