using Unity.Entities;
using UnityEngine;
using RealmForge.Planet.Rendering.DebugVisualization.Components;

public class DebugNoiseVisualizationAuthoring : MonoBehaviour
{
    [Header("Cube Prefab")]
    [Tooltip("디버그 시각화에 사용할 Cube 프리팹")]
    public GameObject CubePrefab;

    [Header("Visualization Settings")]
    [Tooltip("각 큐브의 크기 (unit)")]
    public float CubeSize = 1f;

    [Tooltip("이 값 이하의 노이즈는 숨김")]
    [Range(0f, 1f)]
    public float Threshold = 0.5f;

    [Tooltip("Threshold 기능 사용 여부")]
    public bool UseThreshold = true;

    class Baker : Baker<DebugNoiseVisualizationAuthoring>
    {
        public override void Bake(DebugNoiseVisualizationAuthoring authoring)
        {
            if (authoring.CubePrefab == null)
            {
                Debug.LogError("DebugNoiseVisualizationAuthoring: CubePrefab is not assigned!");
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);

            // Cube 프리팹을 Entity prefab으로 변환
            var cubePrefabEntity = GetEntity(authoring.CubePrefab, TransformUsageFlags.Dynamic);

            AddComponent(entity, new DebugVisualizationSettings
            {
                CubePrefab = cubePrefabEntity,
                CubeSize = authoring.CubeSize,
                Threshold = authoring.Threshold,
                UseThreshold = authoring.UseThreshold
            });
        }
    }
}
