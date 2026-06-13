using System.Collections.Generic;
using Game.Gameplay.Spawn;
using Game.System.Player;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using VContainer.Unity;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// Main(싱글) 타이머 리스폰 — **Main 스코프에만 등록**(던전은 미등록 → 다운잠금 유지). 2.5.1.
    ///
    /// 로컬 플레이어가 다운(State.Dead)하면 일정 시간 후 부활시킨다. 의도된 비대칭(권위 다름):
    /// 던전=서버권위 다운잠금(클리어/실패까지), Main=클라 로컬 리스폰. death-respawn-2-5-1-direction.
    ///
    /// 부활 = PlayerCharacterAgent.Revive(스폰지점) — Dead 태그 해제 + HP 복구 + 다운 애니 리셋 + 텔레포트.
    /// 스폰 지점 = 해당 맵 레이아웃의 첫 플레이어 스폰 포인트(없으면 원점).
    /// </summary>
    public sealed class LocalRespawnController : ITickable
    {
        private const float RespawnDelaySec = 3f;

        private readonly LocalPlayerContext  _localPlayer;
        private readonly SpawnLayoutProvider _layouts;
        private readonly string              _mapId;

        private float _deadAt = -1f; // 다운 시작 시각(상승엣지). -1 = 생존.

        public LocalRespawnController(LocalPlayerContext localPlayer, SpawnLayoutProvider layouts, MainMonsterSettings settings)
        {
            _localPlayer = localPlayer;
            _layouts     = layouts;
            _mapId       = settings.MapId;
        }

        public void Tick()
        {
            var asc = _localPlayer?.AbilitySystem;
            if (asc == null) return;

            if (!asc.HasTag(GameplayTags.Dead))
            {
                _deadAt = -1f; // 생존(또는 부활 완료) → 타이머 리셋
                return;
            }

            if (_deadAt < 0f)
            {
                _deadAt = Time.time; // 사망 상승엣지
                return;
            }

            if (Time.time - _deadAt < RespawnDelaySec)
                return;

            asc.GetComponent<PlayerCharacterAgent>()?.Revive(ResolveSpawn());
            _deadAt = -1f;
        }

        private Vector3 ResolveSpawn()
        {
            try
            {
                var layout = _layouts.Get(_mapId);
                if (layout.Points.Count > 0)
                {
                    var p = layout.Points[0];
                    return new Vector3(p.X, p.Y, p.Z);
                }
            }
            catch (KeyNotFoundException ex)
            {
                // 맵 미정의 → 원점 폴백.
                Debug.LogWarning($"Map '{_mapId}' is undefined. Falling back to origin.");
            }
            return Vector3.zero;
        }
    }
}
