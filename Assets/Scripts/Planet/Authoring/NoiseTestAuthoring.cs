using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class NoiseTestAuthoring : MonoBehaviour
{
    [Header("Noise Settings")]
    public float scale = 50f;
    public int octaves = 4;
    public float persistence = 0.5f;
    public float lacunarity = 2f;
    public float3 offset = float3.zero;
    public int seed = 0;

    [Header("Chunk Settings")]
    public int chunkSize = 16;
    public int3 chunkGridSize = new int3(1, 1, 1); // 생성할 청크 그리드 크기

    class Baker : Baker<NoiseTestAuthoring>
    {
        public override void Bake(NoiseTestAuthoring authoring)
        {
            for (int x = 0; x < authoring.chunkGridSize.x; x++)
            {
                for (int y = 0; y < authoring.chunkGridSize.y; y++)
                {
                    for (int z = 0; z < authoring.chunkGridSize.z; z++)
                    {
                        var entity = CreateAdditionalEntity(TransformUsageFlags.None);

                        // 청크 데이터
                        AddComponent(entity, new ChunkData
                        {
                            ChunkPosition = new int3(x, y, z),
                            ChunkSize = authoring.chunkSize
                        });

                        // 노이즈 설정
                        AddComponent(entity, new NoiseSettings
                        {
                            Scale = authoring.scale,
                            Octaves = authoring.octaves,
                            Persistence = authoring.persistence,
                            Lacunarity = authoring.lacunarity,
                            Offset = authoring.offset,
                            Seed = authoring.seed
                        });

                        // 노이즈 생성 요청 플래그 (Enabled = 즉시 생성 시작)
                        AddComponent(entity, new NoiseGenerationRequest());

                        // 시각화 준비 플래그 (Disabled = 아직 준비 안됨)
                        AddComponent(entity, new NoiseVisualizationReady());
                        SetComponentEnabled<NoiseVisualizationReady>(entity, false);

                        // 노이즈 데이터 버퍼
                        AddBuffer<NoiseDataBuffer>(entity);
                    }
                }
            }
        }
    }
}
