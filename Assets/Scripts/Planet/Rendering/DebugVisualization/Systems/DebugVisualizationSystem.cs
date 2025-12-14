using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;

/// <summary>
/// NoiseDataBuffer를 기반으로 Cube 프리팹을 인스턴스화하여 노이즈를 시각화하는 시스템
/// </summary>
[RequireMatchingQueriesForUpdate]
public partial class DebugVisualizationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // DebugVisualizationSettings 싱글톤 확인
        if (!SystemAPI.HasSingleton<DebugVisualizationSettings>())
            return;

        var settings = SystemAPI.GetSingleton<DebugVisualizationSettings>();

        if (settings.CubePrefab == Entity.Null)
            return;

        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        // NoiseVisualizationReady가 활성화된 엔티티만 처리 (ChunkData에서 청크 정보 읽기)
        foreach (var (chunkData, buffer, entity) in
                 SystemAPI.Query<RefRO<ChunkData>, DynamicBuffer<NoiseDataBuffer>>()
                     .WithAll<NoiseVisualizationReady>()
                     .WithEntityAccess())
        {
            
            var instance = ecb.Instantiate(settings.CubePrefab);
            ecb.SetComponent(instance, LocalTransform.FromPosition(
                new float3(0, 0, 0)
            ));
            
            int chunkSize = chunkData.ValueRO.ChunkSize;
            int3 chunkPos = chunkData.ValueRO.ChunkPosition;
            float cubeSize = settings.CubeSize;

            // 3D 순회: 모든 정점에 대해 큐브 생성
            for (int z = 0; z < chunkSize; z++)
            {
                for (int y = 0; y < chunkSize; y++)
                {
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int index = x + y * chunkSize + z * chunkSize * chunkSize;
                        float value = buffer[index].Value;

                        // Threshold 체크
                        if (settings.UseThreshold && value <= settings.Threshold)
                            continue;

                        // 월드 좌표 계산
                        float3 worldPos = new float3(
                            (chunkPos.x * chunkSize + x) * cubeSize,
                            (chunkPos.y * chunkSize + y) * cubeSize,
                            (chunkPos.z * chunkSize + z) * cubeSize
                        );

                        // Cube 엔티티 인스턴스화
                        var cubeEntity = ecb.Instantiate(settings.CubePrefab);

                        // Transform 설정
                        ecb.SetComponent(cubeEntity, new LocalTransform
                        {
                            Position = worldPos,
                            Rotation = quaternion.identity,
                            Scale = cubeSize
                        });

                        // Grayscale 색상 설정 (URPMaterialPropertyBaseColor)
                        ecb.AddComponent(cubeEntity, new URPMaterialPropertyBaseColor
                        {
                            Value = new float4(value, value, value, 1f)
                        });
                    }
                }
            }

            // 처리 완료 표시: NoiseVisualizationReady 비활성화
            ecb.SetComponentEnabled<NoiseVisualizationReady>(entity, false);

            // 완료 태그 추가
            ecb.AddComponent<DebugVisualizationCompleted>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
