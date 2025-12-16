using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    [Header("Noise Settings")]
    public float Scale = 50f;
    public int Octaves = 4;
    public float Persistence = 0.5f;
    public float Lacunarity = 2f;
    public float3 Offset = float3.zero;
    public int Seed = 0;

    [Header("Chunk Settings")]
    public int ChunkSize = 32;
    public int3 SpawnCoordinate = int3.zero;

    private EntityManager _entityManager;
    private EntityArchetype _chunkArchetype;
    private bool _initialized;
    private World _targetWorld;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        if (_initialized) return;

        // ClientWorld를 찾아서 사용 (렌더링이 되는 World)
        _targetWorld = null;
        foreach (var world in World.All)
        {
            if (world.Name == "ClientWorld")
            {
                _targetWorld = world;
                break;
            }
        }

        // ClientWorld가 없으면 DefaultGameObjectInjectionWorld 사용
        if (_targetWorld == null)
        {
            _targetWorld = World.DefaultGameObjectInjectionWorld;
        }

        if (_targetWorld == null) return;

        _entityManager = _targetWorld.EntityManager;
        
        _chunkArchetype = _entityManager.CreateArchetype(
            typeof(NoiseDataBuffer),
            typeof(NoiseSettings),
            typeof(NoiseGenerationRequest),
            typeof(NoiseVisualizationReady),
            typeof(ChunkData)
        );
        
        _initialized = true;
    }
    
    [ContextMenu("Spawn Single Chunk")]
    public void SpawnSingleChunk()
    {
        Initialize();
        SpawnChunkAt(SpawnCoordinate);
    }
    
    public Entity SpawnChunkAt(int3 coord)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        
        var entity = ecb.CreateEntity(_chunkArchetype);
        
        ecb.SetComponent(entity, new ChunkData
        {
            ChunkPosition = coord,
            ChunkSize = ChunkSize
        });
        
        // 노이즈 설정
        ecb.SetComponent(entity, new NoiseSettings
        {
            Scale = Scale,
            Octaves = Octaves,
            Persistence = Persistence,
            Lacunarity = Lacunarity,
            Offset = Offset,
            Seed = Seed
        });
        
        // 시각화 준비 플래그 (Disabled = 아직 준비 안됨)
        ecb.SetComponentEnabled<NoiseVisualizationReady>(entity, false);

        ecb.Playback(_entityManager);
        ecb.Dispose();
        
        return entity;
        
    }
}