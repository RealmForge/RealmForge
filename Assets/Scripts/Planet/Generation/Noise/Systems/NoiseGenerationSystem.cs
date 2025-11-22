using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

[BurstCompile]
public partial struct NoiseGenerationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NoiseSettings>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 처리되지 않은 노이즈 생성 요청 처리
        foreach (var (request, noiseSettings, entity) in
            SystemAPI.Query<RefRO<NoiseGenerationRequest>, RefRO<NoiseSettings>>()
            .WithAll<NoiseVisualizationReady>()
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
            jobHandle.Complete();

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
            
            // 렌더링 완료 표시
            ecb.SetComponentEnabled<NoiseVisualizationReady>(entity, true);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}