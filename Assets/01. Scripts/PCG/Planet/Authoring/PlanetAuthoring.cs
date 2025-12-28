using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PlanetAuthoring : MonoBehaviour
{
    [Header("Planet Settings")]
    public float3 center = float3.zero;
    public float radius = 50f;

    [Header("Surface Noise")]
    public float noiseScale = 50f;
    public int octaves = 6;
    [Range(0f, 1f)]
    public float persistence = 0.5f;
    public float lacunarity = 2f;
    public float heightMultiplier = 10f;
    public float3 offset = float3.zero;
    public int seed = 0;

    [Header("Cave")]
    public float caveScale = 30f;
    public int caveOctaves = 3;
    [Range(0f, 1f)]
    public float caveThreshold = 0.5f;
    public float caveStrength = 20f;
    public float caveMaxDepth = 30f;

    [Header("Chunk Settings")]
    public int chunkSize = 16;

    class Baker : Baker<PlanetAuthoring>
    {
        public override void Bake(PlanetAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new PlanetData
            {
                Center = authoring.center,
                Radius = authoring.radius
            });

            AddComponent(entity, new NoiseSettings
            {
                Scale = authoring.noiseScale,
                Octaves = authoring.octaves,
                Persistence = authoring.persistence,
                Lacunarity = authoring.lacunarity,
                HeightMultiplier = authoring.heightMultiplier,
                Offset = authoring.offset,
                Seed = authoring.seed,

                CaveScale = authoring.caveScale,
                CaveOctaves = authoring.caveOctaves,
                CaveThreshold = authoring.caveThreshold,
                CaveStrength = authoring.caveStrength,
                CaveMaxDepth = authoring.caveMaxDepth
            });

            AddComponent(entity, new PlanetChunkSettings
            {
                ChunkSize = authoring.chunkSize
            });

            AddComponent<PlanetTag>(entity);
        }
    }
}

public struct PlanetTag : IComponentData { }

public struct PlanetChunkSettings : IComponentData
{
    public int ChunkSize;
}
