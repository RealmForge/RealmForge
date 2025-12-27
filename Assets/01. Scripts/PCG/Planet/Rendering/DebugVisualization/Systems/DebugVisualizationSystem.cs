using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;

/// <summary>
/// NoiseDataBuffer를 기반으로 Cube 프리팹을 인스턴스화하여 노이즈를 시각화하는 시스템
/// </summary>
/// <remarks>
/// TODO: Config 시스템으로 전환 예정
/// - 실행 전 설정 파일 또는 ScriptableObject로 시각화 모드 선택
/// - OnStartRunning에서 config 읽어서 시스템 활성화/비활성화 결정
/// - 옵션: DebugVisualization (Cube) / MarchingCubes (Mesh)
///
/// 현재: NoiseVisualizationReady 플래그 사용 (MeshGenerationSystem은 MeshGenerationRequest 사용)
/// </remarks>
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
            int sampleSize = chunkSize + 1;  // NoiseData is (ChunkSize+1)^3
            int3 chunkPos = chunkData.ValueRO.ChunkPosition;
            float cubeSize = settings.CubeSize;
            int sampleSizeSq = sampleSize * sampleSize;

            for (int z = 0; z < chunkSize; z++)
            {
                int zOffset = z * sampleSizeSq;
                for (int y = 0; y < chunkSize; y++)
                {
                    int yOffset = y * sampleSize;
                    for (int x = 0; x < chunkSize; x++)
                    {
                        float value = buffer[x + yOffset + zOffset].Value;

                        // value > Threshold = solid (내부), value <= Threshold = air (외부)
                        // solid만 표시
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
