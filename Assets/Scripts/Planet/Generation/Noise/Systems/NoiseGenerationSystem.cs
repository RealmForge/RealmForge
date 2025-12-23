using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using RealmForge.Planet.Generation.Noise.Components;

[BurstCompile]
public partial struct NoiseGenerationSystem : ISystem
{
    // 노이즈 생성 요청이 들어온 엔티티만 처리해야 하므로,
    // NativeArray를 Job이 완료된 후 처리할 NativeList로 캐시할 필드를 추가합니다.
    public NativeList<PerlinJobResult> m_PerlinJobResults;

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
        state.Dependency.Complete();

        for (int i = 0; i < m_PerlinJobResults.Length; i++)
        {
            var jobResult = m_PerlinJobResults[i];

            if (jobResult.NoiseValues.IsCreated)
            {
                jobResult.NoiseValues.Dispose();
            }
        }
        m_PerlinJobResults.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var newJobs = new NativeList<PerlinJobResult>(Allocator.Temp);

        // ChunkData에서 청크 정보를 읽고, NoiseGenerationRequest 플래그가 활성화된 엔티티만 처리
        foreach (var (chunkData, noiseSettings, entity) in
            SystemAPI.Query<RefRO<ChunkData>, RefRO<NoiseSettings>>()
                .WithAll<NoiseGenerationRequest>()
                .WithEntityAccess())
        {
            int chunkSize = chunkData.ValueRO.ChunkSize;
            int sampleSize = chunkSize + 1;  // +1 for seamless chunk boundaries
            int totalSize = sampleSize * sampleSize * sampleSize;

            var noiseValues = new NativeArray<float>(totalSize, Allocator.TempJob);

            var job = new PerlinNoiseJob
            {
                ChunkSize = chunkSize,
                SampleSize = sampleSize,
                ChunkPosition = chunkData.ValueRO.ChunkPosition,
                Scale = noiseSettings.ValueRO.Scale,
                Octaves = noiseSettings.ValueRO.Octaves,
                Persistence = noiseSettings.ValueRO.Persistence,
                Lacunarity = noiseSettings.ValueRO.Lacunarity,
                Offset = noiseSettings.ValueRO.Offset,
                Seed = noiseSettings.ValueRO.Seed,
                NoiseValues = noiseValues
            };

            var jobHandle = job.Schedule(totalSize, 64);

            newJobs.Add(new PerlinJobResult
            {
                JobHandle = jobHandle,
                Entity = entity,
                NoiseValues = noiseValues
            });

            // 요청 플래그 비활성화 (중복 스케줄링 방지)
            ecb.SetComponentEnabled<NoiseGenerationRequest>(entity, false);
        }

        // 종속성 연결 및 캐시 업데이트
        if (newJobs.Length > 0)
        {
            var jobHandles = new NativeArray<JobHandle>(newJobs.Length, Allocator.Temp);
            for (int i = 0; i < newJobs.Length; i++)
            {
                jobHandles[i] = newJobs[i].JobHandle;
            }

            JobHandle combinedHandle = JobHandle.CombineDependencies(jobHandles);
            state.Dependency = combinedHandle;
            m_PerlinJobResults.AddRange(newJobs);

            jobHandles.Dispose();
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        newJobs.Dispose();
    }
}

// JobHandle, NativeArray, Entity를 묶어 Job 완료 후 처리를 위한 정보를 담는 구조체
namespace RealmForge.Planet.Generation.Noise.Components
{
    public struct PerlinJobResult
    {
        public JobHandle JobHandle;
        public Entity Entity;
        public NativeArray<float> NoiseValues;
    }
}
