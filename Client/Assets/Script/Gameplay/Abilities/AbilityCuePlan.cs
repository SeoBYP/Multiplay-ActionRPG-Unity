using System;
using System.Collections.Generic;

namespace Game.Gameplay.Abilities
{
    /// <summary>
    /// <see cref="AbilityCueEvent"/> 리스트를 <b>재생 가능한 스케줄</b>로 정규화하는 순수 함수(UnityEngine 비의존 → 엔진 없이 테스트).
    ///
    /// 규칙:
    ///   · 빈 <c>id</c> 이벤트 제거(카탈로그 조회 불가 = 무의미).
    ///   · <c>timeMs</c> 음수는 0 으로 클램프(발동 이전 재생 불가).
    ///   · <c>timeMs</c> 오름차순 정렬 → 재생기가 앞에서부터 순차 대기하면 된다.
    ///   · 안정 정렬(같은 시각이면 저작 순서 보존) — VFX 위에 SFX 를 겹쳐도 의도한 순서 유지.
    ///
    /// 저작 데이터를 그대로 실행하지 않고 이 층을 두는 이유: 인스펙터에서 순서가 뒤섞이거나 음수를 넣어도
    /// 재생기가 방어 코드를 갖지 않게(재생기는 "정렬된 유효 리스트"만 신뢰). MonoBehaviour 밖 검증 = 회귀 테스트 용이.
    /// </summary>
    public static class AbilityCuePlan
    {
        private static readonly AbilityCueEvent[] Empty = Array.Empty<AbilityCueEvent>();

        /// <summary>정규화된 스케줄(시간 오름차순). 원본은 변경하지 않고 새 배열을 반환한다.</summary>
        public static AbilityCueEvent[] Build(IReadOnlyList<AbilityCueEvent> events)
        {
            if (events == null || events.Count == 0) return Empty;

            var list = new List<AbilityCueEvent>(events.Count);
            foreach (var e in events)
            {
                if (e == null) continue;
                // 재생 수단이 하나도 없으면 제거(직접 클립/프리팹·카탈로그 id·Event 메서드·Anim 트리거 중 무엇도 없음 = 무의미).
                bool hasPayload = e.sfxClip != null || e.vfxPrefab != null
                                  || !string.IsNullOrEmpty(e.id) || !string.IsNullOrEmpty(e.invokeMethod)
                                  || (int)e.animTrigger != 0; // AnimationTriggerType.None = 0
                if (!hasPayload) continue;
                list.Add(new AbilityCueEvent
                {
                    timeMs = e.timeMs < 0f ? 0f : e.timeMs,
                    durationMs = e.durationMs < 0f ? 0f : e.durationMs,
                    kind = e.kind,
                    sfxClip = e.sfxClip,
                    vfxPrefab = e.vfxPrefab,
                    id = e.id,
                    socket = e.socket,
                    animTrigger = e.animTrigger,
                    invokeMethod = e.invokeMethod,
                    argType = e.argType,
                    argFloat = e.argFloat,
                    argInt = e.argInt,
                    argBool = e.argBool,
                    argString = e.argString,
                });
            }
            if (list.Count == 0) return Empty;

            // 안정 정렬: List.Sort 는 불안정하므로 인덱스를 tie-breaker 로 써 저작 순서를 보존한다.
            var indexed = new (AbilityCueEvent ev, int order)[list.Count];
            for (int i = 0; i < list.Count; i++) indexed[i] = (list[i], i);
            Array.Sort(indexed, (a, b) =>
            {
                int t = a.ev.timeMs.CompareTo(b.ev.timeMs);
                return t != 0 ? t : a.order.CompareTo(b.order);
            });

            var result = new AbilityCueEvent[indexed.Length];
            for (int i = 0; i < indexed.Length; i++) result[i] = indexed[i].ev;
            return result;
        }
    }
}
