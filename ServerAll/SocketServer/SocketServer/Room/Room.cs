
namespace Server.Room;

using Shared.Packet.Packets;

public class Room
{
    public long RoomId { get; private set; }
    public int MaxMembers { get; private set; }
    
    private readonly Dictionary<ulong, Session> _players = new();
    private readonly HashSet<long> _expectedUserIds = new();
    public int MemberCount
    {
        get
        {
            lock (_players)
            {
                return _players.Count;
            }
        }
    }
    
    public bool IsFull => MemberCount >= MaxMembers;

    public Room(long roomId, List<long> expectedUserIds)
    {
        RoomId = roomId;
        MaxMembers = expectedUserIds.Count;
        _expectedUserIds = new HashSet<long>(expectedUserIds);
    }
    
    public bool IsExpectedPlayer(long userId) => _expectedUserIds.Contains(userId);

    /// <summary>
    /// 플레이어 입장
    /// </summary>
    public bool Join(Session session)
    {
        try
        {
            lock (_players)
            {
                if (IsFull)
                {
                    Console.WriteLine($"[Room {RoomId}] Full! Cannot join session {session.SessionId}");
                    return false;
                }

                if (_players.ContainsKey(session.SessionId))
                {
                    Console.WriteLine($"[Room {RoomId}] Session {session.SessionId} already in room");
                    return false;
                }

                _players.Add(session.SessionId, session);
                Console.WriteLine($"[Room {RoomId}] Session {session.SessionId} joined. Members: {MemberCount}/{MaxMembers}");
                
                return true;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    /// 플레이어 퇴장
    /// </summary>
    public bool Leave(ulong sessionId)
    {
        try
        {
            lock (_players)  // ← lock 추가!
            {
                if (!_players.Remove(sessionId))
                {
                    Console.WriteLine($"[Room {RoomId}] Session {sessionId} not in room");
                    return false;
                }

                Console.WriteLine($"[Room {RoomId}] Session {sessionId} left. Members: {MemberCount}/{MaxMembers}");

                return true;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public void Broadcast(Packet packet, ulong? excludeSessionId = null)
    {
        try
        {
            lock (_players)
            {
                int sentCount = 0;
            
                foreach (var (sessionId, session) in _players)
                {
                    if (excludeSessionId.HasValue && sessionId == excludeSessionId.Value)
                        continue;

                    _ = session.SendPacketAsync(packet);
                    sentCount++;
                }

                Console.WriteLine($"[Room {RoomId}] Broadcast to {sentCount} members");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}