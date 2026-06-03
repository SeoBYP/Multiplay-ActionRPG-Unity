using UnityEngine;

namespace Game.Gameplay.Spawn
{
    /// <summary>
    /// 맵 에디터(프리뷰 씬)에서 플레이어 스폰 지점을 시각적으로 배치하는 마커. **저작 전용**(런타임 미사용).
    ///
    /// Transform = 스폰 위치 + Y축 회전. MapEditorWindow 가 'Spawns' 부모 아래 sibling 순서를
    /// SpawnIndex 로 읽어 MapDefinition.playerSpawns 에 write-back 한다.
    /// (인덱스 라벨 Gizmo 는 Editor 어셈블리에서 DrawGizmo 로 그린다 — 런타임은 UnityEditor 참조 금지.)
    /// </summary>
    public sealed class SpawnPointMarker : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            var pos = transform.position;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawSphere(pos, 0.35f);

            // 바라보는 방향 화살표 (rotationY)
            Gizmos.color = Color.yellow;
            var fwd   = transform.forward;
            var right = transform.right;
            var tip   = pos + fwd * 1.2f;
            Gizmos.DrawLine(pos, tip);
            Gizmos.DrawLine(tip, tip - fwd * 0.3f + right * 0.2f);
            Gizmos.DrawLine(tip, tip - fwd * 0.3f - right * 0.2f);
        }
    }
}
