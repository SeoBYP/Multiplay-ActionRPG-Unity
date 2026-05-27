using MemoryPack;

namespace Shared.Packet.Packets;

[MemoryPackable]
// 입장/퇴장
[MemoryPackUnion(1310, typeof(C_PlayerJoin))]
[MemoryPackUnion(1311, typeof(S_PlayerJoined))]
[MemoryPackUnion(1312, typeof(C_PlayerLeave))]
[MemoryPackUnion(1313, typeof(S_PlayerLeft))]
[MemoryPackUnion(1400,typeof(C_Ping))]
[MemoryPackUnion(1401,typeof(S_Pong))]
// 이동
[MemoryPackUnion(1500,typeof(C_Move))]
[MemoryPackUnion(1501,typeof(S_Move))]
// 전투
[MemoryPackUnion(1600,typeof(C_Attack))]
[MemoryPackUnion(1601,typeof(S_Attack))]
// 게임 라이프사이클
[MemoryPackUnion(1701,typeof(S_GameStatus))]
public abstract partial class Packet
{
    
}