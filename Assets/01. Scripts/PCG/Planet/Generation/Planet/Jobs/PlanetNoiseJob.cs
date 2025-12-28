using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct PlanetNoiseJob : IJobParallelFor
{
    public int ChunkSize;
    public int SampleSize;
    public float3 ChunkMin;
    public float VoxelSize;

    public float3 PlanetCenter;
    public float PlanetRadius;

    // Surface
    public float NoiseScale;
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

    [WriteOnly]
    public NativeArray<float> NoiseValues;

    public void Execute(int index)
    {
        int x = index % SampleSize;
        int y = (index / SampleSize) % SampleSize;
        int z = index / (SampleSize * SampleSize);

        float3 worldPos = ChunkMin + new float3(x, y, z) * VoxelSize;

        // 구형 밀도 (SDF): 음수 = Solid, 양수 = Air
        float distanceFromCenter = math.length(worldPos - PlanetCenter);
        float sphereDensity = distanceFromCenter - PlanetRadius;

        // 릿지 노이즈로 표면 변형
        float surfaceNoise = GenerateRidgeNoise(worldPos);
        float density = sphereDensity - surfaceNoise * HeightMultiplier;

        // 동굴: 행성 내부에서만 적용
        if (density < 0 && CaveStrength > 0)
        {
            float depthFactor = math.saturate(-density / CaveMaxDepth);
            float caveNoise = GenerateCaveNoise(worldPos);

            if (caveNoise > CaveThreshold)
            {
                float caveValue = (caveNoise - CaveThreshold) / (1f - CaveThreshold);
                density += caveValue * CaveStrength * depthFactor;
            }
        }

        NoiseValues[index] = density;
    }

    private float GenerateRidgeNoise(float3 position)
    {
        float noiseSum = 0f;
        float amplitude = 1f;
        float frequency = 1f / NoiseScale;
        float maxValue = 0f;

        for (int i = 0; i < Octaves; i++)
        {
            float3 samplePos = (position + Offset) * frequency + Seed;
            float perlinValue = noise.snoise(samplePos);
            float ridgeValue = 1f - math.abs(perlinValue);

            noiseSum += ridgeValue * amplitude;
            maxValue += amplitude;

            amplitude *= Persistence;
            frequency *= Lacunarity;
        }

        return noiseSum / maxValue;
    }

    private float GenerateCaveNoise(float3 position)
    {
        float noiseSum = 0f;
        float amplitude = 1f;
        float frequency = 1f / CaveScale;
        float maxValue = 0f;

        for (int i = 0; i < CaveOctaves; i++)
        {
            float3 samplePos = position * frequency + Seed + 1000f;
            float perlinValue = noise.snoise(samplePos);

            // Worm 노이즈: 0 근처에서 1, ±1에서 0
            float wormValue = 1f - math.abs(perlinValue);

            noiseSum += wormValue * amplitude;
            maxValue += amplitude;

            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return noiseSum / maxValue;
    }
}
