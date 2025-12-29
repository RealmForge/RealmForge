// PlanetEntitySpawner.cs (변경 없음 - MonoBehaviour라서 Job 적용 불가)
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
        // SessionClientWorld에만 행성 엔티티 생성
        World sessionClientWorld = null;
        foreach (var world in World.All)
        {
            if (world.Name == "SessionClientWorld" && world.IsCreated)
            {
                sessionClientWorld = world;
                break;
            }
        }

        if (sessionClientWorld != null)
        {
            CreatePlanetInWorld(sessionClientWorld);
            Debug.Log("[PlanetEntitySpawner] Planet entity created in SessionClientWorld");
        }
        else
        {
            Debug.LogWarning("[PlanetEntitySpawner] SessionClientWorld not found! Planet will not be created.");
        }

        _spawned = true;
    }

    private void CreatePlanetInWorld(World world)
    {
        if (world == null || !world.IsCreated) return;

        var em = world.EntityManager;

        var archetype = em.CreateArchetype(
            typeof(PlanetTag),
            typeof(PlanetData),
            typeof(PlanetChunkSettings),
            typeof(NoiseSettings)
        );

        var planetEntity = em.CreateEntity(archetype);

        em.SetComponentData(planetEntity, new PlanetData
        {
            Center = settings.center,
            Radius = settings.radius
        });

        em.SetComponentData(planetEntity, new PlanetChunkSettings
        {
            ChunkSize = settings.chunkSize
        });

        em.SetComponentData(planetEntity, new NoiseSettings
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

        var terrainBuffer = em.AddBuffer<TerrainLayerBuffer>(planetEntity);
        foreach (var layer in settings.terrainLayers)
        {
            terrainBuffer.Add(new TerrainLayerBuffer
            {
                MaxHeight = layer.maxHeight,
                Color = new float4(layer.color.r, layer.color.g, layer.color.b, layer.color.a)
            });
        }

        Debug.Log($"[PlanetEntitySpawner] Planet entity created in world '{world.Name}'. Center={settings.center}, Radius={settings.radius}");

        // 첫 번째로 생성된 엔티티를 추적
        if (_planetEntity == Entity.Null)
        {
            _planetEntity = planetEntity;
            _targetWorld = world;
        }
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