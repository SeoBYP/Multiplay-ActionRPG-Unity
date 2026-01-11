using System.Collections.Concurrent;
using ServerCore.Protocol;

namespace Server.Room;

public class Room
{
    public int RoomId { get; private set; }
    public int MaxMembers { get; private set; }
    
    private readonly Dictionary<ulong, Session> _players = new();

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

    public Room(int roomId, int maxMembers = 4)
    {
        RoomId = roomId;
        MaxMembers = maxMembers;
    }

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

                // 입장 알림
                NotifyJoin(session.SessionId);
            
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
            if (!_players.Remove(sessionId))
            {
                Console.WriteLine($"[Room {RoomId}] Session {sessionId} not in room");
                return false;
            }

            Console.WriteLine($"[Room {RoomId}] Session {sessionId} left. Members: {MemberCount}/{MaxMembers}");

            // 퇴장 알림
            NotifyLeave(sessionId);
            
            return true;
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
    
    /// <summary>
    /// 입장 알림 (시스템 메시지)
    /// </summary>
    private void NotifyJoin(ulong sessionId)
    {
        var packet = new Packet();
        packet.SChat = new S_Chat
        {
            SenderId = 0,  // 0 = 시스템
            Message = $"[System] Player {sessionId} joined the room"
        };

        Broadcast(packet, excludeSessionId: sessionId);
    }

    /// <summary>
    /// 퇴장 알림
    /// </summary>
    private void NotifyLeave(ulong sessionId)
    {
        var packet = new Packet();
        packet.SChat = new S_Chat
        {
            SenderId = 0,
            Message = $"[System] Player {sessionId} left the room"
        };

        Broadcast(packet, excludeSessionId: sessionId);
    }
}