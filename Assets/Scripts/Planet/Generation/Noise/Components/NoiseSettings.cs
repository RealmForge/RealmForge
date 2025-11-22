using Unity.Entities;
using Unity.Mathematics;

public struct NoiseSettings : IComponentData
{
    public float Scale;
    public int Octaves;
    public float Persistence;
    public float Lacunarity;
    public float3 Offset;
    public int Seed;
}

public struct NoiseGenerationRequest : IComponentData, IEnableableComponent
{
    public int3 ChunkPosition;
    public int ChunkSize;
}

public struct NoiseVisualizationReady : IComponentData, IEnableableComponent {}