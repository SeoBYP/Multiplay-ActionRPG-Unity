using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 락온(2.6.3) 가능한 대상 마커. 던전 <see cref="MonsterEntity"/>(서버 권위 보간)·Main
    /// <see cref="LocalMonster"/>(클라 권위)는 서로 다른 클래스지만, 이 마커 하나를 프리팹에 부착해
    /// 정적 레지스트리로 통일한다(물리 레이어 의존 없이 — <see cref="DownedAllyMarker"/> 와 동일 패턴).
    ///
    /// <see cref="LockOnDriver"/> 가 <see cref="FindBest"/> 로 화면 중앙에 가장 가까운 대상을 고른다.
    /// 몬스터가 죽거나(Destroy) 비활성되면 OnDisable 로 레지스트리에서 빠져 자동 락 해제로 이어진다.
    /// </summary>
    public sealed class LockOnTarget : MonoBehaviour
    {
        private static readonly List<LockOnTarget> _active = new();

        [Tooltip("조준점 높이 오프셋(발밑이 아니라 몸 중심을 겨냥). 카메라/facing 이 이 지점을 향한다.")]
        [SerializeField] private float aimHeight = 1.0f;

        /// <summary>카메라·facing 이 겨냥하는 월드 지점(발밑 + aimHeight).</summary>
        public Vector3 AimPoint => transform.position + Vector3.up * aimHeight;

        private void OnEnable()
        {
            if (!_active.Contains(this)) _active.Add(this);
        }

        private void OnDisable() => _active.Remove(this);

        /// <summary>
        /// 화면 중앙에 가장 가까운(=뷰포트 중심 최근접) 락온 대상. 조건: 화면 안 + 카메라 앞 + 평면거리 ≤ maxRange.
        /// 없으면 null. 카메라가 보는 쪽을 우선하는 소울류 타겟 선정(거리 단독이 아닌 화면 기준).
        /// </summary>
        public static LockOnTarget FindBest(UnityEngine.Camera camera, Vector3 playerPos, float maxRange)
        {
            if (camera == null) return null;

            LockOnTarget best = null;
            float bestScreenDist = float.MaxValue;
            float maxRangeSq = maxRange * maxRange;
            var center = new Vector2(0.5f, 0.5f);

            foreach (var t in _active)
            {
                if (t == null) continue;

                var planar = t.transform.position - playerPos;
                planar.y = 0f;
                if (planar.sqrMagnitude > maxRangeSq) continue; // 사거리 밖

                Vector3 vp = camera.WorldToViewportPoint(t.AimPoint);
                if (vp.z <= 0f) continue;                         // 카메라 뒤
                if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue; // 화면 밖

                float screenDist = (new Vector2(vp.x, vp.y) - center).sqrMagnitude;
                if (screenDist < bestScreenDist)
                {
                    bestScreenDist = screenDist;
                    best = t;
                }
            }
            return best;
        }
    }
}
