using Unity.Entities;
using Unity.Mathematics;

public struct NoiseSettings : IComponentData
{
    public float Scale;
    public int Octaves;
    public float Persistence;
    public float Lacunarity;
    public float3 Offset;
    public int Seed;
}

/// <summary>
/// 노이즈 생성 요청 플래그 (활성화 시 생성 시작)
/// </summary>
public struct NoiseGenerationRequest : IComponentData, IEnableableComponent {}

/// <summary>
/// 노이즈 시각화 준비 완료 플래그
/// </summary>
public struct NoiseVisualizationReady : IComponentData, IEnableableComponent {}
