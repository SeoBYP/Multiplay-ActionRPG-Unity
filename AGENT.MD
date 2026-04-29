# AGENT.MD

## Purpose

This file keeps the current project context so another Codex chat can continue the same Unity combat/state-system work without starting from scratch.

## Project

- Repository: `C:\Users\user\Github\Multiplay-ActionRPG-Unity`
- Current focus: Unity Client combat/state work, plus the ServerAll multiplayer backend.
- Main client path: `Client`
- Main server path: `ServerAll`
- Current validation command:

```powershell
dotnet build Client\Game.Main.csproj --no-restore
dotnet build ServerAll\ServerAll.sln --no-restore
```

The client build currently succeeds with existing warnings from Unity/package references. The server solution should be checked separately when server files change.

## Current Design Direction

The project is moving toward a data-driven StateMachine and GAS-like damage pipeline.

Key decisions:

- `AttackState` is valid and should exist because attack is a character action mode.
- `AttackState` should handle state entry, animation trigger, duration, and hit-target reset only.
- Hit detection should be triggered by Animation Events at the real hit frame.
- Damage should flow through `AbilitySystemComponent` and `GameplayEffect`, not through a simple `Health` or `IDamageable` pattern.
- `StateFactory` should create states only.
- `StateMachineBuilder` should read config data and assemble states/transitions.
- Character-specific state availability should live in `CharacterStateConfig` assets.

## Important Files

State config and state construction:

- `Client/Assets/Script/Main/Character/State/Configs/StateKind.cs`
- `Client/Assets/Script/Main/Character/State/Configs/StateDefinition.cs`
- `Client/Assets/Script/Main/Character/State/Configs/CharacterStateConfig.cs`
- `Client/Assets/Script/Main/Character/State/Factory/IStateFactory.cs`
- `Client/Assets/Script/Main/Character/State/Factory/StateFactory.cs`
- `Client/Assets/Script/Main/Character/State/Builder/IStateMachineBuilder.cs`
- `Client/Assets/Script/Main/Character/State/Builder/StateMachineBuilder.cs`

Agents:

- `Client/Assets/Script/Main/Character/Agent/CharacterAgent.cs`
- `Client/Assets/Script/Main/Character/Agent/PlayerCharacterAgent.cs`
- `Client/Assets/Script/Main/Character/Agent/NPCCharacterAgent.cs`

Attack state and transitions:

- `Client/Assets/Script/Main/Character/State/AttackState.cs`
- `Client/Assets/Script/Main/Character/State/Transitions/GroundToAttackTransition.cs`
- `Client/Assets/Script/Main/Character/State/Transitions/AttackToGroundTransition.cs`

Input:

- `Client/Assets/Script/Main/Character/Input/CharacterInputFrame.cs`
- `Client/Assets/Script/Main/Character/Input/ICharacterInputSource.cs`
- `Client/Assets/Script/Main/Character/Input/ICharacterInputWriter.cs`
- `Client/Assets/Script/Main/Character/Input/CharacterInputBuffer.cs`
- `Client/Assets/Script/Main/Character/Input/PlayerInputComponent.cs`

Hit and damage:

- `Client/Assets/Script/Main/Character/Weapon/HitDetector.cs`
- `Client/Assets/Script/Main/Character/CharacterHitEventReceiver.cs`
- `Client/Assets/Script/Main/System/GamePlayAbilitySystem/AbilitySystemComponent.cs`
- `Client/Assets/Script/Main/System/GamePlayAbilitySystem/Attributes/GameplayAttribute.cs`
- `Client/Assets/Script/Main/System/GamePlayAbilitySystem/Effects/GameplayEffect.cs`

State config assets:

- `Client/Assets/GameResources/StateConfigs/PlayerStateConfig.asset`
- `Client/Assets/GameResources/StateConfigs/NpcStateConfig.asset`

Docs:

