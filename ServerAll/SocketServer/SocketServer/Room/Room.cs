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

// 방 관리(입장·퇴장·재접속 유예·진행 판정). 전투·진행 판정은 Actors.SyncRoot 하나만 잡는다(락 중첩 없음).
// 다운은 별도 집합이 아니라 액터의 Dead 태그 — 진실원이 하나라 클라 자기신고와 어긋나지 않는다.
public class Room
{
    public long RoomId { get; private set; }
    public int MaxMembers { get; private set; }

    public string MapId { get; set; } = Shared.Infrastructure.Spawn.MapIds.Default;

    public RoomSessions Sessions { get; }

    public ActorStore Actors { get; } = new();

    public DungeonProgress Progress { get; }

    private readonly RoomSimulation _simulation;

    private readonly ILogger<Room> _logger;

    private MapBounds _bounds = MapBounds.Unbounded;

    // 별도 저장소, 자기 락 소유.
    public GroundItemStore Loot { get; } = new();

    // 재접속 유예(ms). 방에 다른 플레이어가 남아 있을 때만 적용 — 전원 끊겨 방이 비면 즉시 제거. 만료 정리는 RoomTickService 스윕.
    public const long ReconnectGraceMs = 60_000;


    public const int DefaultMaxHp = 100;

    public const int DefaultMaxMana = 100;

    public MapBounds Bounds => _bounds;

    private int _nextEffectInstanceId;

    public int NextEffectInstanceId() => System.Threading.Interlocked.Increment(ref _nextEffectInstanceId);

    // 적용한 소비 통지 id — 재배달 시 이중 회복 차단(lock 안에서만 접근). 수명은 방(회복 대상이 방 인메모리라).
    private readonly HashSet<string> _handledConsumeIds = new();

    // consumeId 가 비어 있으면(구 메시지) 차단하지 않는다.
    public bool TryMarkConsumeHandled(string consumeId)
    {
        if (string.IsNullOrEmpty(consumeId))
            return true;

        lock (_handledConsumeIds)
            return _handledConsumeIds.Add(consumeId);
    }

    // participants 는 정원(MaxMembers) 결정에만 쓴다. 입장 자격은 GameServer 가 선기입한 Redis 입장권
    // (gamesession:player:{userId})이 판정 — 인메모리 목록은 재시작에 소실되므로 인증 근거 아님.
    public Room(long roomId, IReadOnlyList<PlayerInfo> participants, ILogger<Room> logger)
    {
        RoomId = roomId;
        MaxMembers = participants.Count;
        _logger = logger;
        Sessions = new RoomSessions(roomId, MaxMembers, logger);
        Progress = new DungeonProgress(Actors);
        _simulation = new RoomSimulation(Actors, NextEffectInstanceId, logger);
    }

    // graceful=true(크래시/끊김): 참가자·액터를 ReconnectGraceMs 동안 보존. false(C_PlayerLeave): 즉시 제거.
    // 세션은 어느 쪽이든 즉시 제거. 빈 방 처리·이벤트 발행은 RoomManager.
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
                    member.DisconnectedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
                else
                {
                    Actors.RemoveMember(userId.Value);
                }
            }
        }

        return true;
    }

    // 입장/재접속: HasJoined 표시(몬스터 AI 타깃 시작) + 유예 마킹 해제. false = 보존된 참가자 없음(유예 만료/명시 퇴장).
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

    // 유예 만료된 참가자를 제거하고 userId 반환. 영구 퇴장 확정(S_PlayerLeft·association 정리)은 RoomManager.
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

    // 스탯은 GameServer 권위값 — 받아 쓰기만 한다.
    public void AddPlayer(long userId, string nickname, int spawnIndex, float spawnX, float spawnY, float spawnZ,
        float rotY,
        int attackPower = 0, int defense = 0, int maxHealth = 0, int maxMana = 0,
        IReadOnlyList<string>? abilityIds = null)
    {
        // 0(미설정)이면 상수 폴백(테스트·레거시 경로 호환).
        int hp = maxHealth > 0 ? maxHealth : DefaultMaxHp;
        int mana = maxMana > 0 ? maxMana : DefaultMaxMana;

        Actors.AddPlayer(userId, nickname, spawnIndex, spawnX, spawnY, spawnZ, rotY,
            attackPower, defense, hp, mana,abilityIds);

        _logger.LogInformation(
            "Initialized player {UserId} ({Nickname}) slot {SpawnIndex} at ({SpawnX}, {SpawnY}, {SpawnZ}) in Room {RoomId}",
            userId, nickname, spawnIndex, spawnX, spawnY, spawnZ, RoomId);
    }

    // 서버 스폰의 단일 진입점 — 모든 스폰 원인은 저장소에 직접 추가하지 말고 이 메서드를 경유한다.
    // mapMonsterLevel 0 = 미저작 → 스폰별 Level 도 없으면 L1.
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

                // 레벨은 스폰 시 1회 확정(매 틱 재계산 안 함). 등급은 카탈로그(monsterId 행)에서 온다.
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
                    // 몬스터는 Health 만 보유 — 공격력·방어력·마나는 0 이 아니라 미보유.
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

    // 다운 집계·실패 판정은 방이 한다. 전송 I/O 는 호출자(RoomTickService)가 락 밖에서.
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