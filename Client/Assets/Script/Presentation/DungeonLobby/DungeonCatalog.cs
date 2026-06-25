using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.DungeonLobby
{
    /// <summary>
    /// 방 생성 시 고를 수 있는 던전 목록(표현 계층 메타). mapId → 표시이름 매핑의 단일 저작 소스.
    ///
    /// 서버는 이미 spawn-layouts.json + SpawnLayoutTable.IsKnown 으로 mapId 유효성을 권위 검증한다.
    /// 따라서 "어떤 던전을 고를 수 있고 화면에 뭐라 보이나"는 클라 표현 책임 → SO 로 둔다(YAGNI: 서버 RPC 불필요).
    /// 단, mapId 값은 반드시 spawn-layouts.json 의 키와 일치해야 한다(불일치 시 서버가 생성 거부).
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonCatalog", menuName = "Game/Dungeon Catalog", order = 1)]
    public sealed class DungeonCatalog : ScriptableObject
    {
        [SerializeField] private List<DungeonOption> dungeons = new();

        public IReadOnlyList<DungeonOption> Dungeons => dungeons;

        /// <summary>mapId 의 표시이름. 미등록이면 mapId 원문을 그대로 반환(빈값이면 "기본 던전").</summary>
        public string GetDisplayName(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return "기본 던전";
            foreach (var d in dungeons)
                if (d.MapId == mapId) return d.DisplayName;
            return mapId;
        }
    }

    /// <summary>던전 선택지 1개(표현용). mapId 는 서버 spawn-layouts.json 키와 일치해야 한다.</summary>
    [Serializable]
    public sealed class DungeonOption
    {
        [Tooltip("spawn-layouts.json 키 / 서버 MapId (예: dungeon_01).")]
        public string MapId;
        [Tooltip("UI 에 표시할 던전 이름 (예: 슬라임 동굴).")]
        public string DisplayName;
    }
}
