using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Noise layer type for different terrain features.
/// </summary>
public enum NoiseLayerType
{
    Sphere,     // 구형 SDF (기본 형태)
    Surface,    // 표면 변형
    Cave        // 동굴 생성
}

/// <summary>
/// Noise blend mode for combining layers.
/// </summary>
public enum NoiseBlendMode
{
    Add,        // 밀도 증가 (구멍 생성)
    Subtract    // 밀도 감소 (표면 울퉁불퉁)
}

/// <summary>
/// Single noise layer configuration for planet surface generation.
/// Currently supports Perlin noise only. Other types (Simplex, Ridged) planned for future.
/// </summary>
public struct NoiseLayerBuffer : IBufferElementData
{
    public NoiseLayerType LayerType;    // Layer type (Surface or Cave)
    public NoiseBlendMode BlendMode;    // Blend mode (Add or Subtract)
    public float Scale;                 // Noise scale (frequency)
    public int Octaves;                 // Number of octaves
    public float Persistence;           // Amplitude decrease per octave
    public float Lacunarity;            // Frequency increase per octave
    public float Strength;              // Layer weight/strength
    public float3 Offset;               // Noise offset
    public bool UseFirstLayerAsMask;    // Use first layer as mask for this layer
}
