using MemoryPack;

namespace Shared.Packet.Packets;

[MemoryPackable]
[MemoryPackUnion(1300, typeof(C_Auth))]
[MemoryPackUnion(1301, typeof(S_Auth))]
[MemoryPackUnion(1400,typeof(C_Ping))]
[MemoryPackUnion(1401,typeof(S_Pong))]
public abstract partial class Packet
{
    
}