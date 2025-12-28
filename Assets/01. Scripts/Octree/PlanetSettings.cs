using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "PlanetSettings", menuName = "RealmForge/Planet Settings")]
public class PlanetSettings : ScriptableObject
{
    [Header("Planet Settings")]
    public float3 center = float3.zero;
    public float radius = 64f;

    [Header("Surface Noise")]
    public float noiseScale = 50f;
    public int octaves = 6;
    [Range(0f, 1f)]
    public float persistence = 0.5f;
    public float lacunarity = 2f;
    public float heightMultiplier = 10f;
    public float3 offset = float3.zero;
    public int seed = 0;

    [Header("Cave")]
    public float caveScale = 30f;
    public int caveOctaves = 3;
    [Range(0f, 1f)]
    public float caveThreshold = 0.5f;
    public float caveStrength = 20f;
    public float caveMaxDepth = 30f;

    [Header("Chunk Settings")]
    public int chunkSize = 16;

    [Header("Terrain Layers")]
    public TerrainLayerConfig[] terrainLayers = new TerrainLayerConfig[]
    {
        new TerrainLayerConfig { maxHeight = 5f, color = new Color(0.5f, 0.5f, 0.5f) },
        new TerrainLayerConfig { maxHeight = 10f, color = new Color(0.6f, 0.4f, 0.2f) },
        new TerrainLayerConfig { maxHeight = 20f, color = new Color(0.3f, 0.7f, 0.2f) }
    };
}
