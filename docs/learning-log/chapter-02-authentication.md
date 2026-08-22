# 챕터 2 학습 로그 — 인증 시스템

## 처음 알았던 것 vs 피드백으로 수정된 것

### 패스워드 해싱 — BCrypt vs SHA256

**처음 내가 알고 있던 것:**
SHA256으로 해싱하면 충분하다고 생각했음.

**피드백:**
패스워드에 SHA256은 위험함. BCrypt를 써야 한다.

| | SHA256 | BCrypt |
|--|--|--|
| Salt | 직접 관리 필요 | 자동 내장 |
| Rainbow Table 공격 | 취약 | 강력 |
| 연산 속도 | 매우 빠름 (공격자에게 유리) | 의도적으로 느림 |

**실제 코드 확인 결과:**
코드를 열어보니 이미 BCrypt 사용 중이었음 → 내 처음 예상이 틀렸던 것.

**추가로 배운 것:**
Refresh Token 해싱에는 SHA256이 맞음. 이유:
- Refresh Token은 이미 32바이트 랜덤값 → Rainbow Table 의미 없음
- 갱신 요청마다 해시 비교 발생 → BCrypt처럼 느리면 성능 문제
- 용도에 맞게 선택하는 것이 핵심

---

### JWT Access Token + Refresh Token 구조

**내가 이해한 것:**
Access Token만 쓰면 만료 시간을 길게 하거나 짧게 해야 하는데 둘 다 문제가 생긴다.
→ Refresh Token으로 조용히 갱신하는 구조가 해결책.

**피드백으로 추가된 것:**
게임에서는 Refresh Token 만료 시간을 짧게 가져가는 것이 맞다.
현재 코드는 7일인데 게임 특성상 1~24시간이 적절함.
만료 시 재로그인으로 처리 (TODO).

---

### Token Rotation

**내가 이해한 것:**
갱신할 때마다 새 토큰으로 교체. 구 토큰은 즉시 폐기.

**피드백으로 추가된 것:**
Token Rotation만으로는 탈취 방어가 완전하지 않다.
"이미 교체된 구 토큰으로 누군가 갱신 요청을 보내면?" → 감지 로직 없음.
→ **Reuse Detection** 필요 (현재 미구현, TODO)

---

### DeviceId Binding

**내가 처음 설계한 것:**
RefreshToken을 DB에 저장할 때 DeviceId + UserId 기준으로 만들겠다.

**피드백 — 생성 방식은 틀렸다:**
토큰 생성은 Random이어야 함. DeviceId로 만들면 동일 기기에서 매번 같은 토큰이 생성됨 → 탈취 후 재발급해도 동일 토큰 → 공격자 재사용 가능.

**올바른 설계:**
- 생성 → Random (예측 불가능성 보장)
- 바인딩 → `SHA256(randomToken + deviceId)` 를 DB에 저장

DeviceId를 별도 컬럼으로 저장하지 않아도 됨. 이유:
- DB에 DeviceId 평문 노출 없음
- 검증 시 동일 방식으로 해싱해서 비교
- 단일 기기 정책에서 추가 컬럼 불필요

**수정 완료:**
`HashRefreshToken(string refreshToken, string deviceId)` 구현.

---

### RefreshToken 별도 테이블 고민

**내가 처음 생각한 것:**
RefreshTokens 별도 테이블을 만들어야 한다.

**피드백:**
단일 기기 정책(마지막 로그인 기기만 유효)이라면 User 테이블의 단일 컬럼으로 충분함.
새 로그인 시 덮어쓰기 → 구 토큰 자연 소멸 → 별도 테이블 불필요.

별도 테이블이 필요한 경우: 다중 기기 동시 로그인 허용 정책일 때.

---

### 유저 도메인 분리 — UserProfile / UserCredential / UserSession

**예전에는 어떻게 봤나:**
`User` 하나가 인증 정보, 프로필 정보, 세션 연관 책임까지 같이 들고 있어도 된다고 생각했음.

**최근 리팩터링으로 정리된 것:**
책임이 다른 데이터는 분리하는 편이 맞았음.

- `UserProfile`
  - 닉네임, 공개 프로필 성격의 정보
- `UserCredential`
  - 이메일, 비밀번호 해시 같은 인증 정보
- `UserSession`
  - 세션/토큰 흐름과 연결되는 로그인 상태 정보

**배운 점:**
- 인증 로직과 프로필 수정 로직은 변경 이유가 다름
- 같은 `User`에 몰아넣으면 서비스와 저장소가 비대해짐
- 책임 기준으로 분리해야 테스트도 단순해지고, API도 역할이 명확해짐

