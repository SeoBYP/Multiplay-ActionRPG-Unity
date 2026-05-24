# Unity 클라이언트 규칙

## gRPC 채널

Unity 기본 `Grpc.Net.Client`는 HTTP/2(h2c) 미지원 → `YetAnotherHttpHandler` 필수.
서비스마다 채널 별도 생성 금지. `GrpcChannelProvider`에서 채널을 공유.

```
GrpcChannelProvider
  ├── YetAnotherHttpHandler (HTTP/2 h2c 강제)
  ├── Authorization 헤더 자동 주입 (AccessToken)
  └── 채널 공유 — 서비스마다 별도 채널 생성 금지
```

파일: `Client/Assets/Script/Network/Https/Core/GrpcChannelProvider.cs`

## VContainer DI

- MonoBehaviour Singleton 도입 금지. VContainer scope 사용.
- 네트워크 레이어를 씬 오브젝트 생명주기에 묶지 않는다.
- 새 서비스 등록 시 lifetime, scope, 인스턴스 공유 여부를 명시적으로 결정한다.
- 생산자·소비자가 동일 객체를 참조해야 하는 경우 (예: `CharacterInputBuffer`) 반드시 같은 scope에서 단일 인스턴스로 등록.

## Unity lifecycle vs DI lifecycle

`OnEnable()`이 `IInitializable.Initialize()`보다 먼저 실행될 수 있다.  
주입 객체를 `OnEnable()`에서 즉시 사용하는 코드 작성 금지. 초기화 완료 여부를 확인 후 사용.

DI 디버깅 시 "null 여부"만 보지 않는다. 반드시 확인:
- 생명주기(Transient vs Singleton vs Scoped)
- 동일 인스턴스 공유 여부
- scope 경계

## Socket 클라이언트

```
SocketConnector  → TCP 연결 관리
SocketSession    → 패킷 송수신 (MemoryPack 직렬화)
```

연결 후 순서: `C_Auth` 완료 → `C_PlayerJoin`. Auth 전 Join 요청 금지.

## gRPC 스트림 취소 처리

`OperationCanceledException`과 `RpcException(StatusCode.Cancelled)` 둘 다 처리.
한쪽만 처리하면 정상 종료가 에러로 기록됨.
