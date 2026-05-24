# Motion Matching V2 설계 문서

> 마지막 업데이트: 2026-05-16  
> 작성 기준: UE5.7 Pose Search 플러그인 분석 + V1 코드베이스 리뷰

---

## 배경 — V1 무엇이 문제였나

V1(`Client/Assets/Script/System/MotionSystem/`)은 80개 파일, 약 1,400줄짜리 `MotionMatching.cs`로 구성된 동작하는 구현체다.  
하지만 다음 구조적 문제로 전면 재설계를 결정했다.

| 문제 | 세부 내용 |
|------|-----------|
| **Schema/Database 미분리** | `Dataset` 하나가 "어떻게 쿼리할지(규칙)"와 "어떤 데이터를 쓸지(데이터)" 역할을 동시에 담당 |
| **채널 타입 미분리** | `weightBonesPosition`, `weightFutureRootDirection` 등 가중치가 MotionMatching.cs에 평탄하게 필드로 박혀있음 |
| **런타임 Database 교체 불가** | 무기 교체/상태 전환 시 Dataset 교체 구조가 없어 전투/이동 애니메이션 분리가 어려움 |
| **검색 전략 고정** | Brute Force 전수 탐색만 존재. DB 규모가 커질수록 성능 한계 |
| **QueryFlow 타입이 하드코딩** | `MotionQueryComputedFlow`, `ActionQueryComputedFlow` 등 타입 분기가 `MotionMatching.cs` 내부에 직접 박혀있음 |

---

## V2 목표

1. **UE5 Pose Search 아키텍처 참조** — Schema/Database/Channel 3계층 분리
2. **KD-Tree 기반 검색** — PCA 차원 축소 + KDTree, 대규모 DB 대응
3. **채널 타입별 분리** — `IPoseSearchChannel` 인터페이스 기반, 채널 추가가 코드 수정 없이 가능
4. **런타임 Database 스왑** — 무기 교체, 상태 전환 시 Schema 유지하고 Database만 교체
5. **NativeArray/JobSystem 유지** — V1의 성능 기반은 계승

---

## 아키텍처 개요

```
[PoseSearchSchema] ← ScriptableObject
  - 채널 목록 (TrajectoryChannel, PoseChannel, VelocityChannel...)
  - SampleRate, NormalizationMode
  - 채널별 Weight
  
[PoseSearchDatabase] ← ScriptableObject
  - Schema 참조
  - AnimationClip 목록 + CostBias per entry
  - 빌드 타임에 FeatureVector NativeArray로 Bake됨
  
[MotionMatchingComponent] ← MonoBehaviour
  - Database 참조 (런타임 교체 가능)
  - 매 프레임 PoseQuery 빌드
  - ISearchStrategy로 검색 위임
  - PoseBlender로 블렌드
```

### 런타임 플로우

```
FixedUpdate
  │
  ├─ TrajectoryComponent.Update()      ← 궤적 샘플 갱신
  │
  ├─ PoseQuery 빌드                    ← 각 Channel.BuildQuery() 호출
  │    └─ FeatureVector (float[])
  │
  ├─ ISearchStrategy.Search()          ← KDTreeSearch or BruteForce
  │    └─ 최저 Cost 포즈 인덱스
  │
  ├─ CostBias 적용                     ← ContinuingPose, Loop, Notify Override
  │
  └─ PoseBlender.Apply()               ← Inertialization 블렌드
```

---

## 폴더 구조

```
Client/Assets/Script/System/
├── MotionSystem/           ← V1 (건드리지 않음, 레퍼런스)
└── MotionSystemV2/
    ├── Schema/
    │   ├── PoseSearchSchema.cs          ScriptableObject. 채널 목록 + SampleRate 정의
    │   ├── Channels/
    │   │   ├── IPoseSearchChannel.cs    채널 인터페이스
    │   │   ├── TrajectoryChannel.cs     궤적 샘플링 (과거/미래 trajectory)
    │   │   ├── PoseChannel.cs           본 위치/속도 (발 본 등)
    │   │   └── VelocityChannel.cs       본 속도 벡터
    │   └── NormalizationSet.cs          채널 간 정규화 그룹
    │
    ├── Database/
    │   ├── PoseSearchDatabase.cs        ScriptableObject. Schema 참조 + AnimClip 목록
    │   ├── PoseSearchEntry.cs           개별 AnimClip 항목 + CostBias
    │   ├── FeatureVector.cs             포즈 1개의 Feature 데이터 (NativeArray 호환)
    │   └── DatabaseBuilder.cs           에디터 타임 빌드 (Bake)
    │
    ├── Search/
    │   ├── ISearchStrategy.cs           검색 전략 인터페이스
    │   ├── BruteForceSearch.cs          Fallback / 소규모 DB용
    │   └── KDTreeSearch.cs              메인 검색 (KDTree + 선택적 PCA)
    │
    ├── Runtime/
    │   ├── MotionMatchingComponent.cs   MonoBehaviour 진입점
    │   ├── PoseQuery.cs                 런타임 쿼리 빌더 (채널별 BuildQuery 호출)
    │   ├── PoseSelector.cs              Cost 계산 + CostBias 적용 + 포즈 선택
    │   └── PoseBlender.cs               Inertialization 블렌드
    │
    └── Notifies/
        ├── BlockTransitionNotify.cs     해당 구간 직접 점프 금지
        └── CostBiasOverrideNotify.cs    구간별 보너스/패널티
```

