using System.Collections.Generic;
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
            builder.Register<IPacketHandler, MovePacketHandler>(Lifetime.Singleton);

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
        void UpsertPlayer(long userId, string nickname, float posX, float posY, float posZ, float rotY, long timeStamp = 0);
        void UpdatePlayerTransform(long userId, float posX, float posY, float posZ, float rotY, long timeStamp);
        bool TryGetPlayer(long userId, out SocketPlayerSnapshot snapshot);
    }

    /// <summary>
    /// 최근 인증 결과와 플레이어 스냅샷을 스레드 안전하게 저장하는 구현체.
    /// </summary>
    public sealed class SocketPacketState : ISocketPacketState
    {
        private readonly object _sync = new object();
        private readonly Dictionary<long, SocketPlayerSnapshot> _players = new Dictionary<long, SocketPlayerSnapshot>();

        /// <summary>
        /// 새로 합류한 플레이어를 추가하거나 기존 스냅샷을 갱신한다.
        /// </summary>
        public void UpsertPlayer(long userId, string nickname, float posX, float posY, float posZ, float rotY, long timeStamp = 0)
        {
            lock (_sync)
            {
                _players[userId] = new SocketPlayerSnapshot(userId, nickname ?? string.Empty, posX, posY, posZ, rotY, timeStamp);
            }
        }

        /// <summary>
        /// 기존 플레이어의 좌표만 갱신한다.
        /// 미등록 플레이어가 오면 닉네임 없는 임시 스냅샷으로 생성한다.
        /// </summary>
        public void UpdatePlayerTransform(long userId, float posX, float posY, float posZ, float rotY, long timeStamp)
        {
            lock (_sync)
            {
                // 기존 스냅샷이 있으면 위치 정보만 덮어쓴다.
                if (_players.TryGetValue(userId, out var existing))
                {
                    _players[userId] = existing.WithTransform(posX, posY, posZ, rotY, timeStamp);
                    return;
                }

                // 아직 조인 패킷이 오지 않은 플레이어는 최소 정보로 생성한다.
                _players[userId] = new SocketPlayerSnapshot(userId, string.Empty, posX, posY, posZ, rotY, timeStamp);
            }
        }

        /// <summary>
        /// 플레이어 스냅샷을 안전한 복사본으로 반환한다.
        /// </summary>
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
    }

    /// <summary>
    /// 한 플레이어의 최근 위치/회전 상태를 담는 불변 스냅샷.
    /// </summary>
    public sealed class SocketPlayerSnapshot
    {
        public long UserId { get; }
        public string Nickname { get; }
        public float PosX { get; }
        public float PosY { get; }
        public float PosZ { get; }
        public float RotY { get; }
        public long TimeStamp { get; }

        public SocketPlayerSnapshot(long userId, string nickname, float posX, float posY, float posZ, float rotY, long timeStamp)
        {
            UserId = userId;
            Nickname = nickname ?? string.Empty;
            PosX = posX;
            PosY = posY;
            PosZ = posZ;
            RotY = rotY;
            TimeStamp = timeStamp;
        }

        /// <summary>
        /// 플레이어 식별 정보는 유지하고 transform 정보만 갱신한 새 스냅샷을 만든다.
        /// </summary>
        public SocketPlayerSnapshot WithTransform(float posX, float posY, float posZ, float rotY, long timeStamp)
        {
            return new SocketPlayerSnapshot(UserId, Nickname, posX, posY, posZ, rotY, timeStamp);
        }

        /// <summary>
        /// 외부 노출용 복사본을 만든다.
        /// </summary>
        public SocketPlayerSnapshot Clone()
        {
            return new SocketPlayerSnapshot(UserId, Nickname, PosX, PosY, PosZ, RotY, TimeStamp);
        }
    }
}
