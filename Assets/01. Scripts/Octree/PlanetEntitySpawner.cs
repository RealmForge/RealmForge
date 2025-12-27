using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 서브씬 없이 런타임에 직접 Planet 엔티티 생성
/// </summary>
public class PlanetEntitySpawner : MonoBehaviour
{
    [Header("Planet Settings")]
    public float3 center = float3.zero;
    public float radius = 64f;
    public float coreRadius = 10f;

    [Header("Chunk Settings")]
    public int chunkSize = 16;

    private Entity _planetEntity;
    private bool _spawned;

    void Start()
    {
        Invoke(nameof(SpawnPlanetEntity), 0.1f);  // 약간 딜레이
    }

    void SpawnPlanetEntity()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;
        
        var em = world.EntityManager;

        // Archetype으로 한 번에 생성
        var archetype = em.CreateArchetype(
            typeof(PlanetTag),
            typeof(PlanetData),
            typeof(PlanetChunkSettings),
            typeof(NoiseLayerBuffer)
        );

        _planetEntity = em.CreateEntity(archetype);

        // 값 설정
        em.SetComponentData(_planetEntity, new PlanetData
        {
            Center = center,
            Radius = radius,
            CoreRadius = coreRadius
        });

        em.SetComponentData(_planetEntity, new PlanetChunkSettings
        {
            ChunkSize = chunkSize
        });

        // 버퍼는 이미 생성됨, 값만 추가
        var buffer = em.GetBuffer<NoiseLayerBuffer>(_planetEntity);
        
        buffer.Add(new NoiseLayerBuffer
        {
            LayerType = NoiseLayerType.Sphere,
            BlendMode = NoiseBlendMode.Subtract,
            Strength = 1f,
            Scale = 1f,
            Octaves = 1,
            Persistence = 0.5f,
            Lacunarity = 2f,
            Offset = float3.zero,
            UseFirstLayerAsMask = false
        });

        buffer.Add(new NoiseLayerBuffer
        {
            LayerType = NoiseLayerType.Surface,
            BlendMode = NoiseBlendMode.Subtract,
            Scale = 50f,
            Octaves = 4,
            Persistence = 0.5f,
            Lacunarity = 2f,
            Strength = 5f,
            Offset = float3.zero,
            UseFirstLayerAsMask = true
        });

        _spawned = true;
        Debug.Log($"[PlanetEntitySpawner] ★ Planet 엔티티 생성 완료! Center={center}, Radius={radius}");
    }

    void OnDestroy()
    {
        if (_spawned && World.DefaultGameObjectInjectionWorld != null)
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (em.Exists(_planetEntity))
            {
                em.DestroyEntity(_planetEntity);
            }
        }
    }
}