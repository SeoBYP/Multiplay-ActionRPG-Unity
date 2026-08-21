using Script.System.GamePlayAbilitySystem;
using Server.Combat;
using Server.Diagnostics;
using Server.Loot;
using Server.Monster;
using Server.Player;
using Shared.Infrastructure.Abilities;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;

namespace Server.Room;

using Microsoft.Extensions.Logging;
using Shared.Packet.Packets;

public class Room
{
    public long RoomId { get; private set; }
    public int MaxMembers { get; private set; }

    /// <summary>플레이 중인 맵 식별자. 스폰 레이아웃 선택에 사용. CreateRoom 에서 설정.</summary>
    public string MapId { get; set; } = Shared.Infrastructure.Spawn.MapIds.Default;

    private readonly Dictionary<ulong, Session> _playerSessions = new();
    private readonly Dictionary<long, PlayerState> _playerStates = new();
    private readonly HashSet<long> _expectedUserIds;
    private readonly ILogger<Room> _logger;

    // 서버 권위 몬스터 상태 (instanceId → state). 접근 시 반드시 lock(_monsters).
    private readonly Dictionary<int, MonsterState> _monsters = new();
    private MapBounds _bounds = MapBounds.Unbounded;
    private int _nextMonsterInstanceId;

    // 서버 권위 바닥 아이템 (groundId → item). 접근 시 반드시 lock(_groundItems).
    private readonly Dictionary<int, GroundItem> _groundItems = new();
    private int _nextGroundItemId;

    /// <summary>줍기 가능 반경(평면 거리). 너무 먼 위치에서의 줍기 요청을 거른다.</summary>
    public const float PickupRange = 3f;

    /// <summary>
    /// 재접속 유예 창(ms). 크래시/끊김(graceful) 시 PlayerState 를 즉시 지우지 않고 이 시간 동안 보존한다.
    /// 방에 다른 플레이어가 남아 있는 한, 끊긴 플레이어가 이 안에 재접속하면 보존 상태로 즉시 던전 복귀.
    /// 만료되면 RoomTickService 스윕이 상태를 정리하고 영구 퇴장으로 확정한다.
    /// (전원 끊겨 방이 비면 방 자체가 즉시 제거되므로 유예 대상 아님 — 클라는 "방 종료" 팝업.)
    /// </summary>
    public const long ReconnectGraceMs = 60_000;

    // 클리어 감지(몬스터 전멸) — lock(_monsters) 안에서만 접근.
    private bool _monstersSpawned;   // 한 번이라도 몬스터가 스폰됐는지(빈 방을 클리어로 오판 방지)

    // 던전 결과(클리어/실패) — 단일 terminal 상태. 0=None,1=Cleared,2=Failed.
    // Interlocked.CompareExchange 로 최초 1회만 claim → 클리어/실패 상호 배타(둘 다 발화 불가).
    private int _outcome;
    // 실패 집계(다운된 참가자) — lock(_downed) 안에서만 접근.
    private readonly HashSet<long> _downed = new();

    /// <summary>플레이어 기본 최대 HP(서버 권위). 후속에 Progression/스탯에서 주입. 클라 prefab ASC(100)와 정렬.</summary>
    public const int DefaultMaxHp = 100;

    /// <summary>스탯 미설정(테스트/레거시) 시 마나 상한 폴백. 클라 prefab Mana(100) 기준선과 정렬.</summary>
    public const int DefaultMaxMana = 100;

    /// <summary>맵 경계 — 몬스터 이동 clamp 기준(RoomTickService 사용).</summary>
    public MapBounds Bounds => _bounds;

    // 서버 권위 GameplayEffect InstanceId 발급기 (방 단위, 스레드 안전).
    private int _nextEffectInstanceId;

    /// <summary>활성 Effect 인스턴스에 부여할 서버 권위 InstanceId를 1씩 증가시켜 반환한다.</summary>
    public int NextEffectInstanceId() => System.Threading.Interlocked.Increment(ref _nextEffectInstanceId);
    

    public int MemberCount
    {
        get
        {
            lock (_playerSessions)
            {
                return _playerSessions.Count;
            }
        }
    }

