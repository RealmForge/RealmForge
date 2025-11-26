using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

[BurstCompile]
public partial struct NoiseGenerationSystem : ISystem
{
    
    // 노이즈 생성 요청이 들어온 엔티티만 처리해야 하므로,
    // NativeArray를 Job이 완료된 후 처리할 NativeList로 캐시할 필드를 추가합니다.
    private NativeList<PerlinJobResult> m_PerlinJobResults;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NoiseSettings>();
        m_PerlinJobResults = new NativeList<PerlinJobResult>(Allocator.Persistent);
    }
    
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        // 시스템이 파괴될 때, 남아있는 모든 NativeList를 안전하게 해제합니다.
        // Job이 완료되지 않았을 수 있으므로 Dependency를 Complete하고 Dispose합니다.
        // 할당 시킨 스레드의 작업이 끝날때까지 메인스레드를 대기시킵니다.
        state.Dependency.Complete();
        
        for (int i = 0; i < m_PerlinJobResults.Length; i++)
        {
            // NativeList의 요소를 직접 참조하여 Dispose를 호출합니다.
            var jobResult = m_PerlinJobResults[i];
        
            if (jobResult.NoiseValues.IsCreated)
            {
                // Dispose 호출은 NativeArray의 상태를 변경하므로,
                // for 루프를 사용해 NativeList 내부에 직접 접근해야 안전합니다.
                jobResult.NoiseValues.Dispose();
            }
        }
        m_PerlinJobResults.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var newJobs = new NativeList<PerlinJobResult>(Allocator.Temp); //새로 생성된 job을 메인 시스템으로 전달하기 위한 캐시 역할 수행
        
        // 처리되지 않은 노이즈 생성 요청 처리
        foreach (var (request, noiseSettings, entity) in
            SystemAPI.Query<RefRO<NoiseGenerationRequest>, RefRO<NoiseSettings>>()
            .WithAll<NoiseGenerationRequest>()// 작업 시작을 나타내기에 request로 바꿨습니다. (중복 작업 방지)
            .WithEntityAccess())
        {

            int totalSize = request.ValueRO.ChunkSize * 
                            request.ValueRO.ChunkSize * 
                            request.ValueRO.ChunkSize;

            var noiseValues = new NativeArray<float>(totalSize, Allocator.TempJob);

            var job = new PerlinNoiseJob
            {
                ChunkSize = request.ValueRO.ChunkSize,
                ChunkPosition = request.ValueRO.ChunkPosition,
                Scale = noiseSettings.ValueRO.Scale,
                Octaves = noiseSettings.ValueRO.Octaves,
                Persistence = noiseSettings.ValueRO.Persistence,
                Lacunarity = noiseSettings.ValueRO.Lacunarity,
                Offset = noiseSettings.ValueRO.Offset,
                Seed = noiseSettings.ValueRO.Seed,
                NoiseValues = noiseValues
            };

            var jobHandle = job.Schedule(totalSize, 64);
            //jobHandle.Complete(); 메인스레드 블로킹 방지
            
            newJobs.Add(new PerlinJobResult
            {
                JobHandle = jobHandle,
                Entity = entity,
                NoiseValues = noiseValues
            });
            
            // 요청 엔티티 비활성화 (중복 스케줄링 방지)
            ecb.SetComponentEnabled<NoiseGenerationRequest>(entity, false);
            
            
            /*
            // Buffer에 데이터 저장
            var buffer = state.EntityManager.GetBuffer<NoiseDataBuffer>(entity);
            buffer.Clear();
            buffer.Capacity = totalSize;

            for (int i = 0; i < totalSize; i++)
            {
                buffer.Add(new NoiseDataBuffer { Value = noiseValues[i] });
            }

            noiseValues.Dispose();

            // 처리 완료 표시
            ecb.SetComponentEnabled<NoiseGenerationRequest>(entity, false);
            */
            
            // 렌더링 완료 표시
            //ecb.SetComponentEnabled<NoiseVisualizationReady>(entity, true);
            
        }
        // 종속성 연결 및 캐시 업데이트
        if (newJobs.Length > 0)
        {
            JobHandle combinedHandle = JobHandle.CombineDependencies(newJobs.AsArray().Reinterpret<JobHandle>());
            state.Dependency = combinedHandle;
            m_PerlinJobResults.AddRange(newJobs);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        newJobs.Dispose();
    }
}

// JobHandle, NativeArray, Entity를 묶어 Job 완료 후 처리를 위한 정보를 담는 구조체
public struct PerlinJobResult
{
    public JobHandle JobHandle;
    public Entity Entity;
    public NativeArray<float> NoiseValues;
}