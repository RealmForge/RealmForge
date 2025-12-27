using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// PlanetAuthoring 값 변경을 감지하여 ECS 컴포넌트에 동기화.
/// 값이 변경되면 NoiseGenerationRequest를 재활성화하여 노이즈 재생성.
/// </summary>
public partial class PlanetSyncSystem : SystemBase
{
    private float3 _lastCenter;
    private float _lastRadius;
    private float _lastCoreRadius;
    private int _lastLayerHash;

    protected override void OnCreate()
    {
        RequireForUpdate<PlanetData>();
    }

    protected override void OnUpdate()
    {
        var authoring = Object.FindFirstObjectByType<PlanetAuthoring>();
        if (authoring == null) return;

        // 변경 감지
        bool changed = false;

        if (!_lastCenter.Equals(authoring.center) ||
            _lastRadius != authoring.radius ||
            _lastCoreRadius != authoring.coreRadius)
        {
            changed = true;
            _lastCenter = authoring.center;
            _lastRadius = authoring.radius;
            _lastCoreRadius = authoring.coreRadius;
        }

        // 레이어 해시 비교 (간단한 변경 감지)
        int layerHash = ComputeLayerHash(authoring.noiseLayers);
        if (_lastLayerHash != layerHash)
        {
            changed = true;
            _lastLayerHash = layerHash;
        }

        if (!changed) return;

        // PlanetData 업데이트
        foreach (var planetData in SystemAPI.Query<RefRW<PlanetData>>())
        {
            planetData.ValueRW.Center = authoring.center;
            planetData.ValueRW.Radius = authoring.radius;
            planetData.ValueRW.CoreRadius = authoring.coreRadius;
        }

        // NoiseLayerBuffer 업데이트 + NoiseGenerationRequest 재활성화
        foreach (var (layerBuffer, entity) in
            SystemAPI.Query<DynamicBuffer<NoiseLayerBuffer>>()
                .WithEntityAccess())
        {
            // 버퍼 클리어 후 재구성
            layerBuffer.Clear();
            foreach (var layer in authoring.noiseLayers)
            {
                layerBuffer.Add(new NoiseLayerBuffer
                {
                    LayerType = layer.layerType,
                    BlendMode = layer.blendMode,
                    Scale = layer.scale,
                    Octaves = layer.octaves,
                    Persistence = layer.persistence,
                    Lacunarity = layer.lacunarity,
                    Strength = layer.strength,
                    Offset = layer.offset,
                    UseFirstLayerAsMask = layer.useFirstLayerAsMask
                });
            }

            // 노이즈 재생성 요청
            EntityManager.SetComponentEnabled<NoiseGenerationRequest>(entity, true);
        }
    }

    private int ComputeLayerHash(NoiseLayerSettings[] layers)
    {
        if (layers == null) return 0;

        int hash = layers.Length;
        foreach (var layer in layers)
        {
            hash = hash * 31 + layer.scale.GetHashCode();
            hash = hash * 31 + layer.octaves;
            hash = hash * 31 + layer.persistence.GetHashCode();
            hash = hash * 31 + layer.lacunarity.GetHashCode();
            hash = hash * 31 + layer.strength.GetHashCode();
            hash = hash * 31 + layer.offset.GetHashCode();
            hash = hash * 31 + (int)layer.layerType;
            hash = hash * 31 + (int)layer.blendMode;
            hash = hash * 31 + (layer.useFirstLayerAsMask ? 1 : 0);
        }
        return hash;
    }
}
