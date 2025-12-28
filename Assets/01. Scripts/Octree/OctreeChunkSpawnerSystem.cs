// OctreeChunkSpawnerSystem.cs - Burst + Job System
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class OctreeChunkSpawnerSystem : SystemBase
{
    private NativeHashMap<long, Entity> _chunkMap;
    private NativeList<long> _keysToRemove;
    private NativeArray<long> _nodeKeys;
    private NativeArray<bool> _isLeafFlags;
    
    private Entity _planetEntity;
    private bool _initialized;

    private NoiseSettings _cachedNoiseSettings;
    private PlanetData _cachedPlanetData;
    private PlanetChunkSettings _cachedChunkSettings;
    private NativeArray<TerrainLayerBuffer> _cachedTerrainLayers;

    protected override void OnCreate()
    {
        _chunkMap = new NativeHashMap<long, Entity>(50000, Allocator.Persistent);
        _keysToRemove = new NativeList<long>(1000, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        if (_chunkMap.IsCreated) _chunkMap.Dispose();
        if (_keysToRemove.IsCreated) _keysToRemove.Dispose();
        if (_nodeKeys.IsCreated) _nodeKeys.Dispose();
        if (_isLeafFlags.IsCreated) _isLeafFlags.Dispose();
        if (_cachedTerrainLayers.IsCreated) _cachedTerrainLayers.Dispose();
    }

    protected override void OnUpdate()
    {
        if (!World.Name.Contains("Client")) return;
        if (OctreeManager.Instance == null) return;

        if (!_initialized)
        {
            var query = GetEntityQuery(typeof(PlanetTag));
            if (query.CalculateEntityCount() == 0) return;

            _planetEntity = query.GetSingletonEntity();
            _cachedPlanetData = EntityManager.GetComponentData<PlanetData>(_planetEntity);
            _cachedChunkSettings = EntityManager.GetComponentData<PlanetChunkSettings>(_planetEntity);
            _cachedNoiseSettings = EntityManager.GetComponentData<NoiseSettings>(_planetEntity);

            var terrainBuffer = EntityManager.GetBuffer<TerrainLayerBuffer>(_planetEntity);
            _cachedTerrainLayers = new NativeArray<TerrainLayerBuffer>(terrainBuffer.Length, Allocator.Persistent);
            for (int i = 0; i < terrainBuffer.Length; i++)
            {
                _cachedTerrainLayers[i] = terrainBuffer[i];
            }

            _initialized = true;
            Debug.Log($"[OctreeChunkSpawner] Initialized");
        }

        var pool = OctreeManager.Instance.GetPool();
        if (pool.Capacity == 0) return;

        // Job용 배열 초기화
        if (!_nodeKeys.IsCreated || _nodeKeys.Length != pool.Capacity)
        {
            if (_nodeKeys.IsCreated) _nodeKeys.Dispose();
            if (_isLeafFlags.IsCreated) _isLeafFlags.Dispose();
            
            _nodeKeys = new NativeArray<long>(pool.Capacity, Allocator.Persistent);
            _isLeafFlags = new NativeArray<bool>(pool.Capacity, Allocator.Persistent);
        }

        // 1단계: 병렬로 리프 노드 수집
        var leafJob = new CollectLeafNodesJob
        {
            Nodes = pool.Nodes,
            IsUsedFlags = pool.IsUsedFlags,
            IsLeafFlags = _isLeafFlags
        };
        var leafHandle = leafJob.Schedule(pool.Capacity, 256);
        
        // 2단계: 병렬로 노드 키 생성
        var keyJob = new GenerateNodeKeysJob
        {
            Nodes = pool.Nodes,
            IsUsedFlags = pool.IsUsedFlags,
            IsLeafFlags = _isLeafFlags,
            NodeKeys = _nodeKeys
        };
        var keyHandle = keyJob.Schedule(pool.Capacity, 256, leafHandle);
        keyHandle.Complete();

        // 3단계: 메인 스레드에서 엔티티 생성/삭제
        ProcessChunksMainThread(pool);
    }
    
    void ProcessChunksMainThread(OctreeNodePool pool)
    {
        int newChunks = 0;
        int removedChunks = 0;

        var activeKeys = new NativeHashSet<long>(1000, Allocator.Temp);
        
        for (int i = 0; i < pool.Capacity; i++)
        {
            long key = _nodeKeys[i];
            if (key == -1) continue;
            
            activeKeys.Add(key);

            if (_chunkMap.ContainsKey(key)) continue;

            var node = pool.Get(i);
            var entity = CreateChunkEntity(i, node);
            _chunkMap.Add(key, entity);
            newChunks++;
        }

        _keysToRemove.Clear();
        
        foreach (var kvp in _chunkMap)
        {
            if (!activeKeys.Contains(kvp.Key))
            {
                if (EntityManager.Exists(kvp.Value))
                {
                    EntityManager.DestroyEntity(kvp.Value);
                }
                _keysToRemove.Add(kvp.Key);
                removedChunks++;
            }
        }

        for (int i = 0; i < _keysToRemove.Length; i++)
        {
            _chunkMap.Remove(_keysToRemove[i]);
        }

        activeKeys.Dispose();

        if (newChunks > 0 || removedChunks > 0)
        {
            Debug.Log($"[OctreeChunkSpawner] New: {newChunks}, Removed: {removedChunks}, Total: {_chunkMap.Count}");
        }
    }

    private Entity CreateChunkEntity(int nodeIndex, OctreeNode node)
    {
        var entity = EntityManager.CreateEntity();

        node.GetAABB(out float3 min, out float3 max);

        EntityManager.AddComponentData(entity, new ChunkData
        {
            ChunkPosition = new int3(
                (int)math.floor(node.Center.x / node.Size),
                (int)math.floor(node.Center.y / node.Size),
                (int)math.floor(node.Center.z / node.Size)
            ),
            ChunkSize = _cachedChunkSettings.ChunkSize,
            NodeIndex = nodeIndex,
            Depth = node.Depth,
            Center = node.Center,
            Size = node.Size,
            Min = min,
            Max = max
        });

        EntityManager.AddComponentData(entity, _cachedPlanetData);
        EntityManager.AddComponentData(entity, _cachedNoiseSettings);

        EntityManager.AddComponent<NoiseGenerationRequest>(entity);

        EntityManager.AddComponent<MeshGenerationRequest>(entity);
        EntityManager.SetComponentEnabled<MeshGenerationRequest>(entity, false);

        EntityManager.AddComponent<NoiseVisualizationReady>(entity);
        EntityManager.SetComponentEnabled<NoiseVisualizationReady>(entity, false);

        EntityManager.AddBuffer<NoiseDataBuffer>(entity);

        var chunkTerrainBuffer = EntityManager.AddBuffer<TerrainLayerBuffer>(entity);
        for (int i = 0; i < _cachedTerrainLayers.Length; i++)
        {
            chunkTerrainBuffer.Add(_cachedTerrainLayers[i]);
        }

        return entity;
    }

    public bool TryGetChunkEntity(int nodeIndex, out Entity entity)
    {
        var pool = OctreeManager.Instance?.GetPool();
        if (pool == null || !pool.Value.IsUsed(nodeIndex))
        {
            entity = Entity.Null;
            return false;
        }
        
        long key = _nodeKeys[nodeIndex];
        if (key == -1)
        {
            entity = Entity.Null;
            return false;
        }
        
        return _chunkMap.TryGetValue(key, out entity);
    }

    public int ActiveChunkCount => _chunkMap.Count;
}