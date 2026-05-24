# 패킷 시스템

## 직렬화: MemoryPack + Union

모든 패킷은 `Shared.Packet/Packets/Packet.cs`의 추상 `Packet` 클래스 상속.  
**새 패킷 추가 시 `Packet.cs` Union 등록 필수** — 누락이 런타임 역직렬화 오류의 1순위 원인.

## 패킷 추가 3단계 (모두 필수)

**1. 패킷 클래스** — `Shared.Packet/Packets/Domains/`
```csharp
[MemoryPackable]
public partial class C_Attack : Packet { }  // C_ = 클라→서버

[MemoryPackable]
public partial class S_Attack : Packet { }  // S_ = 서버→클라
```

**2. Union 등록** — `Packet.cs`
```csharp
[MemoryPackUnion(1600, typeof(C_Attack))]
[MemoryPackUnion(1601, typeof(S_Attack))]
```

**3. 핸들러** — `SocketServer/PacketHandler/Handler/`
```csharp
public static class CombatHandler
{
    [PacketHandler(typeof(C_Attack))]
    public static async ValueTask HandleAttack(Session session, C_Attack packet, CancellationToken ct)
    { }
}
```
`[PacketHandler]` 어트리뷰트 → `PacketHandlerRegistry` 자동 등록.

## Union ID 범위

```
1300~1399: 인증
1310~1319: 입장/퇴장
1400~1499: 유틸 (Ping/Pong)
1500~1599: 이동
1600~1699: 전투
1700~1799: 게임 라이프사이클
1800~1899: 던전 이벤트  ← 다음 추가 영역
```

## 현재 구현된 패킷

| 패킷 | Union ID | 파일 |
|------|----------|------|
| C_Auth / S_Auth | 1300 / 1301 | AuthPackets.cs |
| C_PlayerJoin / S_PlayerJoined | 1310 / 1311 | RoomPackets.cs |
| C_PlayerLeave / S_PlayerLeft | 1312 / 1313 | RoomPackets.cs |
| C_Ping / S_Pong | 1400 / 1401 | PingPongPackets.cs |
| C_Move / S_Move | 1500 / 1501 | MovementPackets.cs |
| C_Attack / S_Attack | 1600 / 1601 | AttackPacket.cs |
| S_GameStatus | 1701 | GameStatusPacket.cs |
