using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class OctreeChunkSpawnerSystem : SystemBase
{
    private NativeHashMap<int, Entity> _nodeToEntity;
    private Entity _planetEntity;
    private bool _initialized;

    // ★ 캐싱용
    private NativeList<NoiseLayerBuffer> _cachedNoiseLayers;
    private PlanetData _cachedPlanetData;
    private PlanetChunkSettings _cachedChunkSettings;

    protected override void OnCreate()
    {
        _nodeToEntity = new NativeHashMap<int, Entity>(50000, Allocator.Persistent);
        _cachedNoiseLayers = new NativeList<NoiseLayerBuffer>(16, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        if (_nodeToEntity.IsCreated) 
            _nodeToEntity.Dispose();
        if (_cachedNoiseLayers.IsCreated)
            _cachedNoiseLayers.Dispose();
    }

    protected override void OnUpdate()
    {
        // ★ Client World에서만 실행 (렌더링은 클라이언트만 가능)
        if (!World.Name.Contains("Client")) return;

        if (OctreeManager.Instance == null) return;

        if (!_initialized)
        {
            var query = GetEntityQuery(typeof(PlanetTag));
            if (query.CalculateEntityCount() == 0) return;
            
            _planetEntity = query.GetSingletonEntity();
            
            // ★ 데이터 캐싱 (한 번만)
            _cachedPlanetData = EntityManager.GetComponentData<PlanetData>(_planetEntity);
            _cachedChunkSettings = EntityManager.GetComponentData<PlanetChunkSettings>(_planetEntity);
            
            var noiseBuffer = EntityManager.GetBuffer<NoiseLayerBuffer>(_planetEntity);
            _cachedNoiseLayers.Clear();
            for (int i = 0; i < noiseBuffer.Length; i++)
            {
                _cachedNoiseLayers.Add(noiseBuffer[i]);
            }
            
            _initialized = true;
            Debug.Log($"[OctreeChunkSpawner] ★ 초기화 완료! NoiseLayer 수: {_cachedNoiseLayers.Length}");
        }

        var pool = OctreeManager.Instance.GetPool();
        if (pool.Capacity == 0) return;

        int newChunks = 0;

        // 1. 새 리프 노드에 청크 엔티티 생성
        for (int i = 0; i < pool.Capacity; i++)
        {
            if (!pool.IsUsed(i)) continue;
            
            var node = pool.Get(i);
            
            if (!node.IsLeaf) continue;
            if (_nodeToEntity.ContainsKey(i)) continue;

            var entity = CreateChunkEntity(i, node);
            _nodeToEntity.Add(i, entity);
            newChunks++;
        }

        if (newChunks > 0)
        {
            Debug.Log($"[OctreeChunkSpawner] ★ 새 청크: {newChunks}, 총: {_nodeToEntity.Count}");
        }

        // 2. 반환된 노드의 청크 엔티티 제거
        var toRemove = new NativeList<int>(Allocator.Temp);
        
        foreach (var kvp in _nodeToEntity)
        {
            if (!pool.IsUsed(kvp.Key) || !pool.Get(kvp.Key).IsLeaf)
            {
                if (EntityManager.Exists(kvp.Value))
                    EntityManager.DestroyEntity(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (var key in toRemove)
            _nodeToEntity.Remove(key);
        
        toRemove.Dispose();
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

        EntityManager.AddComponent<NoiseGenerationRequest>(entity);

        EntityManager.AddComponent<MeshGenerationRequest>(entity);
        EntityManager.SetComponentEnabled<MeshGenerationRequest>(entity, false);

        EntityManager.AddComponent<NoiseVisualizationReady>(entity);
        EntityManager.SetComponentEnabled<NoiseVisualizationReady>(entity, false);

        // ★ 캐싱된 데이터에서 복사
        var destBuffer = EntityManager.AddBuffer<NoiseLayerBuffer>(entity);
        for (int i = 0; i < _cachedNoiseLayers.Length; i++)
        {
            destBuffer.Add(_cachedNoiseLayers[i]);
        }
        
        EntityManager.AddBuffer<NoiseDataBuffer>(entity);

        return entity;
    }

    public bool TryGetChunkEntity(int nodeIndex, out Entity entity)
    {
        return _nodeToEntity.TryGetValue(nodeIndex, out entity);
    }
    
    public int ActiveChunkCount => _nodeToEntity.Count;
}