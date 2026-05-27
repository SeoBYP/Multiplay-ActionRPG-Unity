using System.Net;
using System.Net.Sockets;
using MemoryPack;
using Shared.Packet;
using Shared.Packet.Packets;

Console.WriteLine("Dummy Client Start");

// MemoryPack 테스트
var testPacket = new C_Ping
{
    IsHealthy = true
};

var bin = MemoryPackSerializer.Serialize(testPacket);
Console.WriteLine($"Serialized Length: {bin.Length}");

var val = MemoryPackSerializer.Deserialize<C_Ping>(bin);
Console.WriteLine($"Deserialized IsHealthy: {val?.IsHealthy}");

string ipAddress = "127.0.0.1";
int port = 7777;

// Ping 설정
TimeSpan pingInterval = TimeSpan.FromSeconds(3);
TimeSpan pongTimeout = TimeSpan.FromSeconds(10);

await RunAsync();

async Task RunAsync()
{
    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    var endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
    using var cts = new CancellationTokenSource();

    DateTime lastPongAtUtc = DateTime.UtcNow;

    try
    {
        await socket.ConnectAsync(endpoint);
        Console.WriteLine($"Connected: {endpoint}");

        var receiveTask = ReceiveLoopAsync(
            socket,
            () => lastPongAtUtc = DateTime.UtcNow,
            cts.Token);

        var pingTask = PingLoopAsync(
            socket,
            pingInterval,
            pongTimeout,
            () => lastPongAtUtc,
            cts);

        Console.WriteLine("Type 'auth {userId}', 'join {roomId}', 'move {x} {y} {z}', 'leave', 'ping' to send packets, '/quit' to exit.");

        while (!cts.IsCancellationRequested && socket.Connected)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (input == null || input.Equals("/quit", StringComparison.OrdinalIgnoreCase))
                break;

            if (string.IsNullOrWhiteSpace(input))
                continue;

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0].ToLower();

            try
            {
                if (cmd == "ping")
                {
                    await SendPacketAsync(socket, new C_Ping { IsHealthy = true });
                }
                else if (cmd == "join" && parts.Length > 2)
                {
                    await SendPacketAsync(socket, new C_PlayerJoin { RoomId = long.Parse(parts[1]), UserId = long.Parse(parts[2]) });
                }
                else if (cmd == "move" && parts.Length > 3)
                {
                    await SendPacketAsync(socket, new C_Move
                    {
                        PosX = float.Parse(parts[1]),
                        PosY = float.Parse(parts[2]),
                        PosZ = float.Parse(parts[3]),
                        RotY = 0,
                        TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });
                }
                else if (cmd == "leave")
                {
                    await SendPacketAsync(socket, new C_PlayerLeave());
                }
                else
                {
                    Console.WriteLine("Unknown command or missing arguments.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Command Error: {ex.Message}");
            }
        }

        cts.Cancel();

        if (socket.Connected)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // 이미 닫힌 경우 무시
            }
        }

        await Task.WhenAll(
            IgnoreCancellationAsync(receiveTask),
            IgnoreCancellationAsync(pingTask));
    }
    catch (Exception e)
    {
        Console.WriteLine($"Error: {e.Message}");
    }
    finally
    {
        if (socket.Connected)
        {
            socket.Close();
        }

        Console.WriteLine("Disconnected");
    }
}

async Task PingLoopAsync(
    Socket socket,
    TimeSpan interval,
    TimeSpan timeout,
    Func<DateTime> getLastPongUtc,
    CancellationTokenSource cts)
{
    try
    {
        while (!cts.IsCancellationRequested && socket.Connected)
        {
            await Task.Delay(interval, cts.Token);

            if (cts.IsCancellationRequested || !socket.Connected)
                break;

            var elapsed = DateTime.UtcNow - getLastPongUtc();

            if (elapsed > timeout)
            {
                Console.WriteLine($"[PingLoop] Pong timeout. Last pong was {elapsed.TotalSeconds:F1}s ago.");
                cts.Cancel();

                try
                {
                    socket.Close();
                }
                catch
                {
                    // 무시
                }

                break;
            }

            await SendPacketAsync(socket, new C_Ping
            {
                IsHealthy = true
            });

            Console.WriteLine("[PingLoop] Sent C_Ping");
        }
    }
    catch (OperationCanceledException)
    {
        // 정상 종료
    }
    catch (Exception e)
    {
        Console.WriteLine($"Ping loop error: {e.Message}");
        cts.Cancel();
    }
}