- `Client/docs/Chapter_1_Input_Architecture.md`
- `Client/docs/Chapter_2_Jump_State.md`
- `Client/docs/Chapter_3_Interaction.md`
- `Client/docs/Chapter_4_Attack_State_Hit_Detection.md`

Server solution:

- `ServerAll/ServerAll.sln`
- `ServerAll/Infra/docker-compose.yml`
- `ServerAll/GameServer/GameServer.API/GameServer.API.csproj`
- `ServerAll/GameServer/GameServer.API/Program.cs`
- `ServerAll/GameServer/GameServer.API/appsettings.json`
- `ServerAll/GameServer/GameServer.Application/GameServer.Application.csproj`
- `ServerAll/GameServer/GameServer.Domain/GameServer.Domain.csproj`
- `ServerAll/GameServer/GameServer.Infrastructure/GameServer.Infrastructure.csproj`
- `ServerAll/GameServer/GameServer.Tests/GameServer.Tests.csproj`
- `ServerAll/SocketServer/SocketServer/SocketServer.csproj`
- `ServerAll/SocketServer/SocketServer/Program.cs`
- `ServerAll/SocketServer/SocketServer/appsettings.json`
- `ServerAll/Shared/Shared.Contracts/Shared.Contracts.csproj`
- `ServerAll/Shared/Shared.Contracts/Protos/auth.proto`
- `ServerAll/Shared/Shared.Contracts/Protos/chat.proto`
- `ServerAll/Shared/Shared.Contracts/Protos/common.proto`
- `ServerAll/Shared/Shared.Contracts/Protos/lobby.proto`
- `ServerAll/Shared/Shared.Contracts/Protos/user.proto`
- `ServerAll/Shared/Shared.Infrastructure/Shared.Infrastructure.csproj`
- `ServerAll/Shared/Shared.Packet/Shared.Packet.csproj`
- `ServerAll/Shared/Shared.Packet/Packets/Packet.cs`
- `ServerAll/Shared/Shared.Packet/Packets/Domains/AttackPacket.cs`
- `ServerAll/Shared/Shared.Packet/Packets/Domains/MovementPackets.cs`
- `ServerAll/Shared/Shared.Packet/PacketSerializer.cs`
- `ServerAll/Tools/ClientCodegen/ClientCodegen.csproj`

## Current Feature Status

Implemented:

- `StateKind`, `StateDefinition`, and `CharacterStateConfig`.
- `StateFactory` refactored to create states by kind.
- `StateMachineBuilder` added in its own folder.
- `CharacterAgent` exposes `CurrentStateKind` in the Unity Inspector.
- Attack input added to the input frame/buffer/source/writer flow.
- `GroundToAttackTransition` added.
- `PlayerStateConfig` includes `Attack`.
- `AttackState` triggers the attack animation.
- User confirmed the attack animation plays with RightHook in `PlayerController`.
- `HitDetector` detects targets with `Physics.OverlapBoxNonAlloc`.
- `CharacterHitEventReceiver` receives Animation Events and applies damage.
- `GameplayAttribute` is serializable in Inspector.
- User confirmed target health decreases.

Not done yet:

- `TransitionDefinition` and `TransitionFactory`.
- Data-driven transition setup.
- Full GAS damage spec/execution.
- Team/faction/owner filtering.
- Hit reaction, death, knockback, guard, and invincibility handling.
- Server-authoritative multiplayer hit/damage flow.
- Weapon-specific hurtbox/hitbox profiles.

## ServerAll Overview

`ServerAll` is the backend solution for the multiplayer architecture described in the root `README.md`.

The intended model is hybrid:

