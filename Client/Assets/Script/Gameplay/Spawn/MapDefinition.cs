using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Spawn
{
    /// <summary>
    /// 맵 1개의 저작(authoring) 진실원. 디자이너가 이 SO를 편집한다.
    ///
    /// 런타임은 이 SO를 직접 읽지 않는다. Export 툴(Tools/Spawn/Export Map Data)이
    /// 이 SO들을 모아 spawn-layouts.json 으로 bake → 클라/서버 양쪽 런타임이 그 JSON을 읽는다.
    /// (서버는 UnityEngine 의존이 0이라 SO를 못 읽으므로, JSON 이 유일한 교환 포맷이다.)
    ///
    /// 확장 여지: monsterSpawns[], visualPrefab, cellSize 등은 추후 필드 추가만으로 합류.
    /// </summary>
    [CreateAssetMenu(fileName = "MapDefinition", menuName = "Game/Map Definition", order = 0)]
    public sealed class MapDefinition : ScriptableObject
    {
        [Tooltip("spawn-layouts.json 키 / 서버 MapId 와 정확히 일치해야 한다 (예: dungeon_01).")]
        public string mapId = "dungeon_01";

        [Tooltip("던전 클리어 시 참가자에게 지급할 경험치(서버 권위). 0=보상 없음(Main/아레나 등). spawn-layouts.json 의 expReward 로 bake 되어 서버가 읽는다.")]
        public long expReward;

        [Tooltip("이 던전 몬스터의 기본 레벨(AC-E). 0=L1. 스폰별 level 이 있으면 그쪽이 우선.\n" +
                 "몬스터 HP·피해·드롭이 이 레벨로 스케일된다(monster-leveling.md). 한 줄로 던전 전체 난이도를 조절한다.")]
        public int monsterLevel;

        [Tooltip("맵 배경 모델 프리팹(클라 전용). 던전 진입 시 MapLoader 가 인스턴스화. 서버는 사용 안 함(JSON에 미포함).")]
        public GameObject visualPrefab;

        [Tooltip("플레이어 스폰 슬롯. 리스트 순서 = SpawnIndex (0,1,2…).")]
        public List<MapSpawnPoint> playerSpawns = new();

        [Tooltip("몬스터 스폰(M3). 서버 권위 시뮬레이션이 이 정의로 스폰·순찰한다.")]
        public List<MonsterSpawn> monsterSpawns = new();

        [Tooltip("맵 경계(XZ 사각형). 서버가 몬스터 이동을 이 경계로 clamp 해 맵 이탈을 막는다.")]
        public MapBounds bounds = new();
    }

    /// <summary>맵 경계 저작값(XZ 평면 사각형, center+size). 서버 MapBounds 와 1:1 대응.</summary>
    [Serializable]
    public sealed class MapBounds
    {
        public float centerX;
        public float centerZ;
        public float sizeX = 40f;
        public float sizeZ = 40f;
    }

    /// <summary>저작용 스폰 한 지점(위치 + Y축 회전). 프리뷰 씬 마커가 이 값으로 write-back 된다.</summary>
    [Serializable]
    public sealed class MapSpawnPoint
    {
        public Vector3 position;
        public float rotationY;
    }

    /// <summary>
    /// 몬스터 등급(저작용). 서버 <c>Shared.Infrastructure.Monsters.MonsterTier</c> 의 **미러**다 —
    /// 그쪽은 서버 전용 어셈블리라 클라가 참조할 수 없다.
    /// <para>계약은 <b>JSON 의 int</b>(0/1/2)이고 값이 셋뿐이라 드리프트 위험이 낮다.
    /// 값을 추가하면 <b>양쪽을 같이</b> 고쳐야 한다(리뷰 대상).</para>
    /// </summary>
    public enum MonsterTier
    {
        Normal = 0,
        Elite = 1,
        Boss = 2,
    }

    /// <summary>몬스터 스폰 정의(저작). 실제 스폰/AI 는 M3에서 서버 권위로 구동된다.</summary>
    [Serializable]
    public sealed class MonsterSpawn
    {
        [Tooltip("몬스터 타입 키(서버·클라 공용 식별자).")]
        public string monsterId;
        public Vector3 position;
        public float rotationY;
        [Tooltip("이 지점에서 동시에 스폰할 수.")]
        public int count = 1;
        [Tooltip("웨이브 인덱스(0=시작 시). 미사용 시 0.")]
        public int wave;

        [Tooltip("이 스폰만의 레벨(AC-E). 0=맵 기본(MapDefinition.monsterLevel) 사용.\n" +
                 "같은 던전 안에서 이 몬스터만 대역을 올릴 때 쓴다(엘리트·보스 배치).")]
        public int level;

        [Tooltip("등급(AC-E). 레벨과 직교 — 대역 안에서의 강도.\n" +
                 "Elite=HP×2·피해×1.3·Exp×3·드롭확률×2 / Boss=HP×6·피해×1.6·Exp×10·드롭확률×3.\n" +
                 "HP 를 크게 피해를 작게 올린다 — 피해를 키우면 즉사가 되고 HP 를 키우면 '오래 버티는 위협'이 된다.")]
        public MonsterTier tier = MonsterTier.Normal;

        [Tooltip("Main B-lite 클레임 키(슬롯 안정 식별자, 1부터). 0=클레임 불가. 던전은 미사용.")]
        public int slotId;
        [Tooltip("Main B-lite 재청구·재스폰 쿨다운(ms) = 파밍률 상한. 던전은 미사용(0).")]
        public int respawnCooldownMs;

        [Tooltip("패트롤 경로(순서대로 순회). 비어 있으면 스폰 지점에서 제자리 경비.")]
        public List<Vector3> patrolPoints = new();
    }
}
