using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct PerlinNoiseJob : IJobParallelFor
{
    public int ChunkSize;
    public int3 ChunkPosition;
    public float Scale;
    public int Octaves;
    public float Persistence;
    public float Lacunarity;
    public float3 Offset;
    public int Seed;

    [WriteOnly]
    public NativeArray<float> NoiseValues;

    public void Execute(int index)
    {
        // 1D index를 3D 좌표로 변환
        int x = index % ChunkSize;
        int y = (index / ChunkSize) % ChunkSize;
        int z = index / (ChunkSize * ChunkSize);

        // 월드 좌표 계산
        float3 worldPos = new float3(
            ChunkPosition.x * ChunkSize + x,
            ChunkPosition.y * ChunkSize + y,
            ChunkPosition.z * ChunkSize + z
        );

        float noiseValue = GenerateOctaveNoise(worldPos);
        NoiseValues[index] = noiseValue;
    }

    private float GenerateOctaveNoise(float3 position)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float noiseHeight = 0f;
        float maxValue = 0f;

        for (int i = 0; i < Octaves; i++)
        {
            float3 samplePos = (position + Offset) * frequency / Scale;
            float perlinValue = Perlin3D(samplePos + Seed);
                
            noiseHeight += perlinValue * amplitude;
            maxValue += amplitude;

            amplitude *= Persistence;
            frequency *= Lacunarity;
        }

        // -1~1 범위를 0~1 범위로 매핑
        return (noiseHeight / maxValue) * 0.5f + 0.5f;
    }

    // 간단한 3D Perlin Noise 구현
    private float Perlin3D(float3 position)
    {
        // Unity의 noise.snoise를 사용
        return noise.snoise(position);
    }
}