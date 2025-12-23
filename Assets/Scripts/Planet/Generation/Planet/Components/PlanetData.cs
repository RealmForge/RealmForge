using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Planet basic configuration data
/// </summary>
public struct PlanetData : IComponentData
{
    public float3 Center;       // Planet center position
    public float Radius;        // Planet base radius
    public float NoiseStrength; // Overall noise strength multiplier
}

/// <summary>
/// Planet noise generation request flag
/// </summary>
public struct PlanetNoiseGenerationRequest : IComponentData, IEnableableComponent { }
