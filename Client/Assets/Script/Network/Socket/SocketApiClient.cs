using System;
using System.Collections.Generic;
using System.Linq;
using VContainer;
using VContainer.Unity;

namespace Game.Network.Socket
{
    /// <summary>
    /// TCP 소켓 기반 멀티플레이어 클라이언트 구성요소를 DI에 등록한다.
    /// </summary>
    public class SocketApiClient : IInstaller
    {
        /// <summary>
        /// 패킷 상태 저장소, 핸들러, 디스패처, 커넥터, 세션을 한 번에 구성한다.
        /// </summary>
        public void Install(IContainerBuilder builder)
        {
            // 소켓에서 받은 플레이어 상태를 메모리에 유지한다.
            builder.Register<ISocketPacketState, SocketPacketState>(Lifetime.Singleton);

            // 패킷 타입별 후처리 로직 등록.
            builder.Register<IPacketHandler, PlayerJoinedPacketHandler>(Lifetime.Singleton);
            builder.Register<IPacketHandler, PlayerLeftPacketHandler>(Lifetime.Singleton);
            builder.Register<IPacketHandler, MovePacketHandler>(Lifetime.Singleton);
            // EF-2d: 서버 권위 Effect(버프/디버프) 수신.
            builder.Register<IPacketHandler, EffectApplyPacketHandler>(Lifetime.Singleton);
            builder.Register<IPacketHandler, EffectRemovePacketHandler>(Lifetime.Singleton);
            // M3 ⑥: 서버 권위 몬스터 스폰/상태/사망 수신.
            builder.Register<IPacketHandler, SpawnMonsterPacketHandler>(Lifetime.Singleton);
            builder.Register<IPacketHandler, MonsterStatePacketHandler>(Lifetime.Singleton);
            builder.Register<IPacketHandler, MonsterDeadPacketHandler>(Lifetime.Singleton);
            // 전원 입장(S_GameStatus InProgress) → 던전 준비 완료.
            builder.Register<IPacketHandler, GameStatusPacketHandler>(Lifetime.Singleton);
            // M4 ③: 몬스터 전멸 → 던전 클리어.
            builder.Register<IPacketHandler, DungeonClearPacketHandler>(Lifetime.Singleton);
            // M4 B: 참가자 전원 다운 → 던전 실패.
            builder.Register<IPacketHandler, DungeonFailedPacketHandler>(Lifetime.Singleton);
            // 3.3 루트/드랍: 바닥 아이템 스폰/제거 + 줍기 토스트.
            builder.Register<IPacketHandler, SpawnGroundItemPacketHandler>(Lifetime.Singleton);
            builder.Register<IPacketHandler, GroundItemRemovedPacketHandler>(Lifetime.Singleton);
            builder.Register<IPacketHandler, ItemPickedUpPacketHandler>(Lifetime.Singleton);

            // 송수신 파이프라인 등록.
            builder.Register<ISocketPacketDispatcher, SocketPacketDispatcher>(Lifetime.Singleton);
            builder.Register<ISocketConnector, SocketConnector>(Lifetime.Singleton);
            builder.Register<ISocketSession, SocketSession>(Lifetime.Singleton);
        }
    }

    /// <summary>
    /// 소켓 패킷 처리 결과를 외부 게임 로직이 조회할 수 있도록 보관하는 상태 저장소 인터페이스.
    /// </summary>
    public interface ISocketPacketState
    {
        /// <summary>현재 플레이 중인 맵 식별자. S_PlayerJoined 수신 시 세팅. 결정론 스폰 레이아웃 선택에 사용.</summary>
        string MapId { get; }

        void UpsertPlayer(long userId, string nickname, int spawnIndex, string mapId, float posX, float posY, float posZ, float rotY, long timeStamp = 0);
        void UpdatePlayerTransform(long userId, float posX, float posY, float posZ, float rotY, long timeStamp);
        bool TryGetPlayer(long userId, out SocketPlayerSnapshot snapshot);
        /// <summary>방에서 나간 플레이어를 상태에서 제거한다.</summary>
        void RemovePlayer(long userId);
        /// <summary>현재 보관 중인 모든 플레이어 스냅샷의 복사본을 반환한다. (원격 캐릭터 동기화용)</summary>
        IReadOnlyList<SocketPlayerSnapshot> GetAllPlayers();

