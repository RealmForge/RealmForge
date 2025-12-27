# Marching Cubes Mesh Generation System

## Overview

DOTS 기반 비동기 Marching Cubes 메쉬 생성 시스템.
NoiseData를 입력받아 Burst Job으로 vertices/normals/indices를 생성하고,
완료 후 Unity Mesh로 변환하여 Entity에 렌더링 컴포넌트를 추가한다.

## Architecture

```
NoiseVisualizationReady (flag)
        │
        ▼
┌─────────────────────────┐
│  MeshGenerationSystem   │  [ISystem, BurstCompile]
│  - MarchingCubesJob     │  - NoiseDataBuffer 읽기
│    스케줄링             │  - Job 비동기 실행
│  - MeshJobResults 저장  │  - EdgeTable/TriTable (NativeArray)
└─────────────────────────┘
        │
        ▼ (Job 완료 대기)
┌─────────────────────────┐
│    MeshApplySystem      │  [SystemBase]
│  - Job 완료 확인        │  - Mesh.MeshDataArray 사용
│  - Unity Mesh 생성      │  - 메인 스레드에서 실행
│  - 렌더링 컴포넌트 추가 │  - ChunkData 위치 적용
└─────────────────────────┘
```

## File Structure

```
Assets/Scripts/Planet/Rendering/MarchingCubes/
├── Components/
│   ├── MeshDataBuffer.cs        # MeshVertexBuffer, MeshNormalBuffer, MeshIndexBuffer
│   └── MeshGenerationTags.cs    # MeshGenerationRequest, MeshApplyRequest
├── Data/
│   └── MarchingCubesTables.cs   # EdgeTable (256), TriTable (256x16)
├── Jobs/
│   └── MarchingCubesJob.cs      # IJob, BurstCompile
└── Systems/
    ├── MeshGenerationSystem.cs  # ISystem - Job 스케줄링
    └── MeshApplySystem.cs       # SystemBase - Mesh 생성/적용
```

## Data Flow

```
1. NoiseGenerationSystem
   └─ PerlinNoiseJob (비동기)

2. NoiseDataCopySystem
   └─ NoiseDataBuffer에 복사
   └─ NoiseVisualizationReady 활성화

3. MeshGenerationSystem
   └─ NoiseDataBuffer → NativeArray<float> 복사
   └─ MarchingCubesJob 스케줄링
   └─ MeshJobResults에 저장
   └─ NoiseVisualizationReady 비활성화

4. MeshApplySystem
   └─ Job 완료 확인
   └─ Mesh.AllocateWritableMeshData()
   └─ vertices/normals/indices 복사
   └─ Mesh.ApplyAndDisposeWritableMeshData()
   └─ RenderMeshUtility.AddComponents()
```

## Key Components

### MarchingCubesJob

```csharp
[BurstCompile]
public struct MarchingCubesJob : IJob
{
    [ReadOnly] public NativeArray<float> NoiseData;
    [ReadOnly] public NativeArray<int> EdgeTable;
    [ReadOnly] public NativeArray<int> TriTable;
    public int ChunkSize;
    public float Threshold;     // 표면 threshold (default: 0.5)
    public float VoxelSize;     // voxel 크기 (default: 1.0)

    public NativeList<float3> Vertices;
    public NativeList<float3> Normals;
    public NativeList<int> Indices;
}
```

### MeshJobResult

```csharp
public struct MeshJobResult
{
    public JobHandle JobHandle;
    public Entity Entity;
    public NativeArray<float> NoiseData;
    public NativeList<float3> Vertices;
    public NativeList<float3> Normals;
    public NativeList<int> Indices;
}
```

## Configuration

### Threshold
- 0.0 ~ 1.0 범위
- 값이 낮을수록 더 많은 표면 생성
- 현재 기본값: 0.5

### VoxelSize
- 각 voxel의 월드 단위 크기
- 현재 기본값: 1.0

## Chunk Boundary Solution

### SampleSize 개념
청크 간 연결을 위해 NoiseData는 `(ChunkSize+1)^3` 크기로 생성됨.