- `GameServer.API`: ASP.NET Core server for account/auth/user/chat/dungeon lobby through gRPC and HTTP endpoints.
- `SocketServer`: TCP socket server for real-time dungeon gameplay, movement, room state, and combat packets.
- `Shared.Contracts`: Protocol Buffer contracts for gRPC APIs.
- `Shared.Packet`: MemoryPack packet contracts for TCP socket traffic.
- `Shared.Infrastructure`: shared Redis message queue and broadcast abstractions.
- `DummyClient`: console client for server testing.
- `Tools/ClientCodegen`: code generation tool triggered after `GameServer.API` builds unless `SKIP_CODEGEN=true`.

The root README states the long-term architecture clearly: gRPC handles most request/response and streaming game flows, while TCP Socket plus binary serialization handles low-latency co-op combat and movement.

## ServerAll Runtime Settings

Local service dependencies are defined in:

- `ServerAll/Infra/docker-compose.yml`

Docker services:

- `gameserver`: exposes `5131` for HTTP/REST/Swagger style traffic and `5132` for plaintext gRPC over HTTP/2.
- `socketserver`: exposes TCP `7777`.
- `postgres`: `localhost:5432`, database `gamedb`, user `gameuser`, password `gamepass123`.
- `redis`: `localhost:6379`.
- `pgadmin`: `http://localhost:5050`, email `admin@gameserver.com`, password `admin123`.
- `graylog`: `http://127.0.0.1:9000`, default note in compose says `admin / admin`.
- `mongodb` and `opensearch`: used by Graylog.

`ServerAll/Infra/.env` exists and currently provides `Jwt__Secret`. Do not print or commit secret values.

Useful commands:

```powershell
cd ServerAll\Infra
docker compose up -d

cd ..
dotnet build ServerAll.sln --no-restore
dotnet run --project GameServer\GameServer.API\GameServer.API.csproj
dotnet run --project SocketServer\SocketServer\SocketServer.csproj
```

For faster server builds without Unity client code generation:

```powershell
$env:SKIP_CODEGEN='true'
dotnet build ServerAll\ServerAll.sln --no-restore
```

## GameServer.API Notes

`GameServer.API` uses:

- Target framework: `net10.0`.
- ASP.NET Core gRPC.
- EF Core with PostgreSQL.
- Redis via infrastructure repositories/queues.
- Serilog console/file/Graylog logging.
- JWT settings from `appsettings.json` plus environment variables.

Important behavior:

- `Program.cs` loads `ServerAll/Infra/.env` from several relative paths.
- Kestrel listens on `5131` with HTTP/1.1 and `5132` with HTTP/2 for gRPC.
- On startup, it runs EF Core migrations through `GameServerDbContext.Database.MigrateAsync()`.
- Domain registrations are split across installers: Common, User, Auth, Chat, Dungeon.
- gRPC services include Auth, Chat, DungeonLobby, and User.

Dungeon/lobby flow:

- Players create/join/start dungeon rooms through `DungeonLobbyGrpcService`.
- `DungeonLobbyService.StartGameAsync` marks the room as starting and creates a `GameStartRequestedMessage`.
- The message is stored through the outbox flow and published toward Redis-backed queues.
- When the socket server reports readiness, `GameSessionReadyConsumer` creates a game session, stores socket host/port, moves the room to playing, and publishes a subscription update.
- `SubscribeRoom` sends a `GameSessionReadyEvent` containing socket IP and port to clients.

## SocketServer Notes

`SocketServer` uses:

- Target framework: `net10.0`.
- Plain TCP listener.
- MemoryPack packet serialization.
- Redis message queues.
- Serilog console/file/Graylog logging.

Default socket settings:

- `ServerOptions.Ip`: `127.0.0.1`.
- `ServerOptions.Port`: `7777`.
- Docker overrides IP to `0.0.0.0`.

Runtime services registered in `Program.cs`:

- `TcpListenerService`
- `GameStartRequestedConsumer`
- `HeartBeatService`
- `TestRoomService`
- `RoomManager`
- `SessionManager`
- `PacketHandlerRegistry`
- `PacketDispatcher`

TCP packet framing:

