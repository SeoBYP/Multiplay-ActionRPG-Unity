using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace Game.Network.Socket
{
    public class SocketApiClient : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<ISocketPacketState, SocketPacketState>(Lifetime.Singleton);

            builder.Register<IPacketHandler, AuthPacketHandler>(Lifetime.Singleton);
            builder.Register<IPacketHandler, PlayerJoinedPacketHandler>(Lifetime.Singleton);
            builder.Register<IPacketHandler, MovePacketHandler>(Lifetime.Singleton);

            builder.Register<ISocketPacketDispatcher, SocketPacketDispatcher>(Lifetime.Singleton);
            builder.Register<ISocketConnector, SocketConnector>(Lifetime.Singleton);
            builder.Register<ISocketSession, SocketSession>(Lifetime.Singleton);
        }
    }

    public interface ISocketPacketState
    {
        bool IsAuthenticated { get; }
        string AuthMessage { get; }

        void SetAuthResult(bool success, string message);
        void UpsertPlayer(long userId, string nickname, float posX, float posY, float posZ, float rotY, long timeStamp = 0);
        void UpdatePlayerTransform(long userId, float posX, float posY, float posZ, float rotY, long timeStamp);
        bool TryGetPlayer(long userId, out SocketPlayerSnapshot snapshot);
    }

    public sealed class SocketPacketState : ISocketPacketState
    {
        private readonly object _sync = new object();
        private readonly Dictionary<long, SocketPlayerSnapshot> _players = new Dictionary<long, SocketPlayerSnapshot>();

        private bool _isAuthenticated;
        private string _authMessage = string.Empty;

        public bool IsAuthenticated
        {
            get
            {
                lock (_sync)
                {
                    return _isAuthenticated;
                }
            }
        }

        public string AuthMessage
        {
            get
            {
                lock (_sync)
                {
                    return _authMessage;
                }
            }
        }

        public void SetAuthResult(bool success, string message)
        {
            lock (_sync)
            {
                _isAuthenticated = success;
                _authMessage = message ?? string.Empty;
            }
        }

        public void UpsertPlayer(long userId, string nickname, float posX, float posY, float posZ, float rotY, long timeStamp = 0)
        {
            lock (_sync)
            {
                _players[userId] = new SocketPlayerSnapshot(userId, nickname ?? string.Empty, posX, posY, posZ, rotY, timeStamp);
            }
        }

        public void UpdatePlayerTransform(long userId, float posX, float posY, float posZ, float rotY, long timeStamp)
        {
            lock (_sync)
            {
                if (_players.TryGetValue(userId, out var existing))
                {
                    _players[userId] = existing.WithTransform(posX, posY, posZ, rotY, timeStamp);
                    return;
                }

                _players[userId] = new SocketPlayerSnapshot(userId, string.Empty, posX, posY, posZ, rotY, timeStamp);
            }
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
    }

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

        public SocketPlayerSnapshot WithTransform(float posX, float posY, float posZ, float rotY, long timeStamp)
        {
            return new SocketPlayerSnapshot(UserId, Nickname, posX, posY, posZ, rotY, timeStamp);
        }

        public SocketPlayerSnapshot Clone()
        {
            return new SocketPlayerSnapshot(UserId, Nickname, PosX, PosY, PosZ, RotY, TimeStamp);
        }
    }
}
