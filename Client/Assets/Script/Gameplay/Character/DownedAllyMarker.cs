using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 던전(서버 권위)에서 <b>다운된 원격 아군</b>을 표시하는 마커(Co-op 2.5.2 부활 대상).
    ///
    /// 기존 2.5.1 에선 원격 다운 = 캐릭터 Destroy 였지만, 부활하려면 다운 아군이 상호작용 대상으로
    /// 남아 있어야 한다 → `CharacterSpawner.HandlePlayerDead` 가 Destroy 대신 이 마커를 부착(다운 보존),
    /// `S_PlayerRevived`/퇴장 시 제거한다. 시전자의 <see cref="ReviveInteractor"/> 가 정적 레지스트리에서
    /// 최근접 다운 아군을 찾는다(물리 레이어 의존 없이). 시각 다운 포즈(Animator)는 클라 발전 시.
    /// </summary>
    public sealed class DownedAllyMarker : MonoBehaviour
    {
        private static readonly List<DownedAllyMarker> _active = new();

        /// <summary>다운된 플레이어의 서버 UserId(부활 대상 식별 → C_Revive.TargetUserId).</summary>
        public long UserId { get; private set; }

        public void Setup(long userId) => UserId = userId;

        private void OnEnable()
        {
            if (!_active.Contains(this)) _active.Add(this);
        }

        private void OnDisable() => _active.Remove(this);

        /// <summary>pos 기준 range(평면) 안의 최근접 다운 아군. 없으면 null.</summary>
        public static DownedAllyMarker FindNearest(Vector3 pos, float range)
        {
            DownedAllyMarker best = null;
            float bestSq = range * range;
            foreach (var m in _active)
            {
                if (m == null) continue;
                var d = m.transform.position - pos;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq <= bestSq)
                {
                    bestSq = sq;
                    best = m;
                }
            }
            return best;
        }
    }
}