        /// <summary>UpsertPlayer 호출 시 발행. CharacterSpawner가 원격 캐릭터를 스폰하는 데 사용한다.</summary>
        event Action<SocketPlayerSnapshot> OnPlayerJoined;
        /// <summary>RemovePlayer 호출 시 발행. CharacterSpawner가 원격 캐릭터를 디스폰하는 데 사용한다.</summary>
        event Action<long> OnPlayerLeft;
        /// <summary>기존 플레이어 transform 갱신 시 발행. RemoteDriver가 보간 대상으로 사용한다.</summary>
        event Action<SocketPlayerSnapshot> OnPlayerMoved;

        // ── EF-2d: 서버 권위 Effect 수신 (핸들러가 기록 → EffectReceiver가 ASC에 적용) ──
        /// <summary>S_ApplyEffect 수신 시 발행.</summary>
        event Action<SocketEffectApply> OnEffectApplied;
        /// <summary>S_RemoveEffect 수신 시 발행 (instanceId).</summary>
        event Action<int> OnEffectRemoved;
        void ApplyEffect(SocketEffectApply data);
        void RemoveEffect(int instanceId);

        // ── 전원 입장(서버 S_GameStatus InProgress) = 던전 준비 완료 ──
        /// <summary>전원 입장 시 발행. Presentation(InGameModel)이 인게임 UI 전환에 사용.</summary>
        event Action OnDungeonReady;
        void MarkDungeonReady();

        // ── 던전 클리어(서버 전멸 감지 S_DungeonClear) ──
        /// <summary>S_DungeonClear 수신 시 발행(인자=보상 Exp). Presentation(InGameModel)이 결과 화면→로비 복귀에 사용.</summary>
        event Action<long> OnDungeonCleared;
        void MarkDungeonCleared(long rewardExp);

        // ── 던전 실패(서버 전원 다운 감지 S_DungeonFailed) ──
        /// <summary>S_DungeonFailed 수신 시 발행. Presentation(InGameModel)이 실패 화면→로비 복귀에 사용.</summary>
        event Action OnDungeonFailed;
        void MarkDungeonFailed();

        // ── M3 ⑥: 서버 권위 몬스터(클라는 보간만) ──
        /// <summary>S_SpawnMonster 수신 시 발행. MonsterSpawner가 몬스터 엔티티를 스폰한다.</summary>
        event Action<SocketMonsterSnapshot> OnMonsterSpawned;
        /// <summary>S_MonsterState 수신(이동/HP/페이즈) 시 발행. MonsterEntity가 보간 대상으로 사용.</summary>
        event Action<SocketMonsterSnapshot> OnMonsterMoved;
        /// <summary>S_MonsterDead 수신 시 발행(instanceId). MonsterSpawner가 디스폰한다.</summary>
        event Action<int> OnMonsterDead;
        void AddMonster(SocketMonsterSnapshot snapshot);
        void UpdateMonster(int instanceId, float posX, float posY, float posZ, float rotY, int hp, byte phase);
        void RemoveMonster(int instanceId);
        bool TryGetMonster(int instanceId, out SocketMonsterSnapshot snapshot);
        /// <summary>현재 보관 중인 모든 몬스터 스냅샷의 복사본. (스포너 초기 로스터용)</summary>
        IReadOnlyList<SocketMonsterSnapshot> GetAllMonsters();

        // ── 루트/드랍: 바닥 아이템(서버 권위, 클라는 표시 + 줍기 의도만) ──
        /// <summary>S_SpawnGroundItem 수신 시 발행. GroundItemSpawner가 바닥 아이템을 스폰한다.</summary>
        event Action<SocketGroundItemSnapshot> OnGroundItemSpawned;
        /// <summary>S_GroundItemRemoved 수신 시 발행(groundId). GroundItemSpawner가 디스폰한다.</summary>
        event Action<int> OnGroundItemRemoved;
        /// <summary>S_ItemPickedUp 수신 시 발행(itemId, qty). 획득 토스트 표시에 사용.</summary>
        event Action<string, int> OnItemPickedUp;
        void AddGroundItem(SocketGroundItemSnapshot snapshot);
        void RemoveGroundItem(int groundId);
        void NotifyItemPickedUp(string itemId, int qty);
        /// <summary>현재 보관 중인 모든 바닥 아이템 스냅샷의 복사본. (스포너 초기 로스터용)</summary>
        IReadOnlyList<SocketGroundItemSnapshot> GetAllGroundItems();
    }

