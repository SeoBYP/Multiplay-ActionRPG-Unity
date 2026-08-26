using Script.System.GamePlayAbilitySystem;
using Server.Actors;
using Server.Combat;
using Server.Diagnostics;
using Server.Loot;
using Server.Monster;
using Shared.Infrastructure.Abilities;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;

namespace Server.Room;

using Microsoft.Extensions.Logging;
using Shared.Packet.Packets;

/// <summary>
/// 던전 방 1개. <b>방 관리</b>(입장·퇴장·재접속 유예·진행 판정)를 맡고, 캐릭터·전투는 <see cref="Actor"/> 에 위임한다.
///
/// <para><b>액터·참가자 저장소는 <see cref="ActorStore"/> 가 소유한다</b>(키=ActorId). 예전엔 플레이어와
/// 몬스터가 서로 다른 딕셔너리·서로 다른 락에 있어서 몬스터가 플레이어를 때리는 한 번의 흐름이
/// 락 3중 중첩(<c>_monsters → _playerStates → _downed</c>)을 만들었다. 액터를 합치고 다운을 태그로
/// 옮기면서 그 중첩이 <b>사라졌다</b> — 전투·진행 판정이 모두 <c>Actors.SyncRoot</c> 하나만 잡는다.</para>
///
/// <para><b>다운은 별도 집합이 아니라 액터의 태그다</b>(<c>GameplayTags.Dead</c>).
/// 예전 <c>_downed</c> HashSet 은 "죽었나"의 두 번째 진실원이었고, 클라가 <c>C_PlayerDead</c> 를
/// 자기신고하면 서버 HP 와 무관하게 그 집합에 들어갔다 — 만피인 채로 AI 타깃에서 빠지는 구멍이었다.</para>
/// </summary>
public class Room
{
    public long RoomId { get; private set; }
    public int MaxMembers { get; private set; }

    /// <summary>플레이 중인 맵 식별자. 스폰 레이아웃 선택에 사용. CreateRoom 에서 설정.</summary>
    public string MapId { get; set; } = Shared.Infrastructure.Spawn.MapIds.Default;

    /// <summary>연결 집합·브로드캐스트. 세션은 자주 죽지만 참가자·액터는 유예 동안 살아 있다 — 그래서 저장소가 다르다.</summary>
    public RoomSessions Sessions { get; }

    /// <summary>
    /// 액터·참가자 저장소. <b>누가 존재하고 어떻게 찾는가</b>는 전부 여기가 소유한다 —
    /// Room 은 그 위에서 세션·생명주기·진행 판정만 한다.
    /// </summary>
    public ActorStore Actors { get; } = new();

    /// <summary>던전 진행 판정(클리어·실패·다운·부활). 같은 terminal 상태를 공유하는 넷을 한곳에서.</summary>
    public DungeonProgress Progress { get; }

    /// <summary>한 틱 몬스터 시뮬레이션. 방의 저장소 위에서 돌지만 진행 판정(실패·다운)은 모른다.</summary>
    private readonly RoomSimulation _simulation;

    private readonly HashSet<long> _expectedUserIds;
    private readonly ILogger<Room> _logger;

    private MapBounds _bounds = MapBounds.Unbounded;

    /// <summary>
    /// 바닥 아이템(드랍/줍기). <b>자기 락을 자기가 소유</b>하는 별도 저장소다 —
    /// 방의 다른 상태와 공유하는 것이 없어서 Room 에 둘 이유가 없었다.
    /// </summary>
    public GroundItemStore Loot { get; } = new();

    /// <summary>
    /// 재접속 유예 창(ms). 크래시/끊김(graceful) 시 참가자를 즉시 지우지 않고 이 시간 동안 보존한다.
    /// 방에 다른 플레이어가 남아 있는 한, 끊긴 플레이어가 이 안에 재접속하면 보존 상태로 즉시 던전 복귀.
    /// 만료되면 RoomTickService 스윕이 정리하고 영구 퇴장으로 확정한다.
    /// (전원 끊겨 방이 비면 방 자체가 즉시 제거되므로 유예 대상 아님 — 클라는 "방 종료" 팝업.)
    /// </summary>
    public const long ReconnectGraceMs = 60_000;


    /// <summary>플레이어 기본 최대 HP(서버 권위). 후속에 Progression/스탯에서 주입. 클라 prefab ASC(100)와 정렬.</summary>
    public const int DefaultMaxHp = 100;

    /// <summary>스탯 미설정(테스트/레거시) 시 마나 상한 폴백. 클라 prefab Mana(100) 기준선과 정렬.</summary>
    public const int DefaultMaxMana = 100;

    /// <summary>맵 경계 — 몬스터 이동 clamp 기준.</summary>
    public MapBounds Bounds => _bounds;

    // 서버 권위 GameplayEffect InstanceId 발급기 (방 단위, 스레드 안전).
    private int _nextEffectInstanceId;

