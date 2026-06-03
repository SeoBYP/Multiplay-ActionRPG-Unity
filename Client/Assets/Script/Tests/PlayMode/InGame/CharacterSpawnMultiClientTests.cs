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
            // self: SpawnIndex 1 → Resolve → (2,0,0). 스냅샷 Pos(0,0,0)는 로컬 스폰에 안 쓰임(결정론).
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
            AssertPos(locals[0], 2f, 0f, 0f);

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

        private CharacterSpawner BuildSpawner(ISocketPacketState state)
        {
            var builder = ConfigureCommon(state);
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

        private ContainerBuilder ConfigureCommon(ISocketPacketState state)
        {
            var localTemplate  = MakeTemplate("LocalTemplate",  withAsc: true);
            var remoteTemplate = MakeTemplate("RemoteTemplate", withAsc: false, withRemoteDriver: true);

            var builder = new ContainerBuilder();
            builder.RegisterInstance<ISocketSession>(new FakeJoinedSocketSession());
            builder.RegisterInstance(state);
            builder.RegisterInstance(MakeAuthSession(SelfId));
            builder.RegisterInstance(new CharacterPrefabSettings(localTemplate, remoteTemplate));
            builder.Register<LocalPlayerContext>(Lifetime.Scoped).AsSelf();
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
            public UniTask ConnectAsync(SocketConnectionInfo info, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask JoinRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask LeaveRoomAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask DisconnectAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SendMoveAsync(C_Move packet, CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