    /// <summary>
    /// 최근 인증 결과와 플레이어 스냅샷을 스레드 안전하게 저장하는 구현체.
    /// </summary>
    public sealed class SocketPacketState : ISocketPacketState
    {
        private readonly object _sync = new object();
        private readonly Dictionary<long, SocketPlayerSnapshot> _players = new Dictionary<long, SocketPlayerSnapshot>();
        private readonly Dictionary<int, SocketMonsterSnapshot> _monsters = new Dictionary<int, SocketMonsterSnapshot>();
        private readonly Dictionary<int, SocketGroundItemSnapshot> _groundItems = new Dictionary<int, SocketGroundItemSnapshot>();

        public string MapId { get; private set; } = string.Empty;

        public event Action<SocketPlayerSnapshot> OnPlayerJoined;
        public event Action<long>                 OnPlayerLeft;
        public event Action<SocketPlayerSnapshot> OnPlayerMoved;
        public event Action<SocketEffectApply>    OnEffectApplied;
        public event Action<int>                  OnEffectRemoved;
        public event Action                       OnDungeonReady;
        public event Action<long>                 OnDungeonCleared;
        public event Action                       OnDungeonFailed;
        public event Action<SocketMonsterSnapshot> OnMonsterSpawned;
        public event Action<SocketMonsterSnapshot> OnMonsterMoved;
        public event Action<int>                   OnMonsterDead;
        public event Action<SocketGroundItemSnapshot> OnGroundItemSpawned;
        public event Action<int>                      OnGroundItemRemoved;
        public event Action<string, int>             OnItemPickedUp;

        public void MarkDungeonReady() => OnDungeonReady?.Invoke();
        public void MarkDungeonCleared(long rewardExp) => OnDungeonCleared?.Invoke(rewardExp);
        public void MarkDungeonFailed() => OnDungeonFailed?.Invoke();

        public void ApplyEffect(SocketEffectApply data)
        {
            if (data != null) OnEffectApplied?.Invoke(data);
        }

        public void RemoveEffect(int instanceId)
        {
            OnEffectRemoved?.Invoke(instanceId);
        }

        public void UpsertPlayer(long userId, string nickname, int spawnIndex, string mapId, float posX, float posY, float posZ, float rotY, long timeStamp = 0)
        {
            SocketPlayerSnapshot snapshot;
            lock (_sync)
            {
                if (!string.IsNullOrEmpty(mapId)) MapId = mapId;
                snapshot = new SocketPlayerSnapshot(userId, nickname ?? string.Empty, spawnIndex, posX, posY, posZ, rotY, timeStamp);
                _players[userId] = snapshot;
            }
            OnPlayerJoined?.Invoke(snapshot);
        }

        public void UpdatePlayerTransform(long userId, float posX, float posY, float posZ, float rotY, long timeStamp)
        {
            SocketPlayerSnapshot updated = null;
            lock (_sync)
            {
                if (_players.TryGetValue(userId, out var existing))
                {
                    updated = existing.WithTransform(posX, posY, posZ, rotY, timeStamp);
                    _players[userId] = updated;
                }
                else
                {
                    // S_Move가 S_PlayerJoined보다 먼저 도달한 경우 — 최소 스냅샷으로 보관만 한다.
                    // SpawnIndex는 아직 모름(-1). S_PlayerJoined 수신 시 Upsert로 교정된다.
                    // OnPlayerMoved는 발행하지 않는다 (아직 RemoteDriver가 없음).
                    _players[userId] = new SocketPlayerSnapshot(userId, string.Empty, -1, posX, posY, posZ, rotY, timeStamp);
                }
            }
            if (updated != null) OnPlayerMoved?.Invoke(updated);
        }

        public bool TryGetPlayer(long userId, out SocketPlayerSnapshot snapshot)
        {
            lock (_sync)
            {
                if (_players.TryGetValue(userId, out var existing))
                {
                    snapshot = existing.Clone();
                    return true;
                }
                snapshot = null;
                return false;
            }
        }

