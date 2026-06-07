using Script.System.GamePlayAbilitySystem;
using Server.Monster;
using Server.Player;
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

    // 클리어 감지(몬스터 전멸) — lock(_monsters) 안에서만 접근.
    private bool _monstersSpawned;   // 한 번이라도 몬스터가 스폰됐는지(빈 방을 클리어로 오판 방지)

    // 던전 결과(클리어/실패) — 단일 terminal 상태. 0=None,1=Cleared,2=Failed.
    // Interlocked.CompareExchange 로 최초 1회만 claim → 클리어/실패 상호 배타(둘 다 발화 불가).
    private int _outcome;
    // 실패 집계(다운된 참가자) — lock(_downed) 안에서만 접근.
    private readonly HashSet<long> _downed = new();

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

    public bool Leave(ulong sessionId)
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
                    "Session {SessionId} left room {RoomId}. Members: {MemberCount}/{MaxMembers}",
                    sessionId,
                    RoomId,
                    MemberCount,
                    MaxMembers);
            }

            // 퇴장 시 PlayerState도 정리 — 안 하면 떠난 플레이어가 위치/AI 타깃 계산에 유령으로 잔류한다.
            if (userId > 0)
            {
                lock (_playerStates)
                {
                    _playerStates.Remove(userId);
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

    public void InitPlayerState(long userId, string nickname, int spawnIndex, float spawnX, float spawnY, float spawnZ, float rotY)
    {
        lock (_playerStates)
        {
            var playerState = new PlayerState
            {
                UserId = userId,
                Nickname = nickname,
                SpawnIndex = spawnIndex,
                PosX = spawnX,
                PosY = spawnY,
                PosZ = spawnZ,
                RotY = rotY,
                LastMovedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
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

    // ── 몬스터 ───────────────────────────────────────────

    /// <summary>맵 레이아웃의 몬스터 정의로 초기 스폰(wave 0). InstanceId 는 방 단위 순차 발급.</summary>
    public void SpawnMonsters(IReadOnlyList<MonsterSpawnDef> defs, MapBounds bounds)
    {
        lock (_monsters)
        {
            _bounds = bounds ?? MapBounds.Unbounded;
            foreach (var def in defs ?? Array.Empty<MonsterSpawnDef>())
            {
                var stats = MonsterCatalog.Get(def.MonsterId);
                int count = Math.Max(1, def.Count);
                for (int i = 0; i < count; i++)
                {
                    int id = ++_nextMonsterInstanceId;
                    _monsters[id] = new MonsterState
                    {
                        InstanceId = id,
                        MonsterId  = def.MonsterId,
                        PosX = def.X, PosY = def.Y, PosZ = def.Z,
                        SpawnX = def.X, SpawnZ = def.Z,
                        RotY = def.RotY,
                        MaxHp = stats.MaxHp,
                        Hp = stats.MaxHp,
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
    /// 한 플레이어의 다운(HP 0, 클라 C_PlayerDead 보고)을 집계하고, <b>참가자 전원</b>이 다운되면
    /// 실패를 <b>최초 1회만</b> true 로 표시한다. 기대 로스터에 없는 userId 는 무시.
    /// 클리어가 이미 발화됐으면(_outcome!=None) false — 상호 배타.
    /// </summary>
    public bool TryMarkFailed(long userId)
    {
        lock (_downed)
        {
            if (!_expectedUserIds.Contains(userId))
                return false;

            _downed.Add(userId);
            if (_downed.Count < _expectedUserIds.Count)
                return false;
        }

        return System.Threading.Interlocked.CompareExchange(ref _outcome, 2, 0) == 0;
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
    /// 한 틱 몬스터 시뮬레이션. 플레이어 스냅샷을 먼저 떠(락 비중첩) 각 몬스터를 MonsterAiMath 로 진행하고,
    /// 브로드캐스트할 패킷 목록(이동 S_MonsterState + 공격 S_ApplyEffect)을 반환한다(전송 I/O 는 호출자가 락 밖에서).
    ///
    /// 공격(⑤b): 몬스터가 Attack 페이즈 + 쿨다운 경과 시 최근접 플레이어(MonsterAiMath.Step 반환 인덱스)에
    /// monster_attack_dmg 효과를 브로드캐스트. 플레이어 HP 는 클라가 공유 카탈로그로 결정론 계산(서버 권위 X).
    /// </summary>
    public List<Packet> TickMonsters(float dt, long nowMs)
    {
        List<PlayerState> players;
        lock (_playerStates)
        {
            players = _playerStates.Values.ToList();
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

                outPackets.Add(new S_MonsterState
                {
                    InstanceId = m.InstanceId,
                    PosX = m.PosX, PosY = m.PosY, PosZ = m.PosZ,
                    RotY = m.RotY,
                    Hp = m.Hp,
                    Phase = (byte)m.Phase,
                });

                // ⑤b: Attack 페이즈 + 쿨다운 경과 → 최근접 플레이어에 데미지 효과(클라가 ASC에 적용).
                if (m.Phase == MonsterPhase.Attack
                    && targetIdx >= 0
                    && nowMs - m.LastAttackAt >= stats.AttackCooldownMs)
                {
                    m.LastAttackAt = nowMs;
                    outPackets.Add(new S_ApplyEffect
                    {
                        InstanceId = NextEffectInstanceId(),
                        EffectId = "monster_attack_dmg",
                        TargetId = players[targetIdx].UserId,
                        SourceId = 0, // 0 = 몬스터/환경
                        StartTick = nowMs,
                        Stacks = 1,
                    });
                }
            }
        }
        return outPackets;
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
