using Unity.Entities;
using Unity.Mathematics;

public struct NoiseSettings : IComponentData
{
    // Surface
    public float Scale;
    public int Octaves;
    public float Persistence;
    public float Lacunarity;
    public float HeightMultiplier;
    public float3 Offset;
    public int Seed;

    // Cave
    public float CaveScale;
    public int CaveOctaves;
    public float CaveThreshold;
    public float CaveStrength;
    public float CaveMaxDepth;
}

public struct NoiseGenerationRequest : IComponentData, IEnableableComponent {}

public struct NoiseVisualizationReady : IComponentData, IEnableableComponent {}
