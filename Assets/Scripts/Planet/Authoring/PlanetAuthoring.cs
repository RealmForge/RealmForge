using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PlanetAuthoring : MonoBehaviour
{
    [Header("Planet Settings")]
    public float3 center = float3.zero;
    public float radius = 50f;
    public float noiseStrength = 10f;

    [Header("Noise Layers")]
    public NoiseLayerSettings[] noiseLayers = new NoiseLayerSettings[]
    {
        new NoiseLayerSettings
        {
            scale = 50f,
            octaves = 4,
            persistence = 0.5f,
            lacunarity = 2f,
            strength = 1f,
            offset = float3.zero,
            useFirstLayerAsMask = false
        }
    };

    [Header("Chunk Settings")]
    public int chunkSize = 16;
    public int3 chunkGridSize = new int3(4, 4, 4);

    class Baker : Baker<PlanetAuthoring>
    {
        public override void Bake(PlanetAuthoring authoring)
        {
            for (int x = 0; x < authoring.chunkGridSize.x; x++)
            {
                for (int y = 0; y < authoring.chunkGridSize.y; y++)
                {
                    for (int z = 0; z < authoring.chunkGridSize.z; z++)
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
                            NoiseStrength = authoring.noiseStrength
                        });

                        // Noise layers buffer
                        var layerBuffer = AddBuffer<NoiseLayerBuffer>(entity);
                        foreach (var layer in authoring.noiseLayers)
                        {
                            layerBuffer.Add(new NoiseLayerBuffer
                            {
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
    public float scale = 50f;
    public int octaves = 4;
    [Range(0f, 1f)]
    public float persistence = 0.5f;
    public float lacunarity = 2f;
    [Range(0f, 2f)]
    public float strength = 1f;
    public float3 offset = float3.zero;
    public bool useFirstLayerAsMask = false;
}
