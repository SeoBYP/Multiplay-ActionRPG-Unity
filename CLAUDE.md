# Multiplay ActionRPG

Unity 멀티플레이 액션 RPG 포트폴리오.
**이중 서버**: GameServer (gRPC/HTTP) + SocketServer (TCP/MemoryPack) | **클라**: Unity + VContainer | **.NET 10**

## 현재 작업 확인 (새 채팅 시 필독)

**[docs/wiki/plan.md](docs/wiki/plan.md)** — 지금 어떤 Phase를 작업 중인지, 어떤 태스크가 남았는지 항상 여기서 확인한다.

**plan.md 갱신 규칙 (필수)**:
- Phase 태스크 완료 시 → 체크박스 `[ ]` → `[x]` 즉시 갱신
- Phase 전체 완료 시 → "현재 작업" 섹션을 다음 Phase로 교체하고 완료된 Phase를 "완료" 섹션으로 이동
- 갱신 없이 "완료됐다"고 하지 않는다

## 작업 완료 후 필수 설명 (코드 작업 시 항상)

코드를 작성/수정했다면 "완료됐습니다"로 끝내지 않는다. 반드시 아래 형식으로 설명한다:

1. **무엇을 했는가** — 변경/추가된 코드가 하는 일
2. **왜 이렇게 설계했는가** — 다른 방법 대신 이 방법을 선택한 이유
3. **코드 위치** — 파일 경로, 클래스명, 메서드명
4. **연결 지점** — 이 코드가 어디서 호출되고 어디로 연결되는가

## 핵심 작업 원칙

- 편집 전 관련 파일을 반드시 먼저 읽는다.
- 코드는 가이드만. "전체 코드 줘" 명시 시에만 전체 작성.
- 비자명 변경은 먼저 계획을 제시하고 승인 후 실행한다.
- 공개 계약(proto, 패킷 Union ID, 직렬화 필드, asmdef)은 명시 요청 없이 변경하지 않는다.
- **proto 파일을 수정하면 반드시 클라이언트 Generated/ 파일을 즉시 재생성한다** (protoc 명령 — 아래 참조).
- 변경 후 관련 빌드/테스트 명령을 실행한다. 실행하지 않고 "검증됨"이라고 하지 않는다.

## 검증 명령

```powershell
# 클라이언트
dotnet build Client\Game.Main.csproj --no-restore

# 서버 (코드젠 스킵)
dotnet build ServerAll\ServerAll.sln --no-restore -p:SKIP_CODEGEN=true
```

## proto 수정 후 클라이언트 재생성 (필수)

`.proto` 파일을 변경할 때마다 아래 명령으로 `Client/Assets/Script/Network/Https/Generated/` 를 갱신한다.

```bash
PROTOC="C:/Users/user/.nuget/packages/grpc.tools/2.76.0/tools/windows_x64/protoc.exe"
PLUGIN="C:/Users/user/.nuget/packages/grpc.tools/2.76.0/tools/windows_x64/grpc_csharp_plugin.exe"
PROTO_DIR="C:/Users/user/Github/Multiplay-ActionRPG-Unity/ServerAll/Shared/Shared.Contracts/Protos"
OUT_DIR="C:/Users/user/Github/Multiplay-ActionRPG-Unity/Client/Assets/Script/Network/Https/Generated"

"$PROTOC" \
  --proto_path="$PROTO_DIR" \
  --csharp_out="$OUT_DIR" \
  --grpc_out="$OUT_DIR" \
  --plugin=protoc-gen-grpc="$PLUGIN" \
  "$PROTO_DIR/common.proto" \
  "$PROTO_DIR/auth.proto" \
  "$PROTO_DIR/user.proto" \
  "$PROTO_DIR/lobby.proto" \
  "$PROTO_DIR/chat.proto"
```

변경한 proto만 재생성해도 되지만, 의존 관계(import)가 있는 경우 함께 재생성한다.

## 아키텍처 요약

```
GameServer.API → Application ← Infrastructure → PostgreSQL + Redis
SocketServer (TCP) ←→ Redis Streams ←→ GameServer.API
Unity Client → gRPC → GameServer.API
Unity Client → TCP → SocketServer
```

의존성 방향: `API → Application ← Infrastructure`  
Application이 Infrastructure를 직접 참조하면 위반.

## 작업 전 필독 Wiki

| 작업 | 파일 |
|------|------|
| 도메인/서비스 추가 | [docs/wiki/architecture.md](docs/wiki/architecture.md) |
| 패킷 추가/수정 | [docs/wiki/packets.md](docs/wiki/packets.md) |
| SocketServer 작업 | [docs/wiki/socketserver.md](docs/wiki/socketserver.md) |
| Redis 관련 | [docs/wiki/redis.md](docs/wiki/redis.md) |
| 서버 연동 흐름 | [docs/wiki/gameflow.md](docs/wiki/gameflow.md) |
| Unity 클라이언트 + gRPC | [docs/wiki/unity-client.md](docs/wiki/unity-client.md) |
| 입력 버퍼 + InputRouter 설계 | [docs/portfolio/chapter-10-unity-input-system.md](docs/portfolio/chapter-10-unity-input-system.md) |
| 상태머신 + AttackState + Hit | [docs/portfolio/chapter-10-unity-gameplay-state.md](docs/portfolio/chapter-10-unity-gameplay-state.md) |
| MVI 아키텍처 | [docs/portfolio/chapter-10-mvi-architecture.md](docs/portfolio/chapter-10-mvi-architecture.md) |
| 레이어 분리 (asmdef) | [docs/portfolio/chapter-10-layer-separation.md](docs/portfolio/chapter-10-layer-separation.md) |
| 인증 초기화 순서 | [docs/portfolio/chapter-10-lifetime-auth.md](docs/portfolio/chapter-10-lifetime-auth.md) |
| 현황 확인 | [docs/wiki/status.md](docs/wiki/status.md) |

## 세부 규칙 인덱스

작업 영역에 맞는 규칙 파일을 작업 전에 읽는다.

| 영역 | 규칙 파일 |
|------|-----------|
| 서버 Clean Architecture + 도메인 경계 | [.claude/rules/architecture-server.md](.claude/rules/architecture-server.md) |
| SocketServer + 패킷 + Redis + 게임 흐름 | [.claude/rules/networking.md](.claude/rules/networking.md) |
| Unity 클라이언트 + gRPC + VContainer | [.claude/rules/unity-client.md](.claude/rules/unity-client.md) |
| 입력 시스템 분리 | [.claude/rules/unity-input.md](.claude/rules/unity-input.md) |
| 상태머신 + AttackState + 데미지 흐름 | [.claude/rules/unity-gameplay-state.md](.claude/rules/unity-gameplay-state.md) |
| 테스트 규칙 | [.claude/rules/testing.md](.claude/rules/testing.md) |

## 절대 금지

- `.env` 파일 읽기 / 출력 / 수정
- `git push` 실행
- `rm -rf` 또는 일괄 삭제
- 생성 파일 직접 수정 (`Library/`, `Temp/`, `obj/`, `Generated/`, ClientCodegen 출력)
- `Application → Infrastructure` 직접 참조 추가
- `SocketServer → GameServer` 직접 RPC 호출 추가
- `StreamPosition.NewMessages("$")` 사용
- Redis 트랜잭션 내부 `await` 사용
