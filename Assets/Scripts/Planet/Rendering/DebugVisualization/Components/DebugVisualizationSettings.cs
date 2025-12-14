using Unity.Entities;

public struct DebugVisualizationSettings : IComponentData
{
    public Entity CubePrefab;
    public float CubeSize;
    public float Threshold;
    public bool UseThreshold;
}

/// <summary>
/// 시각화가 이미 완료된 청크를 표시하는 태그
/// </summary>
public struct DebugVisualizationCompleted : IComponentData {}

/// <summary>
/// 디버그 시각화로 생성된 큐브 엔티티를 표시하는 태그 (cleanup용)
/// </summary>
public struct DebugVisualizationCube : IComponentData
{
    public Entity SourceChunk; // 어느 청크에서 생성되었는지
}