    /// <summary>활성 Effect 인스턴스에 부여할 서버 권위 InstanceId를 1씩 증가시켜 반환한다.</summary>
    public int NextEffectInstanceId() => System.Threading.Interlocked.Increment(ref _nextEffectInstanceId);

    // 이미 적용한 소비 통지 id — 재배달 시 이중 회복 차단(lock 안에서만 접근).
    // 수명을 방에 묶는 이유: 회복 대상이 방의 인메모리 상태라, 방이 사라지면 중복 걱정도 함께 사라진다.
    private readonly HashSet<string> _handledConsumeIds = new();

    /// <summary>
    /// 소비 통지를 이 방에서 처음 보는 것이면 표시하고 true. 이미 적용했으면 false.
    /// consumeId 가 비어 있으면(구 메시지) 차단하지 않는다.
    /// </summary>
    public bool TryMarkConsumeHandled(string consumeId)
    {
        if (string.IsNullOrEmpty(consumeId))
            return true;

        lock (_handledConsumeIds)
            return _handledConsumeIds.Add(consumeId);
    }

    public Room(long roomId, IReadOnlyList<PlayerInfo> expectedUserIds, ILogger<Room> logger)
    {
        RoomId = roomId;
        MaxMembers = expectedUserIds.Count;
        _expectedUserIds = new HashSet<long>();
        foreach (var playerInfo in expectedUserIds)
        {
            _expectedUserIds.Add(playerInfo.UserId);
        }
        _logger = logger;
        Sessions = new RoomSessions(roomId, MaxMembers, logger);
        Progress = new DungeonProgress(Actors);
        _simulation = new RoomSimulation(Actors, NextEffectInstanceId, logger);
    }

    public bool IsExpectedPlayer(long userId) => _expectedUserIds.Contains(userId);

