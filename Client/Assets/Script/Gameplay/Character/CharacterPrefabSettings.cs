using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// CharacterSpawner가 사용할 프리팹 참조.
    /// LifetimeScope(MonoBehaviour)의 SerializeField에서 Inspector 연결 후 DI에 등록한다.
    /// </summary>
    public sealed class CharacterPrefabSettings
    {
        public GameObject LocalPlayerPrefab  { get; }
        /// <summary>Dungeon 씬에서만 사용. Main 씬은 null.</summary>
        public GameObject RemotePlayerPrefab { get; }

        public CharacterPrefabSettings(GameObject localPlayerPrefab, GameObject remotePlayerPrefab = null)
        {
            LocalPlayerPrefab  = localPlayerPrefab;
            RemotePlayerPrefab = remotePlayerPrefab;
        }
    }
}
