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

    public float NoiseScale;
    public int Octaves;
    public float Persistence;
    public float Lacunarity;
    public float HeightMultiplier;
    public float3 Offset;
    public int Seed;

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
        float density = distanceFromCenter - PlanetRadius;

        // 릿지 노이즈로 표면 변형
        float noiseValue = GenerateRidgeNoise(worldPos);
        density -= noiseValue * HeightMultiplier;

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

            // 릿지 노이즈: 산맥 같은 날카로운 지형
            float ridgeValue = 1f - math.abs(perlinValue);

            noiseSum += ridgeValue * amplitude;
            maxValue += amplitude;

            amplitude *= Persistence;
            frequency *= Lacunarity;
        }

        return noiseSum / maxValue;
    }
}