        public void RemovePlayer(long userId)
        {
            bool removed;
            lock (_sync) { removed = _players.Remove(userId); }
            if (removed) OnPlayerLeft?.Invoke(userId);
        }

        public IReadOnlyList<SocketPlayerSnapshot> GetAllPlayers()
        {
            lock (_sync)
            {
                return _players.Values.Select(p => p.Clone()).ToList();
            }
        }

        // ── M3 ⑥: 몬스터(서버 권위, 클라는 보간만) ──

        public void AddMonster(SocketMonsterSnapshot snapshot)
        {
            if (snapshot == null) return;
            lock (_sync) { _monsters[snapshot.InstanceId] = snapshot; }
            OnMonsterSpawned?.Invoke(snapshot);
        }

        public void UpdateMonster(int instanceId, float posX, float posY, float posZ, float rotY, int hp, byte phase)
        {
            SocketMonsterSnapshot updated = null;
            lock (_sync)
            {
                if (_monsters.TryGetValue(instanceId, out var existing))
                {
                    updated = existing.WithState(posX, posY, posZ, rotY, hp, phase);
                    _monsters[instanceId] = updated;
                }
                else
                {
                    // 상태가 스폰보다 먼저 도달 — 최소 스냅샷 보관(이름/MaxHp 미상).
                    // OnMonsterMoved는 발행하지 않는다(아직 MonsterEntity 없음). S_SpawnMonster 수신 시 교정.
                    _monsters[instanceId] = new SocketMonsterSnapshot(instanceId, string.Empty, posX, posY, posZ, rotY, hp, hp, phase);
                }
            }
            if (updated != null) OnMonsterMoved?.Invoke(updated);
        }

        public void RemoveMonster(int instanceId)
        {
            bool removed;
            lock (_sync) { removed = _monsters.Remove(instanceId); }
            if (removed) OnMonsterDead?.Invoke(instanceId);
        }

        public bool TryGetMonster(int instanceId, out SocketMonsterSnapshot snapshot)
        {
            lock (_sync)
            {
                if (_monsters.TryGetValue(instanceId, out var existing))
                {
                    snapshot = existing.Clone();
                    return true;
                }
                snapshot = null;
                return false;
            }
        }

        public IReadOnlyList<SocketMonsterSnapshot> GetAllMonsters()
        {
            lock (_sync)
            {
                return _monsters.Values.Select(m => m.Clone()).ToList();
            }
        }

        // ── 루트/드랍: 바닥 아이템(서버 권위, 클라는 표시 + 줍기 의도만) ──

        public void AddGroundItem(SocketGroundItemSnapshot snapshot)
        {
            if (snapshot == null) return;
            lock (_sync) { _groundItems[snapshot.GroundId] = snapshot; }
            OnGroundItemSpawned?.Invoke(snapshot);
        }

        public void RemoveGroundItem(int groundId)
        {
            bool removed;
            lock (_sync) { removed = _groundItems.Remove(groundId); }
            if (removed) OnGroundItemRemoved?.Invoke(groundId);
        }

        public void NotifyItemPickedUp(string itemId, int qty)
            => OnItemPickedUp?.Invoke(itemId ?? string.Empty, qty);

        public IReadOnlyList<SocketGroundItemSnapshot> GetAllGroundItems()
        {
            lock (_sync)
            {
                return _groundItems.Values.Select(g => g.Clone()).ToList();
            }
        }
    }

    /// <summary>
    /// 바닥 아이템 1개의 불변 스냅샷(서버 권위). 클라는 위치 표시 + 줍기 의도 송신만 한다.
    /// </summary>
    public sealed class SocketGroundItemSnapshot
    {
        public int GroundId { get; }
        public string ItemId { get; }
        public int Qty { get; }
        public float PosX { get; }
        public float PosY { get; }
        public float PosZ { get; }

        public SocketGroundItemSnapshot(int groundId, string itemId, int qty, float posX, float posY, float posZ)
        {
            GroundId = groundId;
            ItemId = itemId ?? string.Empty;
            Qty = qty;
            PosX = posX;
            PosY = posY;
            PosZ = posZ;
        }