---

## 채널 인터페이스 설계

채널은 **Feature 생성(빌드 타임)**과 **쿼리 생성(런타임)** 두 책임을 함께 갖는다.  
이 설계가 핵심 — 채널 추가 시 두 곳(빌드/쿼리)을 항상 함께 수정하게 된다.

```csharp
public interface IPoseSearchChannel
{
    // 이 채널이 Feature Vector에 기여하는 float 수
    int FeatureDimension { get; }

    // 에디터 타임: 애니메이션 프레임에서 Feature 추출 → output 버퍼에 씀
    void BuildFeature(FeatureBuildContext ctx, NativeSlice<float> output);

    // 런타임: 현재 캐릭터 상태에서 쿼리 Feature 추출 → output 버퍼에 씀
    void BuildQuery(QueryBuildContext ctx, NativeSlice<float> output);

    float Weight { get; }
    string DebugName { get; }
}
```

### 채널별 FeatureDimension

| 채널 | 기여 차원 수 | 내용 |
|------|------------|------|
| TrajectoryChannel | `sampleCount × 2` | position(x,z) × N 샘플 + direction(x,z) × N 샘플 |
| PoseChannel | `boneCount × 6` | 본당 position(x,y,z) + velocity(x,y,z) |
| VelocityChannel | `3` | 루트 속도 벡터(x,y,z) |

전체 Feature Vector 크기 = Σ(채널별 FeatureDimension)

---

## 검색 전략 — KDTree

### 구현 단계

**1단계 (현재 목표):** KDTree 직접 구축, full feature dimension 사용  
**2단계 (나중):** PCA 차원 축소 후 KDTree → UE5 PCAKDTree 방식

### KNN + Full Cost 패턴

```
KDTree.KNNSearch(query, k=8)
  └─ 후보 8개 반환 (근사값)

후보 8개에 대해 Full Cost 계산
  └─ 최저 Cost 포즈 선택
```

KDTree는 근사 거리만 빠르게 찾고, 최종 선택은 Full Cost 계산으로 정확도 보완.  
`KNNQueryNumNeighbors`가 UE5에서 이 역할이다.

---

## Cost 계산 구조

```
Total Cost
  = Σ(채널별 Cost × 채널 Weight)   ← 정규화 자동 적용
  + ContinuingPoseCostBias          ← 현재 포즈 지속 보너스/패널티 (-0.05 기본)
  + LoopingCostBias                 ← 루핑 애니메이션 선호도
  + NotifyCostBiasOverride          ← 구간별 보너스/패널티 (노티파이)
  + AnimationSwitchPenalty          ← 다른 클립으로 전환 시 패널티
```

**ContinuingPoseCostBias 튜닝 기준:**
- 음수 → 현재 포즈 오래 유지 (안정적, 반응성 낮음)
- 양수 → 더 자주 새 포즈 탐색 (반응적, 팝핑 가능)
- 기본값 -0.05 권장

---

## 챕터별 구현 계획

| 챕터 | 내용 | 상태 |
|------|------|------|
| **1** | Schema + 채널 인터페이스 (`IPoseSearchChannel`, `TrajectoryChannel`, `PoseChannel`) | 🚧 진행 중 |
| **2** | FeatureVector + DatabaseBuilder (Bake 파이프라인) | 📝 예정 |
| **3** | BruteForce 검색 + MotionMatchingComponent 기본 동작 확인 | 📝 예정 |
| **4** | KDTree 구현 + ISearchStrategy 교체 | 📝 예정 |
| **5** | Inertialization 블렌드 (V1 코드 이식) | 📝 예정 |
| **6** | Notify 시스템 (BlockTransition, CostBiasOverride) | 📝 예정 |
| **7** | 디버그 시각화 + 튜닝 파라미터 Inspector 노출 | 📝 예정 |

---

## V1에서 계승할 것

V1 코드에서 그대로 이식 가능한 부분:

| V1 코드 | V2 재사용 위치 |
|---------|---------------|
| `Inertialization.cs` | `PoseBlender.cs` — 로직 거의 그대로 |
| `NativeArray` 버퍼 초기화/Dispose 패턴 | `PoseSearchDatabase.cs`, `MotionMatchingComponent.cs` |
| `TransformAccessArray` + `IJobParallelForTransform` | `PoseBlender.cs` |
| `TrajectoryEstimation` | `TrajectoryChannel.BuildQuery()` 내부로 흡수 |
| `GlobalWeights` 패턴 | 채널별 `Weight` 필드로 대체 |

---

## 주요 결정 사항 (합의된 내용)

- **검색 알고리즘**: KDTree (1단계 full-dim, 2단계 PCA 추가)
- **채널 시스템**: UE5 방식 타입별 분리 (`IPoseSearchChannel`)
- **V1 폴더**: 삭제하지 않고 레퍼런스 유지 → V2 완성 후 제거
- **코드 제공 방식**: 직접 코드 작성, 가이드 기반 챕터별 진행