    public bool IsFull => MemberCount >= MaxMembers;

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
    }

    public bool IsExpectedPlayer(long userId) => _expectedUserIds.Contains(userId);

    public Session? GetSession(ulong sessionId)
    {
        lock (_playerSessions)
        {
            return _playerSessions.GetValueOrDefault(sessionId);
        }
    }

    public PlayerState? GetPlayerState(long userId)
    {
        lock (_playerStates)
        {
            return _playerStates.GetValueOrDefault(userId);
        }
    }

    /// <summary>
    /// 같은 UserId 의 <b>다른</b> 세션을 찾는다(재접속 인수용). 없으면 null.
    /// 끊김은 즉시 감지되지 않는다 — FIN 없이 사라지면 유휴 타임아웃까지 옛 세션이 방에 남는다(실측 63초).
    /// </summary>
    public Session? FindSessionByUserId(long userId, ulong exceptSessionId)
    {
        if (userId <= 0) return null;
        lock (_playerSessions)
        {
            foreach (var kv in _playerSessions)
                if (kv.Key != exceptSessionId && kv.Value.UserId == userId)
                    return kv.Value;
        }
        return null;
    }

    public bool Join(Session session)
    {
        try
        {
            lock (_playerSessions)
            {
                if (IsFull)
                {
                    _logger.LogWarning("Room {RoomId} is full. Session {SessionId} cannot join", RoomId, session.SessionId);
                    return false;
                }

                if (_playerSessions.ContainsKey(session.SessionId))
                {
                    _logger.LogWarning("Session {SessionId} is already in room {RoomId}", session.SessionId, RoomId);
                    return false;
                }

                _playerSessions.Add(session.SessionId, session);
                _logger.LogInformation(
                    "Session {SessionId} joined room {RoomId}. Members: {MemberCount}/{MaxMembers}",
                    session.SessionId,
                    RoomId,
                    MemberCount,
                    MaxMembers);

                return true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to join room {RoomId}", RoomId);
            throw;
        }
    }

    /// <param name="graceful">
    /// true = 크래시/네트워크 끊김(C_PlayerLeave 없음). PlayerState 를 즉시 지우지 않고
    ///        <see cref="ReconnectGraceMs"/> 동안 보존(DisconnectedAtMs 마킹) → 재접속 시 복귀.
    /// false = 명시 퇴장(C_PlayerLeave). PlayerState 즉시 제거(영구 퇴장).
    /// 어느 쪽이든 세션(_playerSessions)은 즉시 제거된다. 빈 방 처리·이벤트 발행은 RoomManager 책임.
    /// </param>
    public bool Leave(ulong sessionId, bool graceful = false)
    {
        try
        {
            long userId;
            lock (_playerSessions)
            {
                if (!_playerSessions.TryGetValue(sessionId, out var session))
                {
                    _logger.LogWarning("Session {SessionId} is not in room {RoomId}", sessionId, RoomId);
                    return false;
                }

                userId = session.UserId;
                _playerSessions.Remove(sessionId);

                _logger.LogInformation(
                    "Session {SessionId} left room {RoomId}. Members: {MemberCount}/{MaxMembers} (graceful={Graceful})",
                    sessionId,
                    RoomId,
                    MemberCount,
                    MaxMembers,
                    graceful);
            }

            if (userId > 0)
            {
                lock (_playerStates)
                {
                    if (graceful && _playerStates.TryGetValue(userId, out var state))
                    {
                        // 크래시/끊김: 상태 보존 + 끊긴 시각 마킹. 재접속 유예 창 동안 AI 타깃에선 제외(TickMonsters).
                        state.DisconnectedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }
                    else
                    {
                        // 명시 퇴장(또는 상태 없음): 유령 잔류 방지로 즉시 제거.
                        _playerStates.Remove(userId);
                    }
                }
            }

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to leave room {RoomId}", RoomId);
            throw;
        }
    }

    /// <summary>
    /// 입장/재접속 시 호출(C_PlayerJoin 성공) — 플레이어를 라이브 상태로 활성화한다:
    /// HasJoined=true 로 표시(이제부터 몬스터 AI 타깃)하고, 끊김 유예 중이었다면 DisconnectedAtMs
    /// 마킹을 해제해 보존 상태로 즉시 복귀시킨다. 반환 false = 보존된 상태가 없음(유예 만료/명시 퇴장 후 → 재입장 불가).
    /// </summary>
    public bool MarkJoined(long userId)
    {
        lock (_playerStates)
        {
            if (!_playerStates.TryGetValue(userId, out var state))
                return false;

            state.DisconnectedAtMs = null;
            state.HasJoined = true;
            return true;
        }
    }

    /// <summary>
    /// 유예 창이 만료된 끊김 플레이어 상태를 제거하고 그 userId 목록을 반환한다(RoomTickService 가 10Hz 로 호출).
    /// 반환된 userId 는 RoomManager 가 영구 퇴장으로 확정(S_PlayerLeft 브로드캐스트 + association 정리).
    /// </summary>
    public List<long> SweepExpiredDisconnected(long nowMs, long graceMs)
    {
        List<long>? expired = null;
        lock (_playerStates)
        {
            foreach (var (userId, state) in _playerStates)
            {
                if (state.DisconnectedAtMs is { } at && nowMs - at >= graceMs)
                    (expired ??= new List<long>()).Add(userId);
            }

            if (expired != null)
                foreach (var userId in expired)
                    _playerStates.Remove(userId);
        }

        return expired ?? EmptyUserIds;
    }

    private static readonly List<long> EmptyUserIds = new();

    public void InitPlayerState(long userId, string nickname, int spawnIndex, float spawnX, float spawnY, float spawnZ, float rotY,
        int attackPower = 0, int defense = 0, int maxHealth = 0, int maxMana = 0)
    {
        lock (_playerStates)
        {
            // MaxHp/MaxMana = GameServer 가 보낸 스탯(권위). 0(미설정)이면 상수 폴백(테스트·레거시 경로 호환).
            int hp = maxHealth > 0 ? maxHealth : DefaultMaxHp;
            int mana = maxMana > 0 ? maxMana : DefaultMaxMana;
            var playerState = new PlayerState
            {
                UserId = userId,
                Nickname = nickname,
                SpawnIndex = spawnIndex,
                PosX = spawnX,
                PosY = spawnY,
                PosZ = spawnZ,
                RotY = rotY,
                LastMovedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                // 서버 권위 HP/마나 — 입장 시 만피/만마로 초기화. 상한은 GameServer 합산 스탯(authority-model §4c).
                Hp = hp,
                MaxHp = hp,
                Mana = mana,
                MaxMana = mana,
                AttackPower = attackPower,
                Defense = defense,
            };

            _playerStates[userId] = playerState;

            _logger.LogInformation(
                "Initialized player state for User {UserId} ({Nickname}) slot {SpawnIndex} at ({SpawnX}, {SpawnY}, {SpawnZ}) in Room {RoomId}",
                userId,
                nickname,
                spawnIndex,
                spawnX,
                spawnY,
                spawnZ,
                RoomId);
        }
    }

    public void UpdatePlayerState(long userId, float x, float y, float z, float rotY, long timestamp)
    {
        lock (_playerStates)
        {
            if (_playerStates.TryGetValue(userId, out var playerState))
            {
                playerState.PosX = x;
                playerState.PosY = y;
                playerState.PosZ = z;
                playerState.RotY = rotY;
                playerState.LastMovedAt = timestamp;
            }
            else
            {
                _logger.LogWarning("Player state not found for User {UserId} in Room {RoomId}", userId, RoomId);
            }
        }
    }

    public IReadOnlyList<PlayerState> GetAllPlayerStates()
    {
        lock (_playerStates)
        {
            return _playerStates.Values.ToList();
        }
    }

    /// <summary>
    /// 모든 플레이어 마나를 시간 비례 자연 회복(서버 권위). RoomTickService 가 매 틱 호출한다.
    /// 동기화 패킷은 발행하지 않는다 — 클라가 같은 rate(<see cref="ManaConfig.RegenPerSecond"/>)로 예측해 수렴.
    /// </summary>
    public void RegenAllPlayerMana(float dt)
    {
        lock (_playerStates)
        {
            foreach (var p in _playerStates.Values)
                p.RegenMana(dt);
        }
    }

    // ── 몬스터 ───────────────────────────────────────────

    /// <summary>
    /// 맵 레이아웃의 몬스터 정의로 스폰(wave 0). InstanceId 는 방 단위 순차 발급.
    ///
    /// **서버 스폰의 단일 진입점(불변식)** — 모든 스폰 *원인*(현재=던전 시작/`RoomManager`,
    /// 미래=웨이브 4.1.6·퀘스트 4.4·존 4.6.1·서버 리스폰)은 `_monsters` 에 직접 추가하지 말고
    /// **반드시 이 메서드를 경유**한다. 원인이 여럿이 되면 이 한 점을 `SpawnSystem`(이벤트 라우터)로
    /// 감싸 "왜 스폰(원인)"과 "어떻게 스폰(여기)"을 분리한다. 설계·승격 트리거 = docs/wiki/spawn-system-evolution.md.
    /// </summary>
    /// <param name="mapMonsterLevel">
    /// 던전 기본 몬스터 레벨(AC-E2). 0 = 미저작 → 스폰별 Level 도 없으면 L1(레벨 도입 전과 동일 동작).
    /// </param>
    public void SpawnMonsters(IReadOnlyList<MonsterSpawnDef> defs, MapBounds bounds, int mapMonsterLevel = 0)
    {
        lock (_monsters)
        {
            _bounds = bounds ?? MapBounds.Unbounded;
            foreach (var def in defs ?? Array.Empty<MonsterSpawnDef>())
            {
                var stats = MonsterCatalog.Get(def.MonsterId);
                int count = Math.Max(1, def.Count);

                // 레벨은 **스폰 시 1회 확정**한다(monster-leveling.md §4.1) — 매 틱 재계산하지 않는다.
                // 등급은 스폰이 아니라 **카탈로그(monsterId 행)** 에서 온다(AC-G) — monsterId 가 곧 변종이다.
                int level = MapSpawnLayout.ResolveLevel(def.Level, mapMonsterLevel);
                int maxHp = Shared.Infrastructure.Monsters.MonsterLevelScaling.Hp(stats.MaxHp, level);

                for (int i = 0; i < count; i++)
                {
                    int id = ++_nextMonsterInstanceId;
                    _monsters[id] = new MonsterState
                    {
                        InstanceId = id,
                        MonsterId  = def.MonsterId,
                        Level = level,
                        // 등급은 카탈로그(monsterId 행)에서 직접 읽는다(AC-G) — MonsterStats 는 시뮬 전용 뷰라
                        // 표시·연출용 등급을 담지 않는다.
                        Tier = Shared.Infrastructure.Monsters.MonsterCatalog.Get(def.MonsterId).Tier,
                        PosX = def.X, PosY = def.Y, PosZ = def.Z,
                        SpawnX = def.X, SpawnZ = def.Z,
                        RotY = def.RotY,
                        MaxHp = maxHp,
                        Hp = maxHp,
                        Phase = MonsterPhase.Idle,
                        Patrol = def.Patrol,
                        PatrolIndex = 0,
                    };
                }
            }

            if (_monsters.Count > 0)
                _monstersSpawned = true;

            _logger.LogInformation("Room {RoomId} spawned {Count} monsters", RoomId, _monsters.Count);
        }
    }

    /// <summary>
    /// 몬스터가 전멸했는지 검사하고, 전멸이면 클리어를 <b>최초 1회만</b> true 로 표시한다.
    /// 스폰된 적이 있어야(빈 방 오판 방지) &amp;&amp; 살아있는 몬스터 0 일 때만 true.
    /// 사망 몬스터는 DamageMonster 가 즉시 제거하므로 _monsters.Count==0 == 전멸.
    /// 호출 위치: 몬스터 처치 직후(CombatHandler). 동시 호출돼도 최초 1회만 발화한다.
    /// </summary>
    public bool TryMarkCleared()
    {
        lock (_monsters)
        {
            if (!_monstersSpawned || _monsters.Count > 0)
                return false;
        }

        // 전멸 확인 후 terminal 상태를 원자적으로 claim — 실패와 동시 발화 불가.
        return System.Threading.Interlocked.CompareExchange(ref _outcome, 1, 0) == 0;
    }

    /// <summary>
    /// 한 플레이어의 다운(HP 0)을 집계한다. 반환:
    ///   NewlyDowned = 이 호출로 처음 다운됐는가(중복/유령 가드 — S_PlayerDead 1회 발화용).
    ///   FailClaimed = 이 다운으로 <b>참가자 전원</b> 다운이 돼 실패를 최초 claim 했는가(상호 배타).
    /// 기대 로스터에 없는 userId 는 (false,false). 서버 사망감지(TickMonsters)·C_PlayerDead 양쪽이 호출.
    /// </summary>
    public (bool NewlyDowned, bool FailClaimed) MarkPlayerDowned(long userId)
    {
        bool newly;
        bool allDown;
        lock (_downed)
        {
            if (!_expectedUserIds.Contains(userId))
                return (false, false);

            newly = _downed.Add(userId);
            allDown = _downed.Count >= _expectedUserIds.Count;
        }

        bool failClaimed = allDown
            && System.Threading.Interlocked.CompareExchange(ref _outcome, 2, 0) == 0;
        return (newly, failClaimed);
    }

    /// <summary>하위호환: 전원 다운 시 실패 claim 여부만 반환(기존 C_PlayerDead 핸들러·테스트).</summary>
    public bool TryMarkFailed(long userId) => MarkPlayerDowned(userId).FailClaimed;

    /// <summary>
    /// Co-op 부활(2.5.2, 서버 권위). 시전자가 다운된 아군을 살린다. 검증:
    ///   ① 자기 자신 아님 ② 던전 미실패(_outcome≠Failed) ③ 시전자 생존·입장 ④ 대상 다운 상태
    ///   ⑤ 평면 거리 ≤ <see cref="ReviveConfig.RangeMeters"/>.
    /// 통과 시 대상을 _downed 에서 제거하고 HP 를 <see cref="ReviveConfig.RestorePercent"/>% 로 복구.
    /// 홀드 시간은 클라 UX(시전 채널) — 서버는 게임의미 불변식만 본다(사용자 결정).
    /// 반환: (성공, 복구된 HP). 멱등 — 이미 부활/미다운이면 (false,0).
    /// </summary>
    public (bool Ok, int NewHp) TryRevive(long reviverId, long targetId)
    {
        if (reviverId == targetId)
            return (false, 0);
        if (System.Threading.Volatile.Read(ref _outcome) == 2) // 전원 다운(실패) 확정 후엔 부활 불가
            return (false, 0);

        float rx, rz, tx, tz;
        int targetMaxHp;
        lock (_playerStates)
        {
            if (!_playerStates.TryGetValue(reviverId, out var reviver)
                || !reviver.HasJoined || reviver.IsDowned || reviver.DisconnectedAtMs is not null)
                return (false, 0); // 시전자 미입장/다운/끊김 → 부활 불가
            if (!_playerStates.TryGetValue(targetId, out var target))
                return (false, 0);
            rx = reviver.PosX; rz = reviver.PosZ;
            tx = target.PosX; tz = target.PosZ;
            targetMaxHp = target.MaxHp;
        }

        float dx = rx - tx, dz = rz - tz;
        if (dx * dx + dz * dz > ReviveConfig.RangeMeters * ReviveConfig.RangeMeters)
            return (false, 0); // 거리 밖

        // 대상이 실제 다운 상태여야 부활. _downed 에서 제거되면 멱등(중복 C_Revive 차단).
        lock (_downed)
        {
            if (!_downed.Remove(targetId))
                return (false, 0);
        }

        int hp = System.Math.Max(1, targetMaxHp * ReviveConfig.RestorePercent / 100);
        lock (_playerStates)
        {
            if (_playerStates.TryGetValue(targetId, out var target))
                target.Hp = hp;
        }
        return (true, hp);
    }

    public IReadOnlyList<MonsterState> GetAllMonsters()
    {
        lock (_monsters)
        {
            return _monsters.Values.ToList();
        }
    }

    public MonsterState? GetMonster(int instanceId)
    {
        lock (_monsters)
        {
            return _monsters.GetValueOrDefault(instanceId);
        }
    }

    public bool RemoveMonster(int instanceId)
    {
        lock (_monsters)
        {
            return _monsters.Remove(instanceId);
        }
    }

    /// <summary>
    /// 몬스터에 GAS Health 모디파이어를 적용(서버 권위). 새 HP 를 GameplayEffectMath 로 집계하고,
    /// 0 이하면 방에서 제거한다. 반환: (적중=대상 존재·생존, 새 HP, 이번에 사망).
    /// </summary>
    public (bool Hit, int NewHp, bool Dead) DamageMonster(int instanceId, IReadOnlyList<GameplayAttributeModifier> mods)
    {
        lock (_monsters)
        {
            if (!_monsters.TryGetValue(instanceId, out var m) || m.IsDead)
                return (false, 0, false);

            // 몬스터는 Health 만 가진다 — Health 모디파이어만 집계.
            var healthMods = mods.Where(x => x.AttributeType == EGameplayAttribute.Health);
            m.Hp = GameplayEffectMath.Aggregate(m.Hp, healthMods, m.MaxHp);

            bool dead = m.IsDead;
            if (dead)
                _monsters.Remove(instanceId);

            return (true, m.Hp, dead);
        }
    }

    /// <summary>
    /// 서버 권위 플레이어 HP 에 GAS Health 모디파이어 적용(데미지=음수/회복=양수 공용).
    /// `GameplayEffectMath.Aggregate`(클라와 동일 함수) → 서버 HP == 클라 HP. HP≤0 이면 다운 집계.
    /// 반환: (적용 후 HP, 이번에 처음 다운, 전원다운 실패 claim). 미존재 userId 는 (0,false,false).
    /// </summary>
    public (int NewHp, bool NewlyDowned, bool FailClaimed) ApplyPlayerEffect(
        long userId, IReadOnlyList<GameplayAttributeModifier> mods)
    {
        int newHp;
        lock (_playerStates)
        {
            if (!_playerStates.TryGetValue(userId, out var p))
                return (0, false, false);

            var healthMods = mods.Where(x => x.AttributeType == EGameplayAttribute.Health);
            p.Hp = GameplayEffectMath.Aggregate(p.Hp, healthMods, p.MaxHp);
            newHp = p.Hp;
        }

        if (newHp > 0)
            return (newHp, false, false);

        // HP 0 — 다운 집계는 _playerStates 락 밖에서(MarkPlayerDowned 가 _downed/_outcome 락).
        var (newly, failClaimed) = MarkPlayerDowned(userId);
        return (newHp, newly, failClaimed);
    }

    /// <summary>
    /// 한 틱 몬스터 시뮬레이션. 플레이어 스냅샷을 먼저 떠(락 비중첩) 각 몬스터를 MonsterAiMath 로 진행하고,
    /// 브로드캐스트할 패킷 목록(이동 S_MonsterState + 공격 S_ApplyEffect)을 반환한다(전송 I/O 는 호출자가 락 밖에서).
    ///
    /// 공격(⑤b): 몬스터가 Attack 페이즈 + 쿨다운 경과 시 최근접 플레이어(MonsterAiMath.Step 반환 인덱스)에
    /// monster_attack_dmg 효과를 브로드캐스트. 플레이어 HP 는 클라가 공유 카탈로그로 결정론 계산(서버 권위 X).
    /// </summary>
    public List<Packet> TickMonsters(float dt, long nowMs)
    {
        // 다운(HP 0 보고)된 플레이어는 AI 타깃에서 제외 — 죽은(다운) 플레이어를 몬스터가 계속 때리지 않도록.
        // 다운 = C_PlayerDead → DungeonLifecycleHandler → TryMarkFailed 로 _downed 에 집계됨.
        HashSet<long> downed;
        lock (_downed) downed = new HashSet<long>(_downed);

        List<PlayerState> players;
        lock (_playerStates)
        {
            // 타깃 자격: 미입장(HasJoined=false, GameStart 로 상태만 초기화·소켓 미입장)·끊김(재접속 유예 중)·
            // 다운 플레이어는 제외. 미입장 제외가 없으면 입장 전에 몬스터가 죽여 S_PlayerDead 가 빈 방에 유실된다.
            players = _playerStates.Values
                .Where(p => p.HasJoined && p.DisconnectedAtMs is null && !downed.Contains(p.UserId))
                .ToList();
        }

        var positions = new List<PlayerPos>(players.Count);
        foreach (var p in players)
            positions.Add(new PlayerPos(p.PosX, p.PosZ));

        var outPackets = new List<Packet>();
        lock (_monsters)
        {
            foreach (var m in _monsters.Values)
            {
                if (m.IsDead) continue;

                var stats = MonsterCatalog.Get(m.MonsterId);
                int targetIdx = MonsterAiMath.Step(m, positions, _bounds, stats, dt);

                // dirty-flag(§5.2): 위치·회전·HP·페이즈가 직전 송신과 같으면 생략 → Idle 경비 몬스터 트래픽 0.
                // 신규 입장자는 S_SpawnMonster 로스터로 최신 상태를 받으므로 유실 없음. Chase/Patrol 은 매 틱 변해 그대로 송신.
                if (m.StateDirty())
                {
                    outPackets.Add(new S_MonsterState
                    {
                        InstanceId = m.InstanceId,
                        PosX = m.PosX, PosY = m.PosY, PosZ = m.PosZ,
                        RotY = m.RotY,
                        Hp = m.Hp,
                        Phase = (byte)m.Phase,
                        // AC-C3: 여기서 만든 이 패킷은 **RoomTickService 가 나중에** 보낸다. 그 사이 데미지가 끼면
                        // 이 옛 HP 가 새 HP 뒤에 도착한다 → 클라가 Seq 로 버린다. 생성 시점 스탬프가 핵심.
                        Seq = m.NextSeq(),
                    });
                    m.MarkStateSent();
                }

                // ⑤b/AC-B: Attack 페이즈 → 사거리·쿨다운을 만족하는 **어빌리티**를 골라 발동(보스 다중 스킬 지원).
                // 발동 게이트 = AbilityActivationMath(플레이어 CombatHandler 와 동일 Shared 규칙). 몬스터는 마나·차단태그 없음.
                if (m.Phase == MonsterPhase.Attack && targetIdx >= 0)
                {
                    var target = players[targetIdx];
                    var chosen = SelectMonsterAbility(m, target, nowMs);
                    if (chosen is null)
                        continue; // 사거리 밖이거나 전부 쿨다운 — 이번 틱은 발동 없음

                    m.MarkCast(chosen.Id, nowMs); // 쿨다운 시작(어빌리티 단위)
                    long targetUserId = target.UserId;
                    long attackerActorId = ActorIds.FromMonster(m.InstanceId); // 부호 규약: 몬스터=음수

                    // AC: 발동 = "이 액터가 스킬을 썼다" 통합 연출 신호. i-frame 으로 빗나가도(헛스윙) 스윙 애니는 나가야 하므로
                    // 데미지 판정(무적 continue)보다 먼저 broadcast. 클라 라우터가 NetworkId 로 어빌리티 Cue 를 해석해 재생.
                    outPackets.Add(new S_AbilityActivated { ActorId = attackerActorId, SkillId = chosen.NetworkId });

                    _logger.LogInformation("[GameplayAbility] monster {MonsterId}(actor {ActorId}) 발동: '{AbilityId}' → user {UserId}",
                        m.MonsterId, attackerActorId, chosen.Id, targetUserId);

                    // 회피 무적(i-frame): 무적 창 안이면 이 공격은 빗나간다(피해/effect 없음).
                    // 쿨다운은 이미 소모(MarkCast) — 몬스터가 헛스윙한 것. 던전=서버 권위 게이트.
                    if (target.IsInvulnerableAt(nowMs))
                        continue;

                    // 데미지 = 어빌리티 BaseDamage − 플레이어 Defense (Shared 결정론, 플레이어→몬스터와 동일 산식).
                    // 스탯 의존이라 클라가 자체계산 불가 → 서버가 권위 수치를 Amount 로 전달하고, HP 도 같은 값으로 차감.
                    const int MonsterAttackPower = 0; // 몬스터 공격력 스탯 미도입 — base 가 곧 공격력

                    // AC-E3: **산식은 그대로, base 만 레벨·등급으로 스케일**한다.
                    // 원래 버그는 산식이 아니라 base 가 고정이라 플레이어 DEF(+2/L) 성장에 밀린 것이었다
                    // (C1c 실측: L19 부터 전 몬스터 1 데미지). 유도 = monster-leveling.md §2.
                    int scaledBase = Shared.Infrastructure.Monsters.MonsterLevelScaling.Damage(
                        chosen.BaseDamage, m.Level);
                    int finalDamage = StatCombatMath.MeleeDamage(scaledBase, MonsterAttackPower, target.Defense);
                    var dmgMods = new[]
                    {
                        GameplayAttributeModifier.Create(EGameplayAttribute.Health, -finalDamage, EModifierType.Additive),
                    };

                    // 트레이스(AC-C1a): 플레이어→몬스터와 **같은 산식·다른 입력**(AP=0, DEF=대상). 그 대비가 보여야 밸런스를 논할 수 있다.
                    // 플레이어 HP 권위는 클라(결정론 lite)라 서버는 before/after 를 모른다 → 0.
                    CombatTrace.Damage(
                        CombatPath.MonsterToPlayer, CombatTrace.FormulaMelee,
                        attackerActorId, ActorIds.FromPlayer(targetUserId),
                        chosen.Id, chosen.NetworkId,
                        // 저작값(chosen.BaseDamage)이 아니라 **실제 산식에 들어간 값**을 찍는다 —
                        // 트레이스가 거짓말하면 진단이 아니라 오도다(C1a 교훈).
                        scaledBase, MonsterAttackPower, target.Defense, finalDamage,
                        targetHpBefore: 0, targetHpAfter: 0,
                        recvMs: nowMs, judgeMs: nowMs, seq: 0); // 틱 경로 = 수신·판정이 같은 틱 시각

                    outPackets.Add(new S_ApplyEffect
                    {
                        InstanceId = NextEffectInstanceId(),
                        EffectId = global::Server.PacketHandler.Handler.CombatHandler.AbilityDamageEffectId, // AC-B 안B: 데미지 단일 라벨(수치=ability.BaseDamage → Amount)
                        TargetId = targetUserId,
                        SourceId = attackerActorId, // AC: 몬스터 = -instanceId (기존 0 승격)
                        StartTick = nowMs,
                        Stacks = 1,
                        Amount = -finalDamage, // 서버 권위 Health 델타(클라가 그대로 적용)
                    });

                    // CC(상태이상): 어빌리티의 OnHitEffectIds(태그/CC 전용)를 데미지와 함께 브로드캐스트.
                    // Amount=0 = HP 변경 없는 상태태그(Duration+GrantedTags) → 클라 EffectReceiver 가 적용 → 입력/이동 게이트.
                    foreach (var ccId in chosen.Timeline.OnHitEffectIds)
                    {
                        if (string.IsNullOrEmpty(ccId)) continue;
                        outPackets.Add(new S_ApplyEffect
                        {
                            InstanceId = NextEffectInstanceId(),
                            EffectId = ccId,
                            TargetId = targetUserId,
                            SourceId = attackerActorId, // AC: 몬스터 = -instanceId
                            StartTick = nowMs,
                            Stacks = 1,
                            Amount = 0,
                        });
                    }

                    // 서버 권위 HP 누적 + 사망 직접 감지(클라 보고에 의존 안 함 → 불사 핵 차단).
                    var (_, newlyDowned, failClaimed) = ApplyPlayerEffect(targetUserId, dmgMods);
                    if (newlyDowned)
                        outPackets.Add(new S_PlayerDead { UserId = targetUserId });
                    if (failClaimed)
                        outPackets.Add(new S_DungeonFailed { RoomId = RoomId });
                }
            }
        }
        return outPackets;
    }

    /// <summary>
    /// 이 몬스터가 지금 대상에게 쓸 수 있는 어빌리티를 고른다(AC-B B4). 없으면 null.
    /// 규칙: 저작 순서(MonsterDefinition.abilityIds) = **우선순위** → 사거리 안 + 쿨다운 경과인 **첫 어빌리티**.
    /// 보스가 여러 스킬을 가지면 앞에 둔 강한 스킬을 먼저 쓰고, 쿨다운이면 뒤의 평타로 폴백하는 식으로 저작한다.
    /// (순수 판정 — 상태 변경은 호출자가 MarkCast 로 커밋)
    /// </summary>
    private static AbilityDef? SelectMonsterAbility(MonsterState m, PlayerState target, long nowMs)
    {
        float dx = target.PosX - m.PosX;
        float dz = target.PosZ - m.PosZ;
        float distSq = dx * dx + dz * dz;

        foreach (var ability in MonsterCatalog.GetAbilities(m.MonsterId))
        {
            if (distSq > ability.ActivationRange * ability.ActivationRange)
                continue; // 이 스킬 사거리 밖
            if (!AbilityActivationMath.CanActivate(
                    nowMs, m.GetLastCast(ability.Id), ability.Timeline.CooldownMs,
                    manaCost: 0, currentMana: 0, blocked: false))
                continue; // 쿨다운 중
            return ability;
        }
        return null;
    }

    // ── 바닥 아이템(루트/드랍) ───────────────────────────────

    /// <summary>
    /// 드랍 roll 결과 1건을 바닥에 스폰(서버 권위). GroundId 는 방 단위 순차 발급.
    /// 브로드캐스트(S_SpawnGroundItem)는 호출자 책임 — 여기선 상태 추가만.
    /// </summary>
    public GroundItem SpawnGroundItem(int itemId, int qty, float x, float y, float z)
    {
        lock (_groundItems)
        {
            int id = ++_nextGroundItemId;
            var item = new GroundItem
            {
                GroundId = id,
                ItemId = itemId,
                Qty = qty,
                PosX = x, PosY = y, PosZ = z,
            };
            _groundItems[id] = item;
            return item;
        }
    }

    /// <summary>현재 바닥 아이템 전체(입장 시 로스터 재전송용).</summary>
    public IReadOnlyList<GroundItem> GetAllGroundItems()
    {
        lock (_groundItems)
        {
            return _groundItems.Values.ToList();
        }
    }

    /// <summary>
    /// 줍기 시도(경쟁 중재) — 거리 검증 후, 바닥에서 <b>제거에 성공한 1명만</b> 아이템을 가져간다.
    /// 반환 non-null = 줍기 확정(이 플레이어가 가져감). null = 없음(이미 주워짐=경쟁 패배)·범위 밖·미존재 플레이어.
    /// 동시 픽업해도 lock(_groundItems) 안 Remove 가 1회만 성공 → 중복 지급 없음.
    /// </summary>
    public GroundItem? TryPickup(long userId, int groundId)
    {
        // 거리 검증용 플레이어 위치 먼저 스냅(락 비중첩 — _groundItems 와 중첩 잠금 회피).
        PlayerState? player;
        lock (_playerStates)
        {
            player = _playerStates.GetValueOrDefault(userId);
        }
        if (player is null)
            return null;

        lock (_groundItems)
        {
            if (!_groundItems.TryGetValue(groundId, out var item))
                return null; // 이미 주워짐(경쟁 패배) 또는 존재하지 않음

            float dx = item.PosX - player.PosX;
            float dz = item.PosZ - player.PosZ;
            if (dx * dx + dz * dz > PickupRange * PickupRange)
                return null; // 줍기 범위 밖

            _groundItems.Remove(groundId); // 경쟁 중재: 제거 성공 = 이 호출자가 승자
            return item;
        }
    }

    public void Broadcast(Packet packet, ulong? excludeSessionId = null)
    {
        try
        {
            lock (_playerSessions)
            {
                foreach (var (sessionId, session) in _playerSessions)
                {
                    if (excludeSessionId.HasValue && sessionId == excludeSessionId.Value)
                        continue;

                    _ = session.SendPacketAsync(packet);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to broadcast in room {RoomId}", RoomId);
        }
    }
}
