using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Generates planet terrain using layer-based approach.
/// ★ 옥트리 기반: ChunkMin + VoxelSize로 월드 좌표 계산
/// </summary>
[BurstCompile]
public struct PlanetNoiseJob : IJobParallelFor
{
    public int ChunkSize;
    public int SampleSize;
    
    // ★ 변경: 옥트리 월드 좌표
    public float3 ChunkMin;
    public float VoxelSize;

    public float3 PlanetCenter;
    public float PlanetRadius;
    public float CoreRadius;

    [ReadOnly] public NativeArray<NoiseLayerData> NoiseLayers;
    public int LayerCount;
    public int Seed;

    [WriteOnly]
    public NativeArray<float> NoiseValues;

    public void Execute(int index)
    {
        int x = index % SampleSize;
        int y = (index / SampleSize) % SampleSize;
        int z = index / (SampleSize * SampleSize);

        // ★ 변경: 옥트리 기반 월드 좌표
        float3 worldPos = ChunkMin + new float3(x, y, z) * VoxelSize;

        float distanceFromCenter = math.length(worldPos - PlanetCenter);
        float sphereSDF;

        if (distanceFromCenter > PlanetRadius)
        {
            sphereSDF = distanceFromCenter - PlanetRadius;
        }
        else
        {
            float t = 1f - (distanceFromCenter - CoreRadius) / (PlanetRadius - CoreRadius + 0.001f);
            t = math.saturate(t);
            float smooth = t * t * (3f - 2f * t);
            sphereSDF = math.lerp(-1f, -100f, smooth);
        }

        float accum = 0f;
        float firstLayerValue = 0f;

        for (int i = 0; i < LayerCount; i++)
        {
            NoiseLayerData layer = NoiseLayers[i];
            float layerValue = 0f;

            if (layer.LayerType == NoiseLayerType.Sphere)
            {
                layerValue = sphereSDF;
            }
            else if (layer.LayerType == NoiseLayerType.Surface)
            {
                float noise01 = GenerateLayerNoise(worldPos, layer);
                layerValue = (noise01 - 0.5f) * 2f;
            }
            else if (layer.LayerType == NoiseLayerType.Cave)
            {
                float noise01 = GenerateLayerNoise(worldPos, layer);
                layerValue = 1.0f - math.abs(noise01 - 0.5f) * 2f;
                layerValue = math.max(0f, layerValue);

                float rawSDF = distanceFromCenter - PlanetRadius;
                float depthFactor = math.saturate(-rawSDF / (PlanetRadius * 0.3f));
                layerValue *= depthFactor;
            }

            if (i == 0)
            {
                if (layer.LayerType == NoiseLayerType.Sphere)
                {
                    firstLayerValue = math.saturate(-sphereSDF / PlanetRadius + 0.5f);
                }
                else
                {
                    firstLayerValue = (layerValue + 1f) * 0.5f;
                }
            }
            else if (layer.UseFirstLayerAsMask)
            {
                layerValue *= firstLayerValue;
            }

            float contribution = layerValue * layer.Strength;
            if (layer.BlendMode == NoiseBlendMode.Add)
            {
                accum += contribution;
            }
            else
            {
                accum -= contribution;
            }
        }

        NoiseValues[index] = math.saturate(-accum);
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

        return (noiseHeight / maxValue) * 0.5f + 0.5f;
    }
}

public struct NoiseLayerData
{
    public NoiseLayerType LayerType;
    public NoiseBlendMode BlendMode;
    public float Scale;
    public int Octaves;
    public float Persistence;
    public float Lacunarity;
    public float Strength;
    public float3 Offset;
    public bool UseFirstLayerAsMask;
}