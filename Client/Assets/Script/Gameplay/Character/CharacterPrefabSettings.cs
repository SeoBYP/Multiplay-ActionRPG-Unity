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
        /// <summary>Dungeon 씬에서만 사용. GroundItemSpawner가 서버 권위 드랍(바닥 아이템)을 스폰. Main 씬은 null.</summary>
        public GameObject GroundItemPrefab { get; }

        public CharacterPrefabSettings(GameObject localPlayerPrefab, GameObject remotePlayerPrefab = null, GameObject monsterPrefab = null, GameObject groundItemPrefab = null)
        {
            LocalPlayerPrefab  = localPlayerPrefab;
            RemotePlayerPrefab = remotePlayerPrefab;
            MonsterPrefab      = monsterPrefab;
            GroundItemPrefab   = groundItemPrefab;
        }
    }
}
