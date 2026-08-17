using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Character;
using Game.Gameplay.Spawn;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using Game.System.Auth;
using Game.System.Player;
using NUnit.Framework;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Game.Tests.PlayMode.InGame
{
    /// <summary>
    /// 다중 클라이언트 스폰 PlayMode 검증 (Fake 소켓 — Docker 불필요).
    ///
    /// 한 클라이언트가 서버 스냅샷(self + 다수 원격)을 받았을 때:
    ///   - 로컬 캐릭터는 **결정론 Resolve**(MapId,내 SpawnIndex) 좌표에 스폰 (서버 좌표 신뢰 X)
    ///   - 원격 캐릭터들은 **서버가 보낸 현재 위치**에 각각 스폰 (여러 명 = 다중 클라 "서로 보임")
    ///   - 입장 후 도착한 원격도 동적 스폰
    ///   - MapLoader 가 MapDefinition.visualPrefab 을 인스턴스화
    ///
    /// ※ 실서버 다중 클라 통합은 SocketE2ETests(Docker). 여기선 Unity 스폰 로직을 격리 검증한다.
    /// </summary>
    [TestFixture]
    public class CharacterSpawnMultiClientTests
    {
        private const long   SelfId = 1001;
        private const string MapId  = "dungeon_01";

        private IObjectResolver _container;
        private readonly List<GameObject> _templates = new();

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();
            _container = null;

            foreach (var rd in Object.FindObjectsByType<RemoteDriver>(FindObjectsSortMode.None))
                if (rd != null) Object.DestroyImmediate(rd.gameObject);
            foreach (var asc in Object.FindObjectsByType<AbilitySystemComponent>(FindObjectsSortMode.None))
                if (asc != null) Object.DestroyImmediate(asc.gameObject);
            var plane = GameObject.Find("Plane(Clone)");
            if (plane != null) Object.DestroyImmediate(plane);

            foreach (var go in _templates)
                if (go != null) Object.DestroyImmediate(go);
            _templates.Clear();
        }

        // ── 테스트 ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 다중클라_로컬1_원격2가_각자_위치에_스폰된다() => UniTask.ToCoroutine(async () =>
        {
            var state = new SocketPacketState();
            // self: SpawnIndex 1 → Resolve 좌표. 스냅샷 Pos(0,0,0)는 로컬 스폰에 안 쓰인다(결정론) — 그게 이 테스트의 불변식.
            state.UpsertPlayer(SelfId, "me", 1, MapId, 0, 0, 0, 0);
            // 원격 2명: 서버가 보낸 "현재 위치"에 스폰돼야 한다.
            state.UpsertPlayer(201, "r1", 2, MapId, 5f, 0f, 5f, 0f);
            state.UpsertPlayer(202, "r2", 3, MapId, -5f, 0f, -5f, 0f);

            var spawner = BuildSpawner(state);
            await spawner.StartAsync(CancellationToken.None);
            await UniTask.DelayFrame(2);

            // 로컬 = ASC 보유(템플릿 제외) 1개, 결정론 좌표 (2,0,0)
            var locals = NonTemplate(Object.FindObjectsByType<AbilitySystemComponent>(FindObjectsSortMode.None)
                .Select(c => c.gameObject));
            Assert.AreEqual(1, locals.Count, "로컬 캐릭터는 1개여야 한다");
            // 기대 좌표를 하드코딩하지 않는다 — 스폰 포인트는 저작 데이터(spawn-layouts)라 맵을 재기획하면 바뀐다.
            // (실제로 dungeon_01 이 2026-07 에 Z-16 으로 옮겨졌는데 이 테스트만 옛 (2,0,0) 을 들고 있어 깨져 있었다.)
            // 프로덕션과 같은 리졸버에서 유도하되, 스냅샷(0,0,0)과 다름을 함께 단언해 "결정론 좌표를 쓴다"를 지킨다.
            var expected = SpawnResolver.Resolve(new SpawnLayoutProvider().Get(MapId), 1);
            Assert.AreNotEqual(Vector3.zero, new Vector3(expected.X, expected.Y, expected.Z),
                "선행 조건: 결정론 좌표가 스냅샷(0,0,0)과 달라야 이 불변식이 성립한다");
            AssertPos(locals[0], expected.X, expected.Y, expected.Z);

            // 원격 = RemoteDriver 보유(템플릿 제외) 2개, 각자 서버 현재 위치
            var remotes = NonTemplate(Object.FindObjectsByType<RemoteDriver>(FindObjectsSortMode.None)
                .Select(c => c.gameObject));
            Assert.AreEqual(2, remotes.Count, "원격 캐릭터는 2개여야 한다(다중 클라)");
            CollectionAssert.AreEquivalent(
                new[] { new Vector3(5f, 0f, 5f), new Vector3(-5f, 0f, -5f) },
                remotes.Select(g => Round(g.transform.position)).ToArray());
        });

        [UnityTest]
        public IEnumerator 입장후_도착한_원격도_동적_스폰된다() => UniTask.ToCoroutine(async () =>
        {
            var state = new SocketPacketState();
            state.UpsertPlayer(SelfId, "me", 0, MapId, 0, 0, 0, 0); // self만 먼저

            var spawner = BuildSpawner(state);
            await spawner.StartAsync(CancellationToken.None);
            await UniTask.DelayFrame(1);

            Assert.AreEqual(0, NonTemplate(Object.FindObjectsByType<RemoteDriver>(FindObjectsSortMode.None)
                .Select(c => c.gameObject)).Count, "아직 원격 없음");

            // 늦게 입장 → OnPlayerJoined 발행 → 동적 스폰
            state.UpsertPlayer(303, "late", 1, MapId, 7f, 0f, 0f, 0f);
            await UniTask.DelayFrame(2);

            var remotes = NonTemplate(Object.FindObjectsByType<RemoteDriver>(FindObjectsSortMode.None)
                .Select(c => c.gameObject));
            Assert.AreEqual(1, remotes.Count, "늦게 입장한 원격이 스폰돼야 한다");
            AssertPos(remotes[0], 7f, 0f, 0f);
        });

        [UnityTest]
        public IEnumerator 원격_스폰시_서버_HP기준선으로_ASC가_초기화된다() => UniTask.ToCoroutine(async () =>
        {
            // 파티 HP HUD 정확도: 원격 ASC 를 prefab 기본값(100)이 아닌 서버 권위 HP(S_PlayerJoined Hp/MaxHp)로 정렬.
            // 이렇게 해야 이후 S_ApplyEffect 델타가 정확한 기준선 위에 얹혀 파티창 HP 가 실제와 일치한다.
            var state = new SocketPacketState();
            state.UpsertPlayer(SelfId, "me", 0, MapId, 0, 0, 0, 0);
            // 원격: 레벨업 캐릭터(MaxHp 140) 가 이미 30 피해(현재 110) 상태로 늦게 입장.
            state.UpsertPlayer(401, "r", 1, MapId, 3f, 0f, 0f, 0f, timeStamp: 0, hp: 110, maxHp: 140);

            var spawner = BuildSpawner(state, remoteHasAsc: true);
            await spawner.StartAsync(CancellationToken.None);
            await UniTask.DelayFrame(2);

            var remoteAsc = NonTemplate(Object.FindObjectsByType<RemoteDriver>(FindObjectsSortMode.None)
                    .Select(c => c.gameObject))
                .Select(g => g.GetComponent<AbilitySystemComponent>())
                .Single(a => a != null);
            var hp = remoteAsc.GetAttribute(EGameplayAttribute.Health);
            Assert.AreEqual(140, hp.MaxValue, "원격 MaxHp 는 서버 권위값(140)이어야 한다(prefab 100 아님).");
            Assert.AreEqual(110, hp.CurrentValue, "원격 현재 HP 는 서버 권위값(110)이어야 한다.");

            // 레지스트리에도 같은 ASC 가 등록돼 EffectReceiver/PartyModel 이 델타를 얹을 수 있어야 한다.
            var registry = _container.Resolve<PartyAscRegistry>();
            Assert.IsTrue(registry.TryGet(401, out var reg) && reg == remoteAsc, "원격 ASC 가 파티 레지스트리에 등록돼야 한다.");
        });

        [UnityTest]
        public IEnumerator MapLoader가_visualPrefab을_인스턴스화한다() => UniTask.ToCoroutine(async () =>
        {
            var state = new SocketPacketState();
            state.UpsertPlayer(SelfId, "me", 0, MapId, 0, 0, 0, 0); // MapId 세팅용

            var loader = BuildMapLoader(state);
            await loader.StartAsync(CancellationToken.None);
            await UniTask.DelayFrame(2);

            Assert.IsNotNull(GameObject.Find("Plane(Clone)"), "MapDefinition.visualPrefab(Plane)이 인스턴스화돼야 한다");
        });

        // ── 빌드 헬퍼 ─────────────────────────────────────────────────

        private CharacterSpawner BuildSpawner(ISocketPacketState state, bool remoteHasAsc = false)
        {
            var builder = ConfigureCommon(state, remoteHasAsc);
            builder.Register<CharacterSpawner>(Lifetime.Scoped).AsSelf();
            _container = builder.Build();
            return _container.Resolve<CharacterSpawner>();
        }

        private MapLoader BuildMapLoader(ISocketPacketState state)
        {
            var builder = ConfigureCommon(state);
            builder.Register<MapLoader>(Lifetime.Scoped).AsSelf();
            _container = builder.Build();
            return _container.Resolve<MapLoader>();
        }

        private ContainerBuilder ConfigureCommon(ISocketPacketState state, bool remoteHasAsc = false)
        {
            var localTemplate  = MakeTemplate("LocalTemplate",  withAsc: true);
            var remoteTemplate = MakeTemplate("RemoteTemplate", withAsc: remoteHasAsc, withRemoteDriver: true);

            var builder = new ContainerBuilder();
            builder.RegisterInstance<ISocketSession>(new FakeJoinedSocketSession());
            builder.RegisterInstance(state);
            builder.RegisterInstance(MakeAuthSession(SelfId));
            builder.RegisterInstance(new CharacterPrefabSettings(localTemplate, remoteTemplate));
            builder.Register<LocalPlayerContext>(Lifetime.Scoped).AsSelf();
            // CharacterSpawner 가 로컬/원격 ASC 를 파티 레지스트리에 등록하므로 필수 의존.
            builder.Register<PartyAscRegistry>(Lifetime.Scoped).AsSelf();
            // AC 트랙(2026-07-17)에서 CharacterSpawner 가 얻은 의존 — 스폰한 액터를 ActorId 로 등록해
            // AbilityCueRouter 가 발동 신호를 라우팅한다. DungeonLifetimeScope 와 같은 Scoped 로 맞춘다.
            builder.Register<ActorRegistry>(Lifetime.Scoped).AsSelf();
            builder.Register<SpawnLayoutProvider>(Lifetime.Scoped).AsSelf();
            return builder;
        }

        private GameObject MakeTemplate(string name, bool withAsc, bool withRemoteDriver = false)
        {
            var go = new GameObject(name);
            if (withAsc) go.AddComponent<AbilitySystemComponent>();
            if (withRemoteDriver) go.AddComponent<RemoteDriver>();
            _templates.Add(go);
            return go;
        }

        // ── 유틸 ─────────────────────────────────────────────────────

        private List<GameObject> NonTemplate(IEnumerable<GameObject> gos)
            => gos.Where(g => !_templates.Contains(g)).ToList();

        private static void AssertPos(GameObject go, float x, float y, float z)
        {
            Assert.AreEqual(x, go.transform.position.x, 0.01f);
            Assert.AreEqual(y, go.transform.position.y, 0.01f);
            Assert.AreEqual(z, go.transform.position.z, 0.01f);
        }

        private static Vector3 Round(Vector3 v)
            => new(Mathf.Round(v.x), Mathf.Round(v.y), Mathf.Round(v.z));

        private static AuthSession MakeAuthSession(long userId)
        {
            var session = new AuthSession();
            session.Update(FakeJwt(userId), "refresh", global::System.DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());
            return session;
        }

        /// <summary>"sub" 클레임만 담은 가짜 JWT — AuthSession.UserId 파싱용.</summary>
        private static string FakeJwt(long userId)
        {
            var payload = Encoding.UTF8.GetBytes($"{{\"sub\":\"{userId}\"}}");
            var b64 = global::System.Convert.ToBase64String(payload).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return $"h.{b64}.s";
        }

        private sealed class FakeJoinedSocketSession : ISocketSession
        {
            public SocketSessionState State => SocketSessionState.Joined;
            public event global::System.Action OnDisconnected { add { } remove { } }
            public UniTask ConnectAsync(SocketConnectionInfo info, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask JoinRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask LeaveRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask DisconnectAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendMoveAsync(C_Move packet, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendAsync(Packet packet, CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