---

### AccountService 분리

**이전 구조의 문제:**
회원가입, 자격 증명 검증, 로그인 토큰 발급이 `AuthService` 쪽에 한꺼번에 모이기 쉬웠음.

**현재 구조에서 정리된 것:**

- `AccountService`
  - 회원가입
  - 이메일/비밀번호 검증
  - 비밀번호 변경
- `AuthService`
  - 로그인
  - Refresh
  - Logout
  - 세션/토큰 발급과 검증

**배운 점:**
- 계정 관리와 인증 세션 관리는 비슷해 보여도 책임이 다름
- `AuthGrpcService`가 회원가입 시 `AccountService`, 로그인/리프레시는 `AuthService`를 나눠 호출하는 구조가 더 자연스러움
- 이렇게 분리하면 이후 소셜 로그인, 비밀번호 재설정, 계정 정책 추가도 확장하기 쉬움

---

### Redis vs DB 저장 고민

**내 고민:**
1. 이미 사용된 구 토큰을 저장하는 게 현재 유효한 토큰을 저장하는 것과 결국 같지 않나?
2. NoSQL(MongoDB)이 맞는 선택인가?
3. Redis 유실 위험이 있는데?

**피드백:**
1. 맞음. Blacklist(구 토큰 저장) vs Whitelist(현재 토큰 저장)는 논리적으로 동등한 구조. 차이는 레코드 수 관리 방식. Whitelist가 관리 단순.

2. 토큰 스키마는 고정적 + PostgreSQL 이미 사용 중 → MongoDB 추가는 오버엔지니어링. 인프라 복잡도만 높아짐.

3. 정확함. **Cache-Aside 패턴**이 정답:
   - 쓰기: PostgreSQL 먼저 저장 (영속성) → Redis 캐시 업데이트
   - 읽기: Redis 먼저 조회 → Miss 시 PostgreSQL 조회 후 캐싱

---

### Clean Architecture 수정 이력

**발견된 문제:**
- `IJwtTokenGenerator.cs`, `IPasswordHasher.cs` 파일이 `Application/Security/` 폴더에 있었지만 네임스페이스가 `GameServer.Infrastructure.Interfaces` 였음
- `AuthService.cs`에서 `using GameServer.Infrastructure.*` 참조하고 있었음

**수정 내용:**
- 인터페이스 네임스페이스를 `GameServer.Application.Security.Interface`로 변경
- `IUserRepository`를 Application 레이어로 이동
- `AuthService.cs`의 모든 `using`에서 Infrastructure 참조 제거

**확인 방법:**
`AuthService.cs`의 using 목록에 `GameServer.Infrastructure.*`가 없어야 올바른 구조.

---

## 현재 코드에서 아직 미완성인 것 (TODO)

```csharp
// Binding 실패 시 보안 위험으로 간주하고 세션 종료 고려 가능
// 여기서는 일단 실패 반환  ← 이 주석
```

**해야 할 것:**
Binding 실패 = 다른 DeviceId로 갱신 시도 = 탈취 의심
→ 단순 실패 반환이 아니라 해당 유저 세션 강제 만료 + RefreshToken 삭제

**추가 개선 목록:**
- `HashRefreshToken` → `private static`으로 변경 (인스턴스 상태 안 씀)
- RefreshToken 검증 순서: null 체크 → **만료일 체크** → 해시 비교 (현재는 만료일이 마지막)
- `CryptographicOperations.FixedTimeEquals()` 로 타이밍 공격 방어 (현재 `!=` 사용)
- 에러 로깅 추가 (외부에는 동일 에러코드, 내부 로그는 원인 구분)
- Refresh Token 만료 시간 7일 → 게임 특성에 맞게 단축

---

## 핵심 키워드 정리

| 키워드 | 한 줄 설명 |
|--------|-----------|
| Token Rotation | 갱신 시마다 새 토큰으로 교체, 구 토큰 즉시 폐기 |
| Reuse Detection | 이미 교체된 구 토큰 재사용 감지 → 전체 세션 무효화 |
| DeviceId Binding | SHA256(token + deviceId)로 기기 귀속 |
| Cache-Aside 패턴 | DB 먼저 저장 → Redis 캐싱, 읽기 시 Redis 우선 |
| Whitelist 방식 | 현재 유효한 토큰만 DB에 보관, 없으면 무효 |
| Timing Attack | 비교 시간 차이로 값 유추 → FixedTimeEquals로 방어 |
| BCrypt cost factor | 연산 비용 조절로 하드웨어 발전해도 해킹 난이도 유지 |
