using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public struct TerrainLayerConfig
{
    public float maxHeight;
    public Color color;
}

public class PlanetEntitySpawner : MonoBehaviour
{
    [SerializeField]
    private PlanetSettings settings;

    private Entity _planetEntity;
    private World _targetWorld;
    private bool _spawned;

    void Start()
    {
        if (settings == null)
        {
            Debug.LogError("[PlanetEntitySpawner] PlanetSettings is not assigned!");
            return;
        }
        Invoke(nameof(SpawnPlanetEntity), 0.1f);
    }

    void SpawnPlanetEntity()
    {
        World world = null;
        foreach (var w in World.All)
        {
            if (w.Name.Contains("Client"))
            {
                world = w;
                break;
            }
        }

        if (world == null)
            world = World.DefaultGameObjectInjectionWorld;

        if (world == null) return;

        _targetWorld = world;
        var em = world.EntityManager;

        var archetype = em.CreateArchetype(
            typeof(PlanetTag),
            typeof(PlanetData),
            typeof(PlanetChunkSettings),
            typeof(NoiseSettings)
        );

        _planetEntity = em.CreateEntity(archetype);

        em.SetComponentData(_planetEntity, new PlanetData
        {
            Center = settings.center,
            Radius = settings.radius
        });

        em.SetComponentData(_planetEntity, new PlanetChunkSettings
        {
            ChunkSize = settings.chunkSize
        });

        em.SetComponentData(_planetEntity, new NoiseSettings
        {
            Scale = settings.noiseScale,
            Octaves = settings.octaves,
            Persistence = settings.persistence,
            Lacunarity = settings.lacunarity,
            HeightMultiplier = settings.heightMultiplier,
            Offset = settings.offset,
            Seed = settings.seed,

            CaveScale = settings.caveScale,
            CaveOctaves = settings.caveOctaves,
            CaveThreshold = settings.caveThreshold,
            CaveStrength = settings.caveStrength,
            CaveMaxDepth = settings.caveMaxDepth
        });

        var terrainBuffer = em.AddBuffer<TerrainLayerBuffer>(_planetEntity);
        foreach (var layer in settings.terrainLayers)
        {
            terrainBuffer.Add(new TerrainLayerBuffer
            {
                MaxHeight = layer.maxHeight,
                Color = new float4(layer.color.r, layer.color.g, layer.color.b, layer.color.a)
            });
        }

        _spawned = true;
        Debug.Log($"[PlanetEntitySpawner] Planet entity created. Center={settings.center}, Radius={settings.radius}");
    }

    void OnDestroy()
    {
        if (_spawned && _targetWorld != null && _targetWorld.IsCreated)
        {
            var em = _targetWorld.EntityManager;
            if (em.Exists(_planetEntity))
            {
                em.DestroyEntity(_planetEntity);
            }
        }
    }
}
