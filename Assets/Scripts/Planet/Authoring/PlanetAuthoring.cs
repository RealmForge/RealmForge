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
        // Layer 0: Sphere (기본 형태)
        new NoiseLayerSettings
        {
            layerType = NoiseLayerType.Sphere,
            blendMode = NoiseBlendMode.Subtract,
            strength = 1f
        },
        // Layer 1: Surface Noise
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
    public int3 startChunkGrid = new int3(-2, -2, -2);
    public int3 endChunkGrid = new int3(1, 1, 1);

    class Baker : Baker<PlanetAuthoring>
    {
        public override void Bake(PlanetAuthoring authoring)
        {
            for (int x = authoring.startChunkGrid.x; x < authoring.endChunkGrid.x; x++)
            {
                for (int y = authoring.startChunkGrid.y; y < authoring.endChunkGrid.y; y++)
                {
                    for (int z = authoring.startChunkGrid.z; z < authoring.endChunkGrid.z; z++)
                    {
                        var entity = CreateAdditionalEntity(TransformUsageFlags.None);

                        // Chunk data
                        AddComponent(entity, new ChunkData
                        {
                            ChunkPosition = new int3(x, y, z),
                            ChunkSize = authoring.chunkSize
                        });

                        // Planet data
                        AddComponent(entity, new PlanetData
                        {
                            Center = authoring.center,
                            Radius = authoring.radius,
                            CoreRadius = authoring.coreRadius
                        });

                        // Noise layers buffer
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

                        // Noise generation request (Enabled = start immediately)
                        AddComponent(entity, new NoiseGenerationRequest());

                        // Mesh generation request (Disabled = not ready yet)
                        AddComponent(entity, new MeshGenerationRequest());
                        SetComponentEnabled<MeshGenerationRequest>(entity, false);

                        // Debug visualization ready (Disabled = not ready yet)
                        AddComponent(entity, new NoiseVisualizationReady());
                        SetComponentEnabled<NoiseVisualizationReady>(entity, false);

                        // Noise data buffer
                        AddBuffer<NoiseDataBuffer>(entity);
                    }
                }
            }
        }
    }
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
