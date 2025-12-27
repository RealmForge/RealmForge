using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

/// <summary>
/// Planet 노이즈 생성 시스템.
/// ★ 옥트리 기반: ChunkData.Min, ChunkData.Size 사용
/// </summary>
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
            var jobResult = m_NoiseJobResults[i];
            if (jobResult.NoiseValues.IsCreated)
                jobResult.NoiseValues.Dispose();
            if (jobResult.NoiseLayers.IsCreated)
                jobResult.NoiseLayers.Dispose();
        }
        m_NoiseJobResults.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var newJobs = new NativeList<NoiseJobResult>(Allocator.Temp);

        foreach (var (chunkData, planetData, layerBuffer, entity) in
            SystemAPI.Query<RefRO<ChunkData>, RefRO<PlanetData>, DynamicBuffer<NoiseLayerBuffer>>()
                .WithAll<NoiseGenerationRequest>()
                .WithEntityAccess())
        {
            int chunkSize = chunkData.ValueRO.ChunkSize;
            int sampleSize = chunkSize + 1;
            int totalSize = sampleSize * sampleSize * sampleSize;

            var noiseValues = new NativeArray<float>(totalSize, Allocator.TempJob);

            int layerCount = layerBuffer.Length;
            var noiseLayers = new NativeArray<NoiseLayerData>(layerCount, Allocator.TempJob);
            for (int i = 0; i < layerCount; i++)
            {
                var layer = layerBuffer[i];
                noiseLayers[i] = new NoiseLayerData
                {
                    LayerType = layer.LayerType,
                    BlendMode = layer.BlendMode,
                    Scale = layer.Scale,
                    Octaves = layer.Octaves,
                    Persistence = layer.Persistence,
                    Lacunarity = layer.Lacunarity,
                    Strength = layer.Strength,
                    Offset = layer.Offset,
                    UseFirstLayerAsMask = layer.UseFirstLayerAsMask
                };
            }

            // ★ 옥트리 기반 VoxelSize 계산
            float voxelSize = chunkData.ValueRO.Size / chunkSize;

            var job = new PlanetNoiseJob
            {
                ChunkSize = chunkSize,
                SampleSize = sampleSize,
                
                // ★ 변경: 옥트리 월드 좌표
                ChunkMin = chunkData.ValueRO.Min,
                VoxelSize = voxelSize,
                
                PlanetCenter = planetData.ValueRO.Center,
                PlanetRadius = planetData.ValueRO.Radius,
                CoreRadius = planetData.ValueRO.CoreRadius,
                NoiseLayers = noiseLayers,
                LayerCount = layerCount,
                Seed = 0,
                NoiseValues = noiseValues
            };

            var jobHandle = job.Schedule(totalSize, 64);

            newJobs.Add(new NoiseJobResult
            {
                JobHandle = jobHandle,
                Entity = entity,
                NoiseValues = noiseValues,
                NoiseLayers = noiseLayers
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
    public NativeArray<NoiseLayerData> NoiseLayers;
}