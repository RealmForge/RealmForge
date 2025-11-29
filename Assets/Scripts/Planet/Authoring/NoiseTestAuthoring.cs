// Hybrid/Authoring/NoiseTestAuthoring.cs
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class NoiseTestAuthoring : MonoBehaviour
{
    public float scale = 50f;
    public int octaves = 4;
    public float persistence = 0.5f;
    public float lacunarity = 2f;
    public int seed = 0;
    public int chunkSize = 16;
    public int3 chunkGridSize = new int3(1, 1, 1); // 생성할 청크 그리드 크기

    class Baker : Baker<NoiseTestAuthoring>
    {
        public override void Bake(NoiseTestAuthoring authoring)
        {
            // 3x3x3 그리드로 총 27개의 청크 엔티티 생성
            for (int x = 0; x < authoring.chunkGridSize.x; x++)
            {
                for (int y = 0; y < authoring.chunkGridSize.y; y++)
                {
                    for (int z = 0; z < authoring.chunkGridSize.z; z++)
                    {
                        // 각 청크마다 새로운 엔티티 생성
                        var entity = CreateAdditionalEntity(TransformUsageFlags.None);

                        AddComponent(entity, new NoiseSettings
                        {
                            Scale = authoring.scale,
                            Octaves = authoring.octaves,
                            Persistence = authoring.persistence,
                            Lacunarity = authoring.lacunarity,
                            Offset = float3.zero,
                            Seed = authoring.seed
                        });

                        AddComponent(entity, new NoiseGenerationRequest
                        {
                            ChunkPosition = new int3(x, y, z),
                            ChunkSize = authoring.chunkSize
                        });
                        // 기본값: Enabled (즉시 생성 시작)

                        AddComponent(entity, new NoiseVisualizationReady());
                        SetComponentEnabled<NoiseVisualizationReady>(entity, false);
                        // 기본값: Disabled (아직 렌더링 준비 안됨)

                        AddBuffer<NoiseDataBuffer>(entity);
                    }
                }
            }
        }
    }
}