- First 4 bytes are packet length using `BitConverter`.
- Payload is a MemoryPack-serialized `Packet`.
- Max accepted packet length is `65536`.

Current packet union IDs:

- `1300`: `C_Auth`
- `1301`: `S_Auth`
- `1310`: `C_PlayerJoin`
- `1311`: `S_PlayerJoined`
- `1312`: `C_PlayerLeave`
- `1313`: `S_PlayerLeft`
- `1400`: `C_Ping`
- `1401`: `S_Pong`
- `1500`: `C_Move`
- `1501`: `S_Move`
- `1600`: `C_Attack`
- `1601`: `S_Attack`
- `1701`: `S_GameStatus`

Current real-time behavior:

- `C_Auth` checks whether the user was assigned to a room by the GameServer/Redis flow.
- `C_Move` updates the player's state in the room and broadcasts `S_Move` to other sessions.
- `C_Attack`/`S_Attack` packet contracts exist, but server-authoritative combat validation and damage resolution still need to be implemented.
- `RoomManager` creates rooms from `GameStartRequestedMessage`, assigns spawn positions, tracks session-to-room mappings, and removes empty rooms.

## Client/Server Combat Integration Direction

The current Unity client damage flow is local MVP. The server already has the rough packet surface for real-time combat:

- Client-side local animation and feel should stay responsive.
- Server should eventually receive an attack intent packet such as `C_Attack`.
- Server should validate room membership, attacker state, target/hit timing, range, cooldown, and combat rules.
- Server should compute authoritative damage.
- Server should broadcast `S_Attack` or a richer combat result packet to other clients.
- Client should reconcile local predicted hit effects against server results.

For the next combat-network step, do not wire the current Unity `CharacterHitEventReceiver` directly as the final authority. Treat it as local prediction/visual timing until the socket server damage model exists.

## Manual Unity Setup Assumptions

For the current MVP to work:

- Player must have `HitDetector`.
- Player must have `CharacterHitEventReceiver`.
- Player or target objects that can receive damage must have `AbilitySystemComponent`.
- Damageable targets need a Health `GameplayAttribute`.
- Damageable targets need colliders.
- `HitDetector.TargetLayerMask` must include the target layer.
- The RightHook animation clip should call `PerformHit` via Animation Event at the real hit frame.
- Optional Animation Events can call `BeginHitWindow` and `EndHitWindow` if a wider active window is needed.

## Known Caveats

- Existing files under `Client/docs` may display garbled Korean in some shells because of prior encoding issues. New docs should be written as UTF-8.
- `Client/Game.Main.csproj` was manually updated to include newly added files so command-line build works before Unity regenerates project files.
- `CharacterStateId.cs` may be leftover from the previous state system and should be reviewed later.
- Current `HitDetector` is a simple box overlap. It is enough for RightHook MVP but not final for all melee weapons.
- Current damage effect is a simple additive Health reduction.
- There is no `READMD.MD` under `ServerAll`; the root `README.md` is the useful architecture reference.
- `GameServer.API` build triggers `Tools/ClientCodegen` unless `SKIP_CODEGEN=true`.
- `SocketServer` has attack packets but does not yet implement final authoritative combat logic.
- `docker-compose.yml` contains local development credentials; keep production secrets out of source.

## Recommended Next Steps

1. Tune the RightHook Animation Event frame and `HitDetector` box values in Unity.
2. Create or verify a test enemy with collider, layer, `AbilitySystemComponent`, and Health attribute.
3. Add hit reaction and death behavior.
4. Add `TransitionDefinition` and move transition registration out of hardcoded builder logic.
5. Expand damage into a proper GAS-style damage spec or execution object.
6. Decide the multiplayer authority model for hit detection and damage application.
7. Connect Unity attack intent to `SocketServer` `C_Attack` after server-side validation rules are designed.
8. Implement server-side combat result broadcasting with `S_Attack` or a richer combat result packet.