    /// <param name="graceful">
    /// true = 크래시/네트워크 끊김(C_PlayerLeave 없음). 참가자를 즉시 지우지 않고
    ///        <see cref="ReconnectGraceMs"/> 동안 보존(DisconnectedAtMs 마킹) → 재접속 시 복귀.
    /// false = 명시 퇴장(C_PlayerLeave). 참가자·액터 즉시 제거(영구 퇴장).
    /// 어느 쪽이든 세션(_playerSessions)은 즉시 제거된다. 빈 방 처리·이벤트 발행은 RoomManager 책임.
    /// </param>
    public bool Leave(ulong sessionId, bool graceful = false)
    {
        long? userId = Sessions.Remove(sessionId);
        if (userId is null)
            return false;

        if (userId > 0)
        {
            lock (Actors.SyncRoot)
            {
                var member = Actors.GetMember(userId.Value);
                if (graceful && member is not null)
                {
                    // 크래시/끊김: 액터 보존 + 끊긴 시각 마킹. 유예 창 동안 AI 타깃에선 제외(Tick).
                    member.DisconnectedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
                else
                {
                    // 명시 퇴장(또는 참가자 없음): 유령 잔류 방지로 즉시 제거.
                    Actors.RemoveMember(userId.Value);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 입장/재접속 시 호출(C_PlayerJoin 성공) — 참가자를 라이브로 활성화한다:
    /// HasJoined=true 로 표시(이제부터 몬스터 AI 타깃)하고, 끊김 유예 중이었다면 마킹을 해제해 즉시 복귀시킨다.
    /// 반환 false = 보존된 참가자가 없음(유예 만료/명시 퇴장 후 → 재입장 불가).
    /// </summary>
    public bool MarkJoined(long userId)
    {
        lock (Actors.SyncRoot)
        {
            var member = Actors.GetMember(userId);
            if (member is null)
                return false;

            member.DisconnectedAtMs = null;
            member.HasJoined = true;
            return true;
        }
    }

    /// <summary>
    /// 유예 창이 만료된 끊김 참가자를 제거하고 그 userId 목록을 반환한다(RoomTickService 가 10Hz 로 호출).
    /// 반환된 userId 는 RoomManager 가 영구 퇴장으로 확정(S_PlayerLeft 브로드캐스트 + association 정리).
    /// </summary>
    public List<long> SweepExpiredDisconnected(long nowMs, long graceMs)
    {
        List<long>? expired = null;
        lock (Actors.SyncRoot)
        {
            foreach (var member in Actors.MembersLocked)
            {
                if (member.DisconnectedAtMs is { } at && nowMs - at >= graceMs)
                    (expired ??= new List<long>()).Add(member.UserId);
            }

            if (expired != null)
                foreach (var userId in expired)
                    Actors.RemoveMember(userId);
        }

        return expired ?? EmptyUserIds;
    }

    private static readonly List<long> EmptyUserIds = new();

    /// <summary>
    /// 참가자와 그 캐릭터를 생성한다(게임 시작 시 1회, 소켓 입장 전).
    /// 스탯(<paramref name="maxHealth"/> 등)은 GameServer 가 계산해 보낸 권위값 — SocketServer 는 받아 쓰기만 한다.
    /// </summary>
    public void AddPlayer(long userId, string nickname, int spawnIndex, float spawnX, float spawnY, float spawnZ, float rotY,
        int attackPower = 0, int defense = 0, int maxHealth = 0, int maxMana = 0)
    {
        // MaxHp/MaxMana = GameServer 가 보낸 스탯(권위). 0(미설정)이면 상수 폴백(테스트·레거시 경로 호환).
        int hp = maxHealth > 0 ? maxHealth : DefaultMaxHp;
        int mana = maxMana > 0 ? maxMana : DefaultMaxMana;

        Actors.AddPlayer(userId, nickname, spawnIndex, spawnX, spawnY, spawnZ, rotY,
            attackPower, defense, hp, mana);

        _logger.LogInformation(
            "Initialized player {UserId} ({Nickname}) slot {SpawnIndex} at ({SpawnX}, {SpawnY}, {SpawnZ}) in Room {RoomId}",
            userId, nickname, spawnIndex, spawnX, spawnY, spawnZ, RoomId);
    }

    /// <summary>
    /// 맵 레이아웃의 몬스터 정의로 스폰(wave 0). InstanceId 는 방 단위 순차 발급.
    ///
    /// **서버 스폰의 단일 진입점(불변식)** — 모든 스폰 *원인*(현재=던전 시작/`RoomManager`,
    /// 미래=웨이브·퀘스트·존·서버 리스폰)은 저장소에 직접 추가하지 말고 반드시 이 메서드를 경유한다.
    /// 설계·승격 트리거 = docs/wiki/spawn-system-evolution.md.
    /// </summary>
    /// <param name="mapMonsterLevel">던전 기본 몬스터 레벨. 0 = 미저작 → 스폰별 Level 도 없으면 L1.</param>
    public void SpawnMonsters(IReadOnlyList<MonsterSpawnDef> defs, MapBounds bounds, int mapMonsterLevel = 0)
    {
        lock (Actors.SyncRoot)
        {
            _bounds = bounds ?? MapBounds.Unbounded;
            int spawned = 0;
            foreach (var def in defs ?? Array.Empty<MonsterSpawnDef>())
            {
                var stats = MonsterCatalog.Get(def.MonsterId);
                int count = Math.Max(1, def.Count);

                // 레벨은 **스폰 시 1회 확정**한다 — 매 틱 재계산하지 않는다.
                // 등급은 스폰이 아니라 **카탈로그(monsterId 행)** 에서 온다 — monsterId 가 곧 변종이다.
                int level = MapSpawnLayout.ResolveLevel(def.Level, mapMonsterLevel);
                int maxHp = Shared.Infrastructure.Monsters.MonsterLevelScaling.Hp(stats.MaxHp, level);

                for (int i = 0; i < count; i++)
                {
                    int id = Actors.NextMonsterInstanceId();
                    var monster = new MonsterActor(id)
                    {
                        MonsterId = def.MonsterId,
                        Level = level,
                        Tier = Shared.Infrastructure.Monsters.MonsterCatalog.Get(def.MonsterId).Tier,
                        PosX = def.X, PosY = def.Y, PosZ = def.Z,
                        SpawnX = def.X, SpawnZ = def.Z,
                        RotY = def.RotY,
                        Phase = MonsterPhase.Idle,
                        Patrol = def.Patrol,
                        PatrolIndex = 0,
                    };
                    // 몬스터는 Health 만 보유한다 — 공격력·방어력·마나는 **아예 없다**(0 이 아니라 미보유).
                    // 예전엔 그 부재를 산식 호출부의 const 0 이 위장했다. 스탯이 생기면 여기서 Define 하면 된다.
                    monster.Gas.DefineResource(EGameplayAttribute.Health, maxHp);

                    Actors.Add(monster);
                    spawned++;
                }
            }

            if (spawned > 0)
                Progress.MarkMonstersSpawned();

            _logger.LogInformation("Room {RoomId} spawned {Count} monsters", RoomId, spawned);
        }
    }

    /// <summary>
    /// 한 틱 진행. 시뮬레이션(<see cref="RoomSimulation"/>)이 액터를 움직이고 패킷을 만들며,
    /// <b>다운 집계·실패 판정은 방이 한다</b> — 시뮬레이션은 "누가 HP 0 이 됐는지"만 알려준다.
    /// 전송 I/O 는 호출자(RoomTickService)가 락 밖에서.
    /// </summary>
    public List<Packet> Tick(float dt, long nowMs)
    {
        var (packets, downedUserIds) = _simulation.Tick(dt, nowMs, _bounds);

        foreach (var userId in downedUserIds)
        {
            var (newlyDowned, failClaimed) = Progress.MarkDowned(userId);
            if (newlyDowned)
                packets.Add(new S_PlayerDead { UserId = userId });
            if (failClaimed)
                packets.Add(new S_DungeonFailed { RoomId = RoomId });
        }

        return packets;
    }
}
