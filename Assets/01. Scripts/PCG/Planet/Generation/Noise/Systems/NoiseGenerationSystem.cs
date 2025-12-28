using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

[BurstCompile]
public partial struct NoiseGenerationSystem : ISystem
{
    public NativeList<NoiseJobResult> m_NoiseJobResults;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlanetData>();
        m_NoiseJobResults = new NativeList<NoiseJobResult>(Allocator.Persistent);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        state.Dependency.Complete();
        for (int i = 0; i < m_NoiseJobResults.Length; i++)
        {
            if (m_NoiseJobResults[i].NoiseValues.IsCreated)
                m_NoiseJobResults[i].NoiseValues.Dispose();
        }
        m_NoiseJobResults.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var newJobs = new NativeList<NoiseJobResult>(Allocator.Temp);

        foreach (var (chunkData, planetData, noiseSettings, entity) in
            SystemAPI.Query<RefRO<ChunkData>, RefRO<PlanetData>, RefRO<NoiseSettings>>()
                .WithAll<NoiseGenerationRequest>()
                .WithEntityAccess())
        {
            int chunkSize = chunkData.ValueRO.ChunkSize;
            int sampleSize = chunkSize + 1;
            int totalSize = sampleSize * sampleSize * sampleSize;

            var noiseValues = new NativeArray<float>(totalSize, Allocator.TempJob);
            float voxelSize = chunkData.ValueRO.Size / chunkSize;

            var job = new PlanetNoiseJob
            {
                ChunkSize = chunkSize,
                SampleSize = sampleSize,
                ChunkMin = chunkData.ValueRO.Min,
                VoxelSize = voxelSize,

                PlanetCenter = planetData.ValueRO.Center,
                PlanetRadius = planetData.ValueRO.Radius,

                NoiseScale = noiseSettings.ValueRO.Scale,
                Octaves = noiseSettings.ValueRO.Octaves,
                Persistence = noiseSettings.ValueRO.Persistence,
                Lacunarity = noiseSettings.ValueRO.Lacunarity,
                HeightMultiplier = noiseSettings.ValueRO.HeightMultiplier,
                Offset = noiseSettings.ValueRO.Offset,
                Seed = noiseSettings.ValueRO.Seed,

                NoiseValues = noiseValues
            };

            var jobHandle = job.Schedule(totalSize, 64);

            newJobs.Add(new NoiseJobResult
            {
                JobHandle = jobHandle,
                Entity = entity,
                NoiseValues = noiseValues
            });

            ecb.SetComponentEnabled<NoiseGenerationRequest>(entity, false);
        }

        if (newJobs.Length > 0)
        {
            var jobHandles = new NativeArray<JobHandle>(newJobs.Length, Allocator.Temp);
            for (int i = 0; i < newJobs.Length; i++)
            {
                jobHandles[i] = newJobs[i].JobHandle;
            }
            state.Dependency = JobHandle.CombineDependencies(jobHandles);
            m_NoiseJobResults.AddRange(newJobs.AsArray());
            jobHandles.Dispose();
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        newJobs.Dispose();
    }
}

public struct NoiseJobResult
{
    public JobHandle JobHandle;
    public Entity Entity;
    public NativeArray<float> NoiseValues;
}
