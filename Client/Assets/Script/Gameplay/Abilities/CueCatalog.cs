using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Gameplay.Abilities
{
    /// <summary>
    /// 연출 리소스 카탈로그(SO) — Cue <c>id</c> → 실제 AudioClip / VFX 프리팹.
    /// <b>클라 전용, bake 없음</b>(서버는 연출을 모른다 — gas-architecture §2). AC-D3.
    ///
    /// <see cref="AbilityCuePlayer"/> 가 <see cref="AbilityCueEvent.id"/> 로 여기서 리소스를 찾아 재생한다.
    /// id 를 데이터로 두는 이유: 어빌리티 SO 는 "언제 무엇을"(타이밍)만 저작하고, "그 무엇의 실체"(클립·프리팹)는
    /// 여기 한 곳에서 교체·재사용 → 같은 스윙음을 여러 어빌리티가 공유하고, 리소스 교체가 어빌리티 저작과 분리된다.
    ///
    /// 인스펙터 직렬화를 위해 Dictionary 대신 리스트 + 런타임 1회 인덱싱(<see cref="BuildIndex"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "CueCatalog", menuName = "Game/Cue Catalog", order = 6)]
    public sealed class CueCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class SfxEntry
        {
            [Tooltip("Cue id — AbilityCueEvent.id 와 매칭(예: swing_light, impact_heavy).")]
            public string id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        [Serializable]
        public sealed class VfxEntry
        {
            [Tooltip("Cue id — AbilityCueEvent.id 와 매칭(예: slash_arc, hit_spark).")]
            public string id;
            [Tooltip("스폰할 파티클/이펙트 프리팹. 소켓(또는 루트)에 부착 생성된다.")]
            public GameObject prefab;
            [Tooltip("스폰 후 자동 파괴까지의 시간(초). 파티클 수명에 맞춘다.")]
            public float autoDestroySec = 3f;
        }

        [SerializeField] private List<SfxEntry> sfx = new();
        [SerializeField] private List<VfxEntry> vfx = new();

        private Dictionary<string, SfxEntry> _sfxIndex;
        private Dictionary<string, VfxEntry> _vfxIndex;

        private void BuildIndex()
        {
            _sfxIndex = new Dictionary<string, SfxEntry>(sfx.Count);
            foreach (var e in sfx)
                if (e != null && !string.IsNullOrEmpty(e.id)) _sfxIndex[e.id] = e;

            _vfxIndex = new Dictionary<string, VfxEntry>(vfx.Count);
            foreach (var e in vfx)
                if (e != null && !string.IsNullOrEmpty(e.id)) _vfxIndex[e.id] = e;
        }

        /// <summary>SFX 조회. 미등록이면 false(재생기는 조용히 스킵).</summary>
        public bool TryGetSfx(string id, out SfxEntry entry)
        {
            if (_sfxIndex == null) BuildIndex();
            entry = null;
            return !string.IsNullOrEmpty(id) && _sfxIndex.TryGetValue(id, out entry) && entry.clip != null;
        }

        /// <summary>VFX 조회. 미등록이면 false(재생기는 조용히 스킵).</summary>
        public bool TryGetVfx(string id, out VfxEntry entry)
        {
            if (_vfxIndex == null) BuildIndex();
            entry = null;
            return !string.IsNullOrEmpty(id) && _vfxIndex.TryGetValue(id, out entry) && entry.prefab != null;
        }

        /// <summary>해당 종류에 등록된 Cue id 목록(타임라인 툴 P3 드롭다운용). Anim 은 카탈로그 대상 아님 → 빈 목록.</summary>
        public IEnumerable<string> IdsFor(ECueKind kind)
        {
            var src = kind switch
            {
                ECueKind.Vfx => vfx.Select(e => e?.id),
                ECueKind.Sfx => sfx.Select(e => e?.id),
                _ => Enumerable.Empty<string>(),
            };
            return src.Where(id => !string.IsNullOrEmpty(id)).Distinct();
        }
    }
}
