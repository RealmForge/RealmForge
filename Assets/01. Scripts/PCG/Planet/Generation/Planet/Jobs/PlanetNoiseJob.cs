using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Generates planet terrain using layer-based approach.
/// All shapes (Sphere, Noise, Cave) are unified as layers.
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
    public float CoreRadius;  // 핵 반경 (이 안쪽은 동굴 불가)

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

        // Pre-calculate sphere SDF for Sphere layers
        float distanceFromCenter = math.length(worldPos - PlanetCenter);
        float sphereSDF;

        if (distanceFromCenter > PlanetRadius)
        {
            // 외부: 양수 (거리에 비례)
            sphereSDF = distanceFromCenter - PlanetRadius;
        }
        else
        {
            // 내부: 표면에서 핵까지 부드럽게 전환
            // t = 0 (표면) ~ 1 (핵 또는 중심)
            float t = 1f - (distanceFromCenter - CoreRadius) / (PlanetRadius - CoreRadius + 0.001f);
            t = math.saturate(t);

            // smoothstep으로 부드러운 전환
            float smooth = t * t * (3f - 2f * t);

            // 표면: -1, 핵: -100으로 부드럽게 보간
            sphereSDF = math.lerp(-1f, -100f, smooth);
        }

        // Accumulate from all layers
        float accum = 0f;
        float firstLayerValue = 0f;

        for (int i = 0; i < LayerCount; i++)
        {
            NoiseLayerData layer = NoiseLayers[i];
            float layerValue = 0f;

            // Layer type별 값 계산
            if (layer.LayerType == NoiseLayerType.Sphere)
            {
                // Sphere: SDF 값 그대로 사용
                layerValue = sphereSDF;
            }
            else if (layer.LayerType == NoiseLayerType.Surface)
            {
                // Surface: 노이즈 0~1 → -0.5~0.5로 변환
                float noise01 = GenerateLayerNoise(worldPos, layer);
                layerValue = (noise01 - 0.5f) * 2f;  // -1 ~ 1
            }
            else if (layer.LayerType == NoiseLayerType.Cave)
            {
                // Cave: Worm 방식 - 0.5 근처일 때 동굴
                float noise01 = GenerateLayerNoise(worldPos, layer);
                layerValue = 1.0f - math.abs(noise01 - 0.5f) * 2f;
                layerValue = math.max(0f, layerValue);  // 0 ~ 1

                // 표면 근처에서는 동굴 입구가 좁아지도록 스케일 적용
                // 깊이 비율: 0 (표면) ~ 1 (깊은 곳)
                float rawSDF = distanceFromCenter - PlanetRadius;  // 원본 SDF (음수 = 내부)
                float depthFactor = math.saturate(-rawSDF / (PlanetRadius * 0.3f));  // 30% 깊이에서 최대
                layerValue *= depthFactor;
            }

            // 마스킹용 값 저장 (0~1로 정규화)
            if (i == 0)
            {
                if (layer.LayerType == NoiseLayerType.Sphere)
                {
                    // Sphere: 내부=1, 외부=0 으로 정규화
                    firstLayerValue = math.saturate(-sphereSDF / PlanetRadius + 0.5f);
                }
                else
                {
                    firstLayerValue = (layerValue + 1f) * 0.5f;  // -1~1 → 0~1
                }
            }
            else if (layer.UseFirstLayerAsMask)
            {
                layerValue *= firstLayerValue;
            }

            // 블렌딩
            float contribution = layerValue * layer.Strength;
            if (layer.BlendMode == NoiseBlendMode.Add)
            {
                accum += contribution;  // 양수 증가 → 밀도 감소 (구멍)
            }
            else // Subtract
            {
                accum -= contribution;  // 음수 증가 → 밀도 증가 (solid)
            }
        }

        // Convert to 0-1 range: inside (negative) = 1, outside (positive) = 0
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
