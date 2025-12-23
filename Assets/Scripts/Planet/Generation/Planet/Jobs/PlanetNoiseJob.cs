using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Generates planet terrain by combining Sphere SDF with multi-layer Perlin noise.
/// Output: 0 = outside (air), 1 = inside (solid)
/// </summary>
[BurstCompile]
public struct PlanetNoiseJob : IJobParallelFor
{
    // Chunk parameters
    public int ChunkSize;
    public int SampleSize;  // ChunkSize + 1
    public int3 ChunkPosition;

    // Planet parameters
    public float3 PlanetCenter;
    public float PlanetRadius;
    public float NoiseStrength;  // Overall noise multiplier

    // Noise layers (from NoiseLayerBuffer)
    [ReadOnly] public NativeArray<NoiseLayerData> NoiseLayers;
    public int LayerCount;

    public int Seed;

    [WriteOnly]
    public NativeArray<float> NoiseValues;

    public void Execute(int index)
    {
        // 1D index to 3D coordinates (SampleSize based)
        int x = index % SampleSize;
        int y = (index / SampleSize) % SampleSize;
        int z = index / (SampleSize * SampleSize);

        // World position calculation
        float3 worldPos = new float3(
            ChunkPosition.x * ChunkSize + x,
            ChunkPosition.y * ChunkSize + y,
            ChunkPosition.z * ChunkSize + z
        );

        // Sphere SDF: positive = outside, negative = inside
        float distanceFromCenter = math.length(worldPos - PlanetCenter);
        float sphereSDF = distanceFromCenter - PlanetRadius;

        // Accumulate noise from all layers
        float totalNoise = 0f;
        float firstLayerValue = 0f;

        for (int i = 0; i < LayerCount; i++)
        {
            NoiseLayerData layer = NoiseLayers[i];
            float layerNoise = GenerateLayerNoise(worldPos, layer);

            // Store first layer value for masking
            if (i == 0)
            {
                firstLayerValue = layerNoise;
                totalNoise += layerNoise * layer.Strength;
            }
            else
            {
                // Apply first layer as mask if enabled
                if (layer.UseFirstLayerAsMask)
                {
                    layerNoise *= firstLayerValue;
                }
                totalNoise += layerNoise * layer.Strength;
            }
        }

        // Combine Sphere SDF with noise
        // sphereSDF is negative inside, positive outside
        // We want negative values to become solid (1), positive to become air (0)
        float finalValue = sphereSDF - (totalNoise * NoiseStrength);

        // Convert to 0-1 range: inside (negative) = 1, outside (positive) = 0
        // Using smooth transition around the surface
        NoiseValues[index] = math.saturate(-finalValue);
    }

    private float GenerateLayerNoise(float3 position, NoiseLayerData layer)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float noiseHeight = 0f;
        float maxValue = 0f;

        for (int i = 0; i < layer.Octaves; i++)
        {
            float3 samplePos = (position + layer.Offset) * frequency / layer.Scale;
            float perlinValue = noise.snoise(samplePos + Seed);

            noiseHeight += perlinValue * amplitude;
            maxValue += amplitude;

            amplitude *= layer.Persistence;
            frequency *= layer.Lacunarity;
        }

        // Normalize to 0-1 range
        return (noiseHeight / maxValue) * 0.5f + 0.5f;
    }
}

/// <summary>
/// Burst-compatible struct for noise layer data.
/// Mirrors NoiseLayerBuffer but used within Job.
/// </summary>
public struct NoiseLayerData
{
    public float Scale;
    public int Octaves;
    public float Persistence;
    public float Lacunarity;
    public float Strength;
    public float3 Offset;
    public bool UseFirstLayerAsMask;
}
