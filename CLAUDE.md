# Multiplay ActionRPG

Unity 멀티플레이 액션 RPG 포트폴리오.
**이중 서버**: GameServer (gRPC/HTTP) + SocketServer (TCP/MemoryPack) | **클라**: Unity + VContainer | **.NET 10**

## 최우선 코딩 원칙 (항상 적용 — 다른 모든 지침에 우선)

이 6개는 모든 코드 작업의 최상위 규칙이다. 다른 지침과 충돌하면 이 원칙이 우선한다.

1. **간결성 우선 · 과도한 추상화 금지** — 50줄로 충분한 코드를 200줄로 늘리지 않는다. 지금 필요 없는 추상화(불필요한 인터페이스/제네릭/레이어)는 만들지 않는다. (YAGNI)
2. **Client 어셈블리(asmdef) 과도 분리 금지** — Domain·생명주기 단위로만 나눈다. 잘게 쪼개면 오히려 코드를 읽기 어렵게 만든다.
3. **Client는 게임 생명주기에 맞춰 설계** — 예: GameHud는 Lobby·Dungeon 양쪽에서 쓰이므로 어느 씬에서든 언제든 생성·해제할 수 있어야 한다. 특정 씬에 묶지 않는다.
4. **자주 쓰이는 코드는 모듈화·응집성·SOLID** — 반복·공유 로직은 책임 단위로 묶고 SOLID 관점에서 설계한다. 단, 1번과 균형: 응집은 높이되 과추상화는 피한다.
5. **코드 수정 전 방향 협의 필수** — 비자명한 변경은 먼저 방향/계획을 제시하고 승인을 받은 뒤 실행한다. 협의 없이 곧장 코드를 고치지 않는다.
6. **읽으며 발견한 결함은 항상 정리해 보고** — 빠진 처리·누락·엣지케이스, 서버의 보안·유지보수 취약점을 발견하면 시키지 않아도 정리해 보여준다. (고치기 전 5번에 따라 협의)

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

## 설계·기획 시 필수 — Flow 다이어그램 (항상)

설계/기획을 제시할 때는 **반드시 ASCII Flow 다이어그램을 그려가며** 설명한다. 글머리표 나열만으로 끝내지 않는다.

- **단계별 흐름을 그린다** — 입력→처리→출력, 상태 전이, 호출 경로를 화살표(`→ ▼ ├─▶`)로 시각화한다.
- **상황(시나리오)별로 분리해 그린다** — 예: 평상시 / 트리거 발생 / 점유 중 / 해제 / 엣지(중첩 등). 한 다이어그램에 다 욱여넣지 않는다.
- **컴포넌트 배치도를 먼저** — 누가 누구를 호출하는지(레이어/의존 방향 포함) 한 장으로 보인 뒤 흐름으로 들어간다.
- **실제 타입/파일명으로** — 추상적 박스 말고 코드에 존재하는(또는 만들) 클래스·메서드·맵 이름으로 그린다.
- 다이어그램 뒤에 "왜 이 구조인가 / 대안 대비 장점 / 변경 규모"를 짧게 붙인다.
- 과설계 금지(최우선 원칙 1번)와 충돌하면 간결성이 우선 — 다이어그램도 군더더기 없이.

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
  "$PROTO_DIR/chat.proto" \
  "$PROTO_DIR/inventory.proto"
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
| **클라 vs 서버 권위 (전투·수치·연출 설계)** | [docs/wiki/authority-model.md](docs/wiki/authority-model.md) |
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
| **멀티플레이 테스트 (MPPM 2-창 / E2E)** | [docs/wiki/mppm-testing.md](docs/wiki/mppm-testing.md) |
| 현황 확인 | [docs/wiki/plan.md](docs/wiki/plan.md) |

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

## GBrain 설정 (configured by /setup-gbrain, 2026-06-03)

- Mode: local-stdio · Engine: PGLite (`~/.gbrain/brain.pglite`) · 임베딩: `ollama:nomic-embed-text` (768d, 로컬)
- CLI: `C:\Users\user\.bun\bin\gbrain.exe` (PATH 미등록 — 절대경로로 호출). `bun` 미설치라 gstack 헬퍼는 동작 안 함.
- MCP: 절대경로로 user scope 재등록 완료 (`gbrain serve`, ✓ Connected). **이미 열린 Claude 세션은 재시작해야 `mcp__gbrain__*` 툴이 보임.**
- 코드 소스: `actionrpg` (federated, 474 pages / 714 chunks) — Client/Assets/Script + ServerAll 만.

### 왜 sparse 워크트리인가

gbrain `sync --strategy code`의 **첫 동기화는 .gitignore를 무시하는 파일시스템 walk**라, repo 루트를 가리키면 Unity `Library/`·`obj/`·`bin/`·`Packages/` 캐시까지 8751개를 인덱싱한다. `sync`는 소스 경로가 git 루트여야 해서 하위 디렉터리로 범위를 좁힐 수도 없다. 그래서 **코드 디렉터리만 담은 detached sparse git worktree**를 만들어 그곳을 소스로 지정했다 (생성파일은 gitignore라 워크트리에 애초에 없음 → 깔끔하게 ~474개만).

- 워크트리 위치: `C:\Users\user\.gbrain-worktrees\actionrpg-code` (repo 밖 — Unity가 안 건드림)

### 인덱스 자동 갱신 (commit 시 자동)

`commit`할 때마다 **`.git/hooks/post-commit`**(git-lfs 훅에 추가됨)이 백그라운드로 갱신 스크립트를 돌린다 → commit은 안 느려짐.
- 갱신 스크립트: `C:\Users\user\.gbrain-worktrees\gbrain-refresh.sh` (로그: `refresh.log`)
- 동작: worktree를 새 HEAD로 `checkout --detach` → `gbrain sync --no-pull`.
- **PGLite는 single-writer**라 `gbrain serve`(MCP)가 떠 있으면 락을 잡는다. 스크립트는 락이 걸리면 stray `serve` 프로세스를 종료(Claude가 필요 시 자동 재기동)하고 재시도한다. 그래서 첫 시도가 ~30초 대기 후 재시도할 수 있으나 백그라운드라 무방.
- 수동 갱신: `bash "C:/Users/user/.gbrain-worktrees/gbrain-refresh.sh"`
- 비활성화: post-commit 훅의 gbrain 블록 삭제.

## GBrain 검색 가이드

코드 위치/의도를 모를 때 Grep보다 gbrain을 먼저 쓴다.

- "X 어디서 처리?" / 의미 기반: `gbrain.exe search "<terms>"` 또는 `gbrain.exe query "<question>"`
  - 코드만: `--source actionrpg` / 노트·회고: `--source default` 또는 `developer-retrospect`
- **C# 한계**: gbrain은 8개 언어만 심볼/콜그래프 엣지를 추출하며 **C#은 미포함**. → `code-def`/`code-refs`/`code-callers`는 C#에서 빈 결과. 의미 검색(`search`/`query`)은 정상.
- 정확한 문자열·정규식·파일 글롭은 여전히 Grep이 맞다.
- 임베딩 실패(파일 길이 초과)한 소수 파일(예: 생성된 `PlayerInputActions.cs`)은 키워드 검색으로 폴백.
- 선택적 후속: `gbrain.exe extract --stale` (링크 그래프 추출 — 브레인 전체 대상, C#엔 효과 미미).
