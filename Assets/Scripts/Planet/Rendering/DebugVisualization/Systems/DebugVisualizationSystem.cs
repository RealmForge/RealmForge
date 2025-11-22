// Planet/Rendering/Visualization/Systems/DebugVisualizationSystem.cs
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[RequireMatchingQueriesForUpdate]
public partial class DebugVisualizationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (request, buffer) in 
                 SystemAPI.Query<RefRO<NoiseGenerationRequest>, 
                     DynamicBuffer<NoiseDataBuffer>>())
        {
            int chunkSize = request.ValueRO.ChunkSize;
            int3 chunkPos = request.ValueRO.ChunkPosition;

            // 간단한 Gizmo 시각화 (Y=0 슬라이스만)
            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    int index = x + 0 * chunkSize + z * chunkSize * chunkSize;
                    float value = buffer[index].Value;

                    float3 worldPos = new float3(
                        chunkPos.x * chunkSize + x,
                        value * 10f, // 높이로 표현
                        chunkPos.z * chunkSize + z
                    );

                    Color color = Color.Lerp(Color.black, Color.white, value);
                    Debug.DrawLine(worldPos, worldPos + new float3(0, 0.5f, 0), color, 1f);
                }
            }
        }
    }
}