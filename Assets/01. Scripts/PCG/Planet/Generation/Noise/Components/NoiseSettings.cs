using Unity.Entities;
using Unity.Mathematics;

public struct NoiseSettings : IComponentData
{
    public float Scale;
    public int Octaves;
    public float Persistence;
    public float Lacunarity;
    public float HeightMultiplier;
    public float3 Offset;
    public int Seed;
}

public struct NoiseGenerationRequest : IComponentData, IEnableableComponent {}

public struct NoiseVisualizationReady : IComponentData, IEnableableComponent {}