async Task ReceiveLoopAsync(
    Socket socket,
    Action onPongReceived,
    CancellationToken ct)
{
    try
    {
        while (!ct.IsCancellationRequested && socket.Connected)
        {
            byte[] lengthBytes = await ReceiveExactAsync(socket, 4, ct);
            int length = BitConverter.ToInt32(lengthBytes, 0);

            if (length <= 0 || length > 65536)
            {
                Console.WriteLine($"Invalid packet length: {length}");
                break;
            }

            byte[] packetData = await ReceiveExactAsync(socket, length, ct);
            var packet = PacketSerializer.Deserialize(packetData);

            if (packet == null)
            {
                Console.WriteLine("Failed to deserialize packet.");
                continue;
            }

            await HandlePacketAsync(socket, packet, onPongReceived, ct);
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Receive loop canceled.");
    }
    catch (SocketException)
    {
        Console.WriteLine("Socket closed.");
    }
    catch (Exception e)
    {
        Console.WriteLine($"Receive error: {e.Message}");
    }
}

async Task<byte[]> ReceiveExactAsync(Socket socket, int count, CancellationToken ct)
{
    byte[] buffer = new byte[count];
    int offset = 0;

    while (offset < count)
    {
        ct.ThrowIfCancellationRequested();

        int received = await socket.ReceiveAsync(
            new ArraySegment<byte>(buffer, offset, count - offset),
            SocketFlags.None,
            ct);

        if (received == 0)
            throw new SocketException((int)SocketError.ConnectionReset);

        offset += received;
    }

    return buffer;
}

async Task HandlePacketAsync(
    Socket socket,
    Packet packet,
    Action onPongReceived,
    CancellationToken ct)
{
    switch (packet)
    {
        case S_Pong pong:
            onPongReceived();
            Console.WriteLine($"[S_Pong] IsHealthy={pong.IsHealthy}");
            break;

        case S_PlayerJoined joined:
            Console.WriteLine($"[S_PlayerJoined] Success={joined.Success} UserId={joined.UserId} ({joined.PosX},{joined.PosY},{joined.PosZ}) Message={joined.Message}");
            break;

        case S_PlayerLeft left:
            Console.WriteLine($"[S_PlayerLeft] UserId={left.UserId}");
            break;

        case S_GameStatus status:
            Console.WriteLine($"[S_GameStatus] RoomId={status.RoomId} Status={status.GameStatus}");
            break;

        case S_Move move:
            Console.WriteLine($"[S_Move] UserId={move.UserId} ({move.PosX},{move.PosY},{move.PosZ}) RotY={move.RotY}");
            break;

        default:
            Console.WriteLine($"Unknown packet: {packet.GetType().Name}");
            break;
    }
}

async Task SendPacketAsync(Socket socket, Packet packet, CancellationToken ct = default)
{
    try
    {
        byte[] buffer = PacketSerializer.Serialize(packet);

        int length = buffer.Length;
        byte[] lengthBytes = BitConverter.GetBytes(length);

        byte[] finalPacket = new byte[4 + length];
        Array.Copy(lengthBytes, 0, finalPacket, 0, 4);
        Array.Copy(buffer, 0, finalPacket, 4, length);

        int offset = 0;
        while (offset < finalPacket.Length)
        {
            ct.ThrowIfCancellationRequested();

            int sent = await socket.SendAsync(
                new ArraySegment<byte>(finalPacket, offset, finalPacket.Length - offset),
                SocketFlags.None,
                ct);

            if (sent == 0)
                throw new SocketException((int)SocketError.ConnectionReset);

            offset += sent;
        }

        Console.WriteLine($"Sent: {packet.GetType().Name}");
    }
    catch (OperationCanceledException)
    {
        // 정상 취소
    }
    catch (Exception e)
    {
        Console.WriteLine($"Send error: {e.Message}");
        throw;
    }
}

static async Task IgnoreCancellationAsync(Task task)
{
    try
    {
        await task;
    }
    catch (OperationCanceledException)
    {
        // 무시
    }
}