using UnityEngine;

namespace Game.Gameplay.Spawn
{
    /// <summary>
    /// 맵 경계(XZ 사각형) 저작 마커. **저작 전용**(런타임 미사용).
    /// Transform.position = 경계 중심(X,Z 사용), sizeX/sizeZ = 가로/세로.
    /// MapEditorWindow 가 MapDefinition.bounds 에 write-back 한다. 서버가 몬스터 이동을 이 경계로 clamp.
    /// </summary>
    public sealed class MapBoundsMarker : MonoBehaviour
    {
        [Tooltip("경계 가로(X) 길이.")]
        public float sizeX = 40f;

        [Tooltip("경계 세로(Z) 길이.")]
        public float sizeZ = 40f;

        private void OnDrawGizmos()
        {
            var c = transform.position;

            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.85f);
            Gizmos.DrawWireCube(new Vector3(c.x, c.y, c.z), new Vector3(sizeX, 0.1f, sizeZ));
        }
    }
}
