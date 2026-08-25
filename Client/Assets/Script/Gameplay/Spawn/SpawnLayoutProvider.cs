using System.Collections.Generic;
using UnityEngine.AddressableAssets;

namespace Game.Gameplay.Spawn
{
    /// <summary>
    /// 맵별 스폰 레이아웃을 제공한다. 출처는 **MapDefinition(SO)** — 저작 진실원을 그대로 읽는다.
    ///
    /// 예전에는 bake 산출물(Resources/spawn-layouts.json)을 읽었다. Addressables 전환의 목적이
    /// "Resources = 빌드에 항상 포함" 회피였는데 맵이 늘수록 커지는 이 데이터가 마지막까지 남아 있었다(F6).
    /// 이제 `MapLoader`(배경 프리팹)와 **같은 주소 체계**로 on-demand 로드한다.
    ///
    /// ⚠ 서버는 여전히 bake(spawn-layouts.json)를 읽는다. 즉 **Export 를 잊으면 클라와 서버의 스폰이 갈린다.**
    /// 그 사고는 `SpawnLayoutSourceTests.MapDefinition_저작값이_서버_bake_와_일치한다` 가 잡는다.
    /// </summary>
    public sealed class SpawnLayoutProvider
    {
        private const string AddressFormat = "Assets/GameData/Maps/{0}.asset";

        private readonly Dictionary<string, MapSpawnLayout> _cache = new();

        /// <summary>mapId 의 레이아웃을 반환한다. 알 수 없는 mapId 면 예외(데이터 누락을 조용히 넘기지 않는다).</summary>
        public MapSpawnLayout Get(string mapId)
        {
            if (_cache.TryGetValue(mapId, out var cached))
                return cached;

            var layout = Load(mapId);
            _cache[mapId] = layout;
            return layout;
        }

        private static MapSpawnLayout Load(string mapId)
        {
            var address = string.Format(AddressFormat, mapId);

            // 먼저 주소 등록 여부만 조회한다. 바로 Load 하면 미등록 주소가 InvalidKeyException 을
            // **에러 로그와 함께** 던져, 정상 흐름(알 수 없는 맵)이 콘솔 에러로 남는다.
            var locate = Addressables.LoadResourceLocationsAsync(address, typeof(MapDefinition));
            var locations = locate.WaitForCompletion();
            bool registered = locations != null && locations.Count > 0;
            if (locate.IsValid()) Addressables.Release(locate);

            if (!registered)
            {
                throw new KeyNotFoundException(
                    $"Spawn layout for mapId '{mapId}' not found. Addressable 주소 '{address}' 가 등록돼 있는가?");
            }

            var handle = Addressables.LoadAssetAsync<MapDefinition>(address);
            var def = handle.WaitForCompletion();
            if (def == null)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                throw new KeyNotFoundException(
                    $"Spawn layout for mapId '{mapId}' 로드 결과 없음 (address='{address}').");
            }

            // 값만 복사한다 — 그래야 핸들을 바로 놓아줄 수 있다(에셋을 붙들지 않는다).
            var points = new List<SpawnPoint>(def.playerSpawns.Count);
            foreach (var p in def.playerSpawns)
                points.Add(new SpawnPoint(p.position.x, p.position.y, p.position.z, p.rotationY));

            var monsters = new List<MonsterSlot>(def.monsterSpawns.Count);
            foreach (var m in def.monsterSpawns)
                monsters.Add(new MonsterSlot(m.monsterId, m.position.x, m.position.y, m.position.z,
                    m.slotId, m.respawnCooldownMs));

            var layout = new MapSpawnLayout(def.mapId, points, monsters);

            if (handle.IsValid()) Addressables.Release(handle);
            return layout;
        }
    }
}
