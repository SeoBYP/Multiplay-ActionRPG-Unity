# Contracts — 분리 대기 구역 (임시)

**이 폴더는 최종 위치가 아니다.** 나중에 별도 어셈블리로 떼어내기 쉽도록 **한곳에 모아둔 것**이다.

## 무엇이 여기 있나

클라이언트와 서버가 **둘 다 쓰는 데이터 계약**(enum·구조체·순수 roll 함수):

| 파일 | 클라 참조 파일 수(2026-08-27 실측) |
|---|---:|
| `EquipmentType.cs` | 12 |
| `ShopCategory.cs` | 14 |
| `ItemGrade.cs` | 7 |
| `DropTable.cs` (`DropEntry`/`DropResult`/`DropTableRoll`) | 2 |

## 왜 여기 있나 (Shared.Infrastructure 가 아니라)

의미상으로는 인프라/데이터 쪽이 맞지만, **`Shared.Infrastructure` 는 클라가 물리적으로 못 읽는다**:

```
Shared.Infrastructure   net10.0 · StackExchange.Redis · Logging   → Unity 불가, Plugins 미복사
        │ ProjectReference
        ▼
Shared.Gameplay         netstandard2.1 · 외부 의존 0              → Client/Assets/Plugins 로 복사
        ▲
        └── Unity Client 가 참조하는 유일한 DLL
```

즉 두 어셈블리의 실제 분할 기준은 "게임플레이 vs 인프라"가 아니라 **"클라가 볼 수 있는가"** 다.
이름이 그 기준을 말해주지 않아 읽는 사람이 매번 오해한다.

## 결함으로 등록됨

근본 원인(데이터 테이블의 클라용/서버용 미분리)과 분리 계획은
[cleanup-backlog.md](../../../../docs/wiki/cleanup-backlog.md) **A6** 참조.

**여기에 새 파일을 늘리지 말 것** — 늘릴수록 분리 비용이 커진다. 분리 결정 전까지는 현상 유지.
