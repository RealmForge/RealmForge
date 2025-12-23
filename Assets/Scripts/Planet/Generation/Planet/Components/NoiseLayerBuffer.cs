using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Single noise layer configuration for planet surface generation.
/// Currently supports Perlin noise only. Other types (Simplex, Ridged) planned for future.
/// </summary>
public struct NoiseLayerBuffer : IBufferElementData
{
    public float Scale;             // Noise scale (frequency)
    public int Octaves;             // Number of octaves
    public float Persistence;       // Amplitude decrease per octave
    public float Lacunarity;        // Frequency increase per octave
    public float Strength;          // Layer weight/strength
    public float3 Offset;           // Noise offset
    public bool UseFirstLayerAsMask; // Use first layer as mask for this layer
}
