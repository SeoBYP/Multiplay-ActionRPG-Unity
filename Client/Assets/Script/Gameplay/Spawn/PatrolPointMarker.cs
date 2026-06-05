using UnityEngine;

namespace Game.Gameplay.Spawn
{
    /// <summary>
    /// 몬스터 패트롤 경로의 한 지점 마커. **저작 전용**(런타임 미사용).
    /// <see cref="MonsterSpawnMarker"/> 의 자식으로 두며 sibling 순서가 순회 순서다.
    /// 경로를 시각화하려고 이전 지점(또는 스폰 지점)으로 선을 그린다. (M3 증분②b)
    /// </summary>
    public sealed class PatrolPointMarker : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            var pos = transform.position;

            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.9f);
            Gizmos.DrawSphere(pos, 0.22f);

            var parent = transform.parent;
            if (parent == null) return;

            int idx = transform.GetSiblingIndex();
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.5f);

            if (idx > 0)
            {
                var prev = parent.GetChild(idx - 1);
                if (prev.GetComponent<PatrolPointMarker>() != null)
                    Gizmos.DrawLine(prev.position, pos);
            }
            else if (parent.GetComponent<MonsterSpawnMarker>() != null)
            {
                // 첫 경로점은 스폰 지점에서 연결
                Gizmos.DrawLine(parent.position, pos);
            }
        }
    }
}