        public SocketGroundItemSnapshot Clone()
            => new SocketGroundItemSnapshot(GroundId, ItemId, Qty, PosX, PosY, PosZ);
    }

    /// <summary>
    /// 한 몬스터의 최근 상태(위치/회전/HP/페이즈)를 담는 불변 스냅샷. 서버 권위 — 클라는 보간/표시만.
    /// </summary>
    public sealed class SocketMonsterSnapshot
    {
        public int InstanceId { get; }
        public string MonsterId { get; }
        public float PosX { get; }
        public float PosY { get; }
        public float PosZ { get; }
        public float RotY { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public byte Phase { get; }

        public SocketMonsterSnapshot(int instanceId, string monsterId, float posX, float posY, float posZ, float rotY, int hp, int maxHp, byte phase)
        {
            InstanceId = instanceId;
            MonsterId = monsterId ?? string.Empty;
            PosX = posX;
            PosY = posY;
            PosZ = posZ;
            RotY = rotY;
            Hp = hp;
            MaxHp = maxHp;
            Phase = phase;
        }

        /// <summary>식별 정보(MonsterId/MaxHp)는 유지하고 상태(위치/회전/HP/페이즈)만 갱신.</summary>
        public SocketMonsterSnapshot WithState(float posX, float posY, float posZ, float rotY, int hp, byte phase)
            => new SocketMonsterSnapshot(InstanceId, MonsterId, posX, posY, posZ, rotY, hp, MaxHp, phase);

        public SocketMonsterSnapshot Clone()
            => new SocketMonsterSnapshot(InstanceId, MonsterId, PosX, PosY, PosZ, RotY, Hp, MaxHp, Phase);
    }

    /// <summary>
    /// 한 플레이어의 최근 위치/회전 상태를 담는 불변 스냅샷.
    /// </summary>
    public sealed class SocketPlayerSnapshot
    {
        public long UserId { get; }
        public string Nickname { get; }
        /// <summary>스폰 슬롯 인덱스. 결정론 스폰 입력(자기 캐릭터 스폰 위치 계산). 미상이면 -1.</summary>
        public int SpawnIndex { get; }
        public float PosX { get; }
        public float PosY { get; }
        public float PosZ { get; }
        public float RotY { get; }
        public long TimeStamp { get; }

        public SocketPlayerSnapshot(long userId, string nickname, int spawnIndex, float posX, float posY, float posZ, float rotY, long timeStamp)
        {
            UserId = userId;
            Nickname = nickname ?? string.Empty;
            SpawnIndex = spawnIndex;
            PosX = posX;
            PosY = posY;
            PosZ = posZ;
            RotY = rotY;
            TimeStamp = timeStamp;
        }

        /// <summary>
        /// 플레이어 식별 정보(SpawnIndex 포함)는 유지하고 transform 정보만 갱신한 새 스냅샷을 만든다.
        /// </summary>
        public SocketPlayerSnapshot WithTransform(float posX, float posY, float posZ, float rotY, long timeStamp)
        {
            return new SocketPlayerSnapshot(UserId, Nickname, SpawnIndex, posX, posY, posZ, rotY, timeStamp);
        }

        /// <summary>
        /// 외부 노출용 복사본을 만든다.
        /// </summary>
        public SocketPlayerSnapshot Clone()
        {
            return new SocketPlayerSnapshot(UserId, Nickname, SpawnIndex, PosX, PosY, PosZ, RotY, TimeStamp);
        }
    }

    /// <summary>
    /// 서버에서 수신한 Effect 부여 정보(네트워크 레이어 DTO). EffectId는 공유 카탈로그 키.
    /// EffectReceiver(상위 레이어)가 카탈로그 조회 + 타겟 ASC 적용에 사용한다.
    /// </summary>
    public sealed class SocketEffectApply
    {
        public string EffectId { get; }
        public int InstanceId { get; }
        public long TargetId { get; }
        public long SourceId { get; }
        public long StartTick { get; }
        public int Stacks { get; }

        public SocketEffectApply(string effectId, int instanceId, long targetId, long sourceId, long startTick, int stacks)
        {
            EffectId = effectId ?? string.Empty;
            InstanceId = instanceId;
            TargetId = targetId;
            SourceId = sourceId;
            StartTick = startTick;
            Stacks = stacks;
        }
    }
}
