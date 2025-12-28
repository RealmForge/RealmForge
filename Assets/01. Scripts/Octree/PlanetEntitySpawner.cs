using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PlanetEntitySpawner : MonoBehaviour
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

    private Entity _planetEntity;
    private World _targetWorld;
    private bool _spawned;

    void Start()
    {
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
            Center = center,
            Radius = radius
        });

        em.SetComponentData(_planetEntity, new PlanetChunkSettings
        {
            ChunkSize = chunkSize
        });

        em.SetComponentData(_planetEntity, new NoiseSettings
        {
            Scale = noiseScale,
            Octaves = octaves,
            Persistence = persistence,
            Lacunarity = lacunarity,
            HeightMultiplier = heightMultiplier,
            Offset = offset,
            Seed = seed,

            CaveScale = caveScale,
            CaveOctaves = caveOctaves,
            CaveThreshold = caveThreshold,
            CaveStrength = caveStrength,
            CaveMaxDepth = caveMaxDepth
        });

        _spawned = true;
        Debug.Log($"[PlanetEntitySpawner] Planet entity created. Center={center}, Radius={radius}");
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
