using UnityEngine;

namespace Game.Gameplay.Spawn
{
    /// <summary>
    /// 맵 에디터(프리뷰 씬)에서 몬스터 스폰 지점을 배치하는 마커. **저작 전용**(런타임 미사용).
    ///
    /// Transform = 스폰 위치 + Y축 회전. 자식 <see cref="PatrolPointMarker"/> 의 sibling 순서 = 패트롤 경로.
    /// MapEditorWindow 가 'Monsters' 부모 아래에서 이 마커들을 읽어 MapDefinition.monsterSpawns 에 write-back 한다.
    /// </summary>
    public sealed class MonsterSpawnMarker : MonoBehaviour
    {
        [Tooltip("몬스터 타입 키(서버·클라 공용 식별자).")]
        public string monsterId = "creepy_demon";

        [Tooltip("이 지점에서 동시에 스폰할 수.")]
        public int count = 1;

        [Tooltip("웨이브 인덱스(0=시작 시).")]
        public int wave;

        [Tooltip("Main B-lite 클레임 키(슬롯 안정 식별자, 1부터). 0=클레임 불가. 던전은 미사용.")]
        public int slotId;

        [Tooltip("Main B-lite 재청구·재스폰 쿨다운(ms) = 파밍률 상한. 던전은 미사용(0).")]
        public int respawnCooldownMs;

        private void OnDrawGizmos()
        {
            var pos = transform.position;

            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            Gizmos.DrawSphere(pos, 0.4f);

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
