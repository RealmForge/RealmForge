using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// 단일 Perlin 노이즈 생성 Job.
/// 현재는 PlanetNoiseJob으로 대체되어 사용되지 않음.
/// 추후 단순 노이즈 생성이 필요할 때 재사용 가능.
///
/// 사용 예시:
/// <code>
/// var job = new PerlinNoiseJob
/// {
///     ChunkSize = 16,
///     SampleSize = 17,
///     ChunkPosition = new int3(0, 0, 0),
///     Scale = 50f,
///     Octaves = 4,
///     Persistence = 0.5f,
///     Lacunarity = 2f,
///     Offset = float3.zero,
///     Seed = 0,
///     NoiseValues = new NativeArray&lt;float&gt;(17*17*17, Allocator.TempJob)
/// };
/// var handle = job.Schedule(totalSize, 64);
/// </code>
/// 결과는 NoiseJobResult로 래핑하여 NoiseDataCopySystem에서 처리.
/// </summary>
[BurstCompile]
public struct PerlinNoiseJob : IJobParallelFor
{
    public int ChunkSize;
    public int SampleSize;  // ChunkSize + 1 (for seamless chunk boundaries)
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
        // 1D index를 3D 좌표로 변환 (SampleSize 기준)
        int x = index % SampleSize;
        int y = (index / SampleSize) % SampleSize;
        int z = index / (SampleSize * SampleSize);

        // 월드 좌표 계산 (ChunkSize 기준으로 청크 위치 계산)
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