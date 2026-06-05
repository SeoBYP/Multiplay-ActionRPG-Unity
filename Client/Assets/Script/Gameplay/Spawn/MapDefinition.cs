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

        [Tooltip("패트롤 경로(순서대로 순회). 비어 있으면 스폰 지점에서 제자리 경비.")]
        public List<Vector3> patrolPoints = new();
    }
}
