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
        /// <summary>Dungeon 씬에서만 사용. MonsterSpawner가 서버 권위 몬스터를 스폰. Main 씬은 null.</summary>
        public GameObject MonsterPrefab { get; }

        public CharacterPrefabSettings(GameObject localPlayerPrefab, GameObject remotePlayerPrefab = null, GameObject monsterPrefab = null)
        {
            LocalPlayerPrefab  = localPlayerPrefab;
            RemotePlayerPrefab = remotePlayerPrefab;
            MonsterPrefab      = monsterPrefab;
        }
    }
}
