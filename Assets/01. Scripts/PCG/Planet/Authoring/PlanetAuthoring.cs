using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PlanetAuthoring : MonoBehaviour
{
    [Header("Planet Settings")]
    public float3 center = float3.zero;
    public float radius = 50f;
    [Tooltip("핵 반경. 이 안쪽은 동굴이 생기지 않음")]
    public float coreRadius = 20f;

    [Header("Layers")]
    public NoiseLayerSettings[] noiseLayers = new NoiseLayerSettings[]
    {
        new NoiseLayerSettings
        {
            layerType = NoiseLayerType.Sphere,
            blendMode = NoiseBlendMode.Subtract,
            strength = 1f
        },
        new NoiseLayerSettings
        {
            layerType = NoiseLayerType.Surface,
            blendMode = NoiseBlendMode.Subtract,
            scale = 50f,
            octaves = 4,
            persistence = 0.5f,
            lacunarity = 2f,
            strength = 5f,
            offset = float3.zero,
            useFirstLayerAsMask = true
        }
    };

    [Header("Chunk Settings")]
    public int chunkSize = 16;

    class Baker : Baker<PlanetAuthoring>
    {
        public override void Bake(PlanetAuthoring authoring)
        {
            // ★ 변경: 단일 행성 엔티티만 생성 (청크는 런타임에 옥트리 기반 생성)
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new PlanetData
            {
                Center = authoring.center,
                Radius = authoring.radius,
                CoreRadius = authoring.coreRadius
            });

            AddComponent(entity, new PlanetChunkSettings
            {
                ChunkSize = authoring.chunkSize
            });

            var layerBuffer = AddBuffer<NoiseLayerBuffer>(entity);
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

            AddComponent<PlanetTag>(entity);
        }
    }
}

public struct PlanetTag : IComponentData { }

public struct PlanetChunkSettings : IComponentData
{
    public int ChunkSize;
}

[Serializable]
public class NoiseLayerSettings
{
    public NoiseLayerType layerType = NoiseLayerType.Surface;
    public NoiseBlendMode blendMode = NoiseBlendMode.Subtract;
    public float scale = 50f;
    public int octaves = 4;
    [Range(0f, 1f)]
    public float persistence = 0.5f;
    public float lacunarity = 2f;
    public float strength = 1f;
    public float3 offset = float3.zero;
    public bool useFirstLayerAsMask = false;
}