```
ChunkSize = 16
SampleSize = 17 (ChunkSize + 1)

NoiseData: 17x17x17 = 4,913개
Cubes: 16x16x16 = 4,096개

인접 청크와 경계 1줄 공유:
Chunk(0,0,0)의 x=16 == Chunk(1,0,0)의 x=0 (Perlin noise deterministic)
```

### 수정된 파일
- `PerlinNoiseJob.cs`: SampleSize 기준 인덱싱
- `NoiseGenerationSystem.cs`: totalSize = sampleSize^3
- `MarchingCubesJob.cs`: SampleSize 기준 NoiseData 접근
- `MeshGenerationSystem.cs`: SampleSize를 Job에 전달

## Known Issues / TODO

### 1. 프레임당 처리 제한 (Planned)
- 현재: 완료된 모든 Job을 한 프레임에 처리 → 다수 청크 동시 완료 시 프레임 드랍 가능
- 개선: 프레임당 최대 N개 청크만 Mesh 생성
- 추가 고려: 우선순위 큐 (플레이어 근처 청크 우선), 시간 기반 제한 (프레임당 X ms)

### 2. Config 시스템 (Planned)
- DebugVisualization / MarchingCubes 선택적 활성화
- ScriptableObject 또는 설정 파일 기반
- OnStartRunning에서 시스템 활성화/비활성화

### 3. LOD 시스템 (Planned)
- 현재: VoxelSize = 1.0f 하드코딩
- 개선: ChunkData 또는 별도 컴포넌트에서 Scale 정보 가져오기
- LOD 레벨별 VoxelSize: 1.0f, 2.0f, 4.0f, 8.0f (2^n)
- 데이터 개수는 동일 (17x17x17), Scale만 변경

### 4. Threshold 설정 (Planned)
- 현재: Threshold = 0.5f 하드코딩
- 개선: NoiseSettings 또는 별도 컴포넌트에서 설정 가능하게
- 값이 낮을수록 더 많은 표면 생성

### 5. Material 설정
- 현재 기본 Lit Material 사용
- Authoring을 통한 Material 지정 기능 필요

### 6. 다중 Noise 합성 및 행성 형태 (Planned)
- 여러 종류의 Noise 생성 및 합성 (Perlin, Simplex, Ridged 등)
- 구형(Sphere) SDF를 기반으로 행성 형태 생성
- 최종 데이터 = Sphere SDF + 다중 Noise 합성
- Noise 레이어별 가중치, 스케일, 옥타브 설정

### 7. Weight 데이터 처리 방식 (Discussion Needed)
현재: 렌더링 시점에 threshold 비교
제안: ChunkData 생성 시 0/1 이진 데이터로 변환

**Option A: 원본 Weight 유지 (현재)**
- 장점: 보간 가능, Dual Marching Cubes 적용 가능, 부드러운 표면
- 단점: 메모리 사용량 (float per voxel)

**Option B: 0/1 이진 데이터 변환**
- 장점: 메모리 절약 (bit per voxel 가능), 단순한 처리
- 단점: 보간 불가, 계단현상 발생 가능

**결정 필요**:
- Dual Marching Cubes 적용 예정인지?
- LOD에서 부드러운 전환이 필요한지?
- 메모리 최적화가 우선인지?

### 8. 폴더 구조 재검토 (Planned)
- 현재: `Planet/Generation/Planet/` (중복)
- 검토 필요: 상위 폴더 구조 정리 (Planet → World 또는 Terrain 등)

## Related Systems

- `NoiseGenerationSystem`: Perlin 노이즈 생성
- `NoiseDataCopySystem`: Job 결과를 Buffer에 복사
- `DebugVisualizationSystem`: Cube 프리팹 시각화 (현재 비활성화)

## Performance Notes

- MarchingCubesJob: Burst 컴파일됨
- Lookup Tables: System OnCreate에서 NativeArray로 한 번만 할당
- Mesh 생성: 메인 스레드 필수 (Unity API 제약)
