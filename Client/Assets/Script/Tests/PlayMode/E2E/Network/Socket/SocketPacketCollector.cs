using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using UnityEngine;

namespace Game.Tests.PlayMode.E2E
{
    /// <summary>
    /// TCP 소켓 E2E 테스트용 패킷 수집기.
    /// SocketConnector를 래핑해 수신 패킷을 버퍼링하고 특정 타입을 대기한다.
    /// </summary>
    internal sealed class SocketPacketCollector
    {
        private readonly List<Packet> _receivedPackets = new List<Packet>();
        private readonly object _sync = new object();
        private readonly SocketConnector _connector = new SocketConnector();

        private CancellationTokenSource _receiveCts;
        private UniTask _receiveLoop;

        public async UniTask ConnectAsync(string host, int port, CancellationToken ct)
        {
            _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            await _connector.ConnectAsync(host, port, _receiveCts.Token);
            _receiveLoop = _connector.StartReceiveLoopAsync(OnPacketAsync, _receiveCts.Token);
            _receiveLoop.Forget(ex =>
            {
                if (!IsExpectedDisconnectException(ex))
                    Debug.LogException(ex);
            });
        }

        public UniTask SendAsync(Packet packet, CancellationToken ct)
            => _connector.SendAsync(packet, ct);

        public async UniTask<TPacket> WaitForPacketAsync<TPacket>(
            Func<TPacket, bool> predicate, CancellationToken ct)
            where TPacket : Packet
        {
            await UniTask.WaitUntil(() => TryFindPacket(predicate, out TPacket _), cancellationToken: ct);
            TryFindPacket(predicate, out TPacket packet);
            return packet;
        }

        public async UniTask DisposeAsync()
        {
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
            await _connector.DisposeAsync();
        }

        private UniTask OnPacketAsync(Packet packet)
        {
            lock (_sync) { _receivedPackets.Add(packet); }
            return UniTask.CompletedTask;
        }

        private bool TryFindPacket<TPacket>(Func<TPacket, bool> predicate, out TPacket found)
            where TPacket : Packet
        {
            lock (_sync)
            {
                foreach (var packet in _receivedPackets)
                {
                    if (packet is TPacket typed && predicate(typed))
                    {
                        found = typed;
                        return true;
                    }
                }
            }
            found = null;
            return false;
        }

        private static bool IsExpectedDisconnectException(Exception exception)
            => exception is OperationCanceledException
            || exception is ObjectDisposedException
            || (exception is IOException io && io.InnerException is SocketException)
            || exception is SocketException;
    }
}
