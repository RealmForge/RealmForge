using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;

/// <summary>
/// NoiseDataBuffer를 기반으로 Cube 프리팹을 인스턴스화하여 노이즈를 시각화하는 시스템
/// </summary>
[BurstCompile]
public partial struct DebugVisualizationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DebugVisualizationSettings>();
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var settings = SystemAPI.GetSingleton<DebugVisualizationSettings>();
        if (settings.CubePrefab == Entity.Null) return;

        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (chunkData, buffer, entity) in
                 SystemAPI.Query<RefRO<ChunkData>, DynamicBuffer<NoiseDataBuffer>>()
                     .WithAll<NoiseVisualizationReady>()
                     .WithEntityAccess())
        {
            int chunkSize = chunkData.ValueRO.ChunkSize;
            int3 chunkPos = chunkData.ValueRO.ChunkPosition;
            float cubeSize = settings.CubeSize;
            int chunkSizeSq = chunkSize * chunkSize;

            for (int z = 0; z < chunkSize; z++)
            {
                int zOffset = z * chunkSizeSq;
                for (int y = 0; y < chunkSize; y++)
                {
                    int yOffset = y * chunkSize;
                    for (int x = 0; x < chunkSize; x++)
                    {
                        float value = buffer[x + yOffset + zOffset].Value;

                        if (settings.UseThreshold && value <= settings.Threshold)
                            continue;

                        float3 worldPos = new float3(
                            chunkPos.x * chunkSize + x,
                            chunkPos.y * chunkSize + y,
                            chunkPos.z * chunkSize + z
                        ) * cubeSize;

                        var cubeEntity = ecb.Instantiate(settings.CubePrefab);

                        ecb.SetComponent(cubeEntity, LocalTransform.FromPositionRotationScale(
                            worldPos, quaternion.identity, cubeSize));

                        ecb.AddComponent(cubeEntity, new URPMaterialPropertyBaseColor
                        {
                            Value = new float4(value, value, value, 1f)
                        });
                    }
                }
            }

            ecb.SetComponentEnabled<NoiseVisualizationReady>(entity, false);
            ecb.AddComponent<DebugVisualizationCompleted>(entity);
        }
    }
}
