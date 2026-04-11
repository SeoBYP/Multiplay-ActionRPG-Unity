using MemoryPack;

namespace Shared.Packet.Packets;

[MemoryPackable]
public partial class S_GameStatus: Packet
{
    public long RoomId { get; set; }
    public EGameStatus GameStatus { get; set; }
}

public enum EGameStatus
{
    WaitingForPlayers,
    InProgress,
    Finished
}