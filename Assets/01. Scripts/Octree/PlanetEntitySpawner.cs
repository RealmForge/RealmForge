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
    private System.Collections.Generic.HashSet<World> _createdWorlds = new System.Collections.Generic.HashSet<World>();

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
        // Server와 Client World 모두에 행성 엔티티 생성
        foreach (var world in World.All)
        {
            // 이미 생성한 World는 스킵
            if (_createdWorlds.Contains(world))
                continue;

            // Server 또는 Client World에만 생성 (정확한 이름 매칭)
            if (world.Name == "ServerWorld" || world.Name == "SessionClientWorld" || world.Name == "ClientWorld")
            {
                CreatePlanetInWorld(world);
                _createdWorlds.Add(world);
            }
        }

        // Fallback: Default World에도 생성 (중복 방지)
        if (World.DefaultGameObjectInjectionWorld != null &&
            !_createdWorlds.Contains(World.DefaultGameObjectInjectionWorld))
        {
            CreatePlanetInWorld(World.DefaultGameObjectInjectionWorld);
            _createdWorlds.Add(World.DefaultGameObjectInjectionWorld);
        }

        _spawned = true;
    }

    private void CreatePlanetInWorld(World world)
    {
        if (world == null || !world.IsCreated) return;

        var em = world.EntityManager;

        // 이미 PlanetTag를 가진 엔티티가 있는지 확인
        var query = em.CreateEntityQuery(typeof(PlanetTag));
        if (query.CalculateEntityCount() > 0)
        {
            Debug.Log($"[PlanetEntitySpawner] Planet already exists in world '{world.Name}', skipping creation.");
            query.Dispose();
            return;
        }
        query.Dispose();

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