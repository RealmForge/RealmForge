// Hybrid/Authoring/NoiseTestAuthoring.cs
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

    class Baker : Baker<NoiseTestAuthoring>
    {
        public override void Bake(NoiseTestAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

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
                ChunkPosition = int3.zero